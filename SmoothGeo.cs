#region Metadata
/*
  Author      : Rajeev Pulari + Gemini
  Platform    : Rhino 8 | Grasshopper C#
  Version     : 2026.10.02
  Component   : Universal Smoother
  NickName    : SmoothGeo
  Message     : Smooth v2.1
  Description : Rhino's Smooth command for Meshes, Curves, Surfaces and single-face
                Breps / Extrusions: moves points towards the average of their
                neighbours, step by step, optionally only along X / Y / Z of the
                chosen coordinate system.

  Inputs:
    Geometry      : GeometryBase (Item) - Mesh, Curve, Surface, single-face Brep or Extrusion
    SmoothFactor  : double (Item) - -1..1, how far points move per step (0 = 0.5)
    Steps         : int    (Item) - number of smoothing passes (0 = 1)
    SmoothX/Y/Z   : bool   (Item) - which directions may move
    FixBoundaries : bool   (Item) - keep open edges / curve ends fixed
    CoordSystem   : int    (Item) - 0 = World, 1 = CPlane (SmoothPlane), 2 = Object
    SmoothPlane   : Plane  (Item) - plane for CoordSystem 1. Missing = World XY
    VertexIndices : List<int> (List) - MESH ONLY: smooth just these vertices (empty = all)

  Outputs:
    Result : smoothed geometry
    Info   : what was done
*/
#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

public class Script_Instance : GH_ScriptInstance
{
  // VertexIndices : list access, everything else item access
  private void RunScript(
    GeometryBase Geometry,
    double SmoothFactor,
    int Steps,
    bool SmoothX,
    bool SmoothY,
    bool SmoothZ,
    bool FixBoundaries,
    int CoordSystem,
    Plane SmoothPlane,
    List<int> VertexIndices,
    ref object Result,
    ref object Info)
  {
    // Metadata + pin tooltips (once)
    if (Component != null && Component.Name != "Universal Smoother")
    {
      Component.Name = "Universal Smoother";
      Component.NickName = "SmoothGeo";
      Component.Message = "Smooth v2.1";
      Component.Description = "Smooths Meshes, Curves, Surfaces and single-face Breps (Rhino Smooth), with direction and coordinate-system control.";

      var pi = Component.Params.Input;
      SetTip(pi, "Geometry", "Mesh, Curve, Surface, single-face Brep or Extrusion.");
      SetTip(pi, "SmoothFactor", "-1..1, how far points move per step. 0 = 0.5.");
      SetTip(pi, "Steps", "Number of smoothing passes. 0 = 1.");
      SetTip(pi, "SmoothX", "Allow movement along X of the coordinate system.");
      SetTip(pi, "SmoothY", "Allow movement along Y of the coordinate system.");
      SetTip(pi, "SmoothZ", "Allow movement along Z of the coordinate system.");
      SetTip(pi, "FixBoundaries", "Keep open edges / curve end points fixed.");
      SetTip(pi, "CoordSystem", "0 = World, 1 = CPlane (uses SmoothPlane), 2 = Object.");
      SetTip(pi, "SmoothPlane", "Plane used when CoordSystem = 1. Missing = World XY.");
      SetTip(pi, "VertexIndices", "Mesh only: smooth just these vertex indices. Empty = all. List access.");
      SetTip(Component.Params.Output, "Result", "Smoothed geometry.");
      SetTip(Component.Params.Output, "Info", "What was done.");
    }

    // 1. Settings
    double factor = (SmoothFactor == 0.0) ? 0.5 : Math.Max(-1.0, Math.Min(1.0, SmoothFactor));
    int steps = Math.Max(1, Steps);
    Plane plane = SmoothPlane.IsValid ? SmoothPlane : Plane.WorldXY;

    SmoothingCoordinateSystem csys;
    switch (CoordSystem)
    {
      case 1:  csys = SmoothingCoordinateSystem.CPlane; break;
      case 2:  csys = SmoothingCoordinateSystem.Object; break;
      default: csys = SmoothingCoordinateSystem.World;  break;
    }

    if (!SmoothX && !SmoothY && !SmoothZ)
      Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "SmoothX, SmoothY and SmoothZ are all False - nothing can move.");

    if (Geometry == null)
    {
      Info = "No geometry provided.";
      Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No geometry provided.");
      return;
    }

    // 2. Route by type
    string info;
    GeometryBase geo = Geometry;
    if (geo is Extrusion) geo = ((Extrusion)geo).ToBrep();

    if (geo is Mesh)
    {
      Result = SmoothMesh((Mesh)geo, factor, steps, SmoothX, SmoothY, SmoothZ, FixBoundaries, csys, plane, VertexIndices, out info);
    }
    else if (geo is Curve)
    {
      Result = SmoothCurve((Curve)geo, factor, steps, SmoothX, SmoothY, SmoothZ, FixBoundaries, csys, plane, out info);
    }
    else if (geo is Surface)
    {
      Result = SmoothSurface((Surface)geo, factor, steps, SmoothX, SmoothY, SmoothZ, FixBoundaries, csys, plane, out info);
    }
    else if (geo is Brep)
    {
      Brep brep = (Brep)geo;
      if (brep.Faces.Count == 1)
      {
        BrepFace face = brep.Faces[0];
        if (!face.IsSurface)
          Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
            "The face is trimmed - the underlying (untrimmed) surface is smoothed and returned.");
        Surface smoothed = SmoothSurface(face.UnderlyingSurface(), factor, steps, SmoothX, SmoothY, SmoothZ, FixBoundaries, csys, plane, out info);
        Result = (smoothed != null) ? (object)smoothed.ToBrep() : (object)brep;
      }
      else
      {
        Result = brep;
        info = "Multi-face Breps not supported. Pass individual surfaces (Deconstruct Brep).";
        Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, info);
      }
    }
    else
    {
      Result = null;
      info = "Unsupported geometry type: " + geo.GetType().Name + ".";
      Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, info);
    }

    Info = info;
  }

  // <Custom additional code>

  private Mesh SmoothMesh(Mesh mesh, double factor, int steps, bool xS, bool yS, bool zS, bool fixBnd,
                          SmoothingCoordinateSystem csys, Plane plane, List<int> vertexIndices, out string info)
  {
    Mesh m = mesh.DuplicateMesh();
    int[] indices = null;
    if (vertexIndices != null && vertexIndices.Count > 0)
    {
      // Only valid indices (an out-of-range index used to make the smooth fail)
      indices = vertexIndices.Where(i => i >= 0 && i < m.Vertices.Count).Distinct().ToArray();
      int dropped = vertexIndices.Count - indices.Length;
      if (dropped > 0)
        Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, dropped + " vertex index/indices out of range or repeated - ignored.");
      if (indices.Length == 0) { info = "No valid vertex indices - mesh unchanged."; return m; }
    }

    int done = 0;
    for (int i = 0; i < steps; i++)
    {
      bool ok = (indices != null)
        ? m.Smooth(indices, factor, xS, yS, zS, fixBnd, csys, plane)
        : m.Smooth(factor, xS, yS, zS, fixBnd, csys, plane);
      if (!ok) break;
      done++;
    }

    m.Normals.ComputeNormals();
    m.Compact();
    info = "Mesh smoothed | steps=" + done + "/" + steps + " | csys=" + csys +
           (indices != null ? " | vertices=" + indices.Length : "");
    if (done < steps) Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Mesh smoothing stopped after " + done + " step(s).");
    return m;
  }

  // The plane is now passed to curves and surfaces too (before, CPlane mode ignored it for them)
  private Curve SmoothCurve(Curve curve, double factor, int steps, bool xS, bool yS, bool zS, bool fixBnd,
                            SmoothingCoordinateSystem csys, Plane plane, out string info)
  {
    Curve c = curve.DuplicateCurve();
    int done = 0;
    for (int i = 0; i < steps; i++)
    {
      Curve s = c.Smooth(factor, xS, yS, zS, fixBnd, csys, plane);
      if (s == null) break;
      c = s;
      done++;
    }
    info = "Curve smoothed | steps=" + done + "/" + steps + " | csys=" + csys;
    if (done < steps) Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Curve smoothing stopped after " + done + " step(s).");
    return c;
  }

  private Surface SmoothSurface(Surface srf, double factor, int steps, bool xS, bool yS, bool zS, bool fixBnd,
                                SmoothingCoordinateSystem csys, Plane plane, out string info)
  {
    Surface s = (Surface)srf.Duplicate();
    int done = 0;
    for (int i = 0; i < steps; i++)
    {
      Surface r = s.Smooth(factor, xS, yS, zS, fixBnd, csys, plane);
      if (r == null) break;
      s = r;
      done++;
    }
    info = "Surface smoothed | steps=" + done + "/" + steps + " | csys=" + csys;
    if (done < steps) Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Surface smoothing stopped after " + done + " step(s).");
    return s;
  }

  // Find the pin by its variable name (not index), so an extra "out" pin can't shift names
  private void SetTip(IList<IGH_Param> ps, string name, string tip)
  {
    if (ps == null) return;
    IGH_Param hit = null;
    foreach (IGH_Param p in ps)
      if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) { hit = p; break; }
    if (hit == null)
      foreach (IGH_Param p in ps)
        if (string.Equals(p.NickName, name, StringComparison.OrdinalIgnoreCase)) { hit = p; break; }
    if (hit == null) return;
    hit.NickName = name;
    hit.Description = tip;
  }
  // </Custom additional code>
}
