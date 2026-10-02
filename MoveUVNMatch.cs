#region Metadata
/*
  Author      : Rajeev Pulari
  Platform    : Rhino 8 | Grasshopper C#
  Version     : 2026.10.02
  Component   : Move UVN + Match
  NickName    : MUVN+
  Message     : UVN + Target Match v2.1
  Description : Rhino's MoveUVN for surface control points, plus a "shrink-wrap"
                mode that pulls the surface onto a target.
                  MatchTarget = False -> each control point moves by U / V / N
                                         amounts along the surface's own U, V and
                                         normal directions (lists cycle)
                  MatchTarget = True  -> iterative closest-point fit: up to 15 passes
                                         move the control points until the surface
                                         lies on TargetGeometry

  Inputs:
    BaseSurface    : Surface      (Item) - surface to edit
    UValues        : List<double> (List) - move along U per control point (cycles; empty = 0)
    VValues        : List<double> (List) - move along V per control point (cycles; empty = 0)
    NValues        : List<double> (List) - move along the normal per control point (cycles; empty = 0)
    FixBoundaries  : bool         (Item) - keep the edge control points where they are
    MatchTarget    : bool         (Item) - True = fit onto TargetGeometry instead of using U/V/N
    TargetGeometry : GeometryBase (Item) - Surface, Brep, Extrusion or Mesh to fit onto

  Output:
    MovedSrf : the edited NURBS surface
*/
#endregion

using System;
using System.Collections;
using System.Collections.Generic;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

public class Script_Instance : GH_ScriptInstance
{
    private const int MAX_ITERATIONS = 15;

    private void RunScript(
        Surface BaseSurface,
        List<double> UValues,
        List<double> VValues,
        List<double> NValues,
        bool FixBoundaries,
        bool MatchTarget,
        GeometryBase TargetGeometry,
        ref object MovedSrf)
    {
        // Metadata + pin tooltips (once)
        if (Component != null && Component.Name != "Move UVN + Match")
        {
            Component.Name = "Move UVN + Match";
            Component.NickName = "MUVN+";
            Component.Message = "UVN + Target Match v2.1";
            Component.Description = "Moves surface control points along U/V/Normal (like MoveUVN), or fits the surface onto a target geometry.";

            var pi = Component.Params.Input;
            SetTip(pi, "BaseSurface", "Surface to edit.");
            SetTip(pi, "UValues", "Move along U per control point, row by row (U outer, V inner). Cycles. Empty = 0. List access.");
            SetTip(pi, "VValues", "Move along V per control point. Cycles. Empty = 0. List access.");
            SetTip(pi, "NValues", "Move along the surface normal per control point. Cycles. Empty = 0. List access.");
            SetTip(pi, "FixBoundaries", "Keep the edge control points where they are.");
            SetTip(pi, "MatchTarget", "True = fit the surface onto TargetGeometry (U/V/N values are ignored).");
            SetTip(pi, "TargetGeometry", "Surface, Brep, Extrusion or Mesh to fit onto.");
            SetTip(Component.Params.Output, "MovedSrf", "The edited NURBS surface.");
        }

        if (BaseSurface == null)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "BaseSurface is null. Please connect a valid surface.");
            return;
        }

        NurbsSurface nSrf = BaseSurface.ToNurbsSurface();
        if (nSrf == null)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "BaseSurface could not be converted to a NurbsSurface.");
            return;
        }

        // Target (Extrusions are turned into Breps)
        GeometryBase target = TargetGeometry;
        if (target is Extrusion) target = ((Extrusion)target).ToBrep();
        Brep targetBrep = target as Brep;
        Surface targetSrf = target as Surface;
        Mesh targetMesh = target as Mesh;

        bool match = MatchTarget;
        if (match && (targetBrep == null && targetSrf == null && targetMesh == null))
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                TargetGeometry == null
                  ? "MatchTarget is True, but TargetGeometry is null. Falling back to UVN lists."
                  : "TargetGeometry must be a Surface, Brep, Extrusion or Mesh. Falling back to UVN lists.");
            match = false;
        }

        var uVals = (UValues == null || UValues.Count == 0) ? new List<double> { 0.0 } : UValues;
        var vVals = (VValues == null || VValues.Count == 0) ? new List<double> { 0.0 } : VValues;
        var nVals = (NValues == null || NValues.Count == 0) ? new List<double> { 0.0 } : NValues;

        int uCount = nSrf.Points.CountU;
        int vCount = nSrf.Points.CountV;

        RhinoDoc doc = RhinoDocument ?? RhinoDoc.ActiveDoc;
        double tol = (doc != null) ? doc.ModelAbsoluteTolerance : 0.001;

        if (match)
        {
            // Parameter of each control point, taken on the NURBS surface itself
            // (BaseSurface can be parameterised differently from its NURBS form,
            //  which made the old map point at the wrong spots)
            var paramU = new double[uCount, vCount];
            var paramV = new double[uCount, vCount];
            for (int i = 0; i < uCount; i++)
                for (int j = 0; j < vCount; j++)
                {
                    double pu, pv;
                    nSrf.ClosestPoint(nSrf.Points.GetControlPoint(i, j).Location, out pu, out pv);
                    paramU[i, j] = pu;
                    paramV[i, j] = pv;
                }

            int iter = 0;
            double maxGap = double.MaxValue;
            for (; iter < MAX_ITERATIONS; iter++)
            {
                var shifts = new Vector3d[uCount, vCount];
                maxGap = 0.0;

                // Pass 1: all gaps at once (avoids rippling)
                for (int i = 0; i < uCount; i++)
                    for (int j = 0; j < vCount; j++)
                    {
                        if (FixBoundaries && IsBoundary(i, j, uCount, vCount)) continue;

                        Point3d onSrf = nSrf.PointAt(paramU[i, j], paramV[i, j]);
                        Point3d onTarget = ClosestOnTarget(onSrf, targetMesh, targetBrep, targetSrf);
                        shifts[i, j] = onTarget - onSrf;
                        maxGap = Math.Max(maxGap, shifts[i, j].Length);
                    }

                // Converged: stop early instead of always running 15 passes
                if (maxGap <= tol) break;

                // Pass 2: move the control points
                for (int i = 0; i < uCount; i++)
                    for (int j = 0; j < vCount; j++)
                    {
                        if (shifts[i, j].IsZero) continue;
                        ControlPoint cp = nSrf.Points.GetControlPoint(i, j);
                        nSrf.Points.SetControlPoint(i, j, new ControlPoint(cp.Location + shifts[i, j], cp.Weight));
                    }
            }

            Component.Message = "UVN + Target Match v2.1 | " + iter + " pass(es), gap " + maxGap.ToString("0.###");
            if (maxGap > tol)
                Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                    "Largest remaining gap after " + MAX_ITERATIONS + " passes: " + maxGap.ToString("0.####") +
                    " (more control points fit closer; FixBoundaries also limits the fit).");
        }
        else
        {
            // Standard MoveUVN: index runs U outer, V inner (boundary points count too)
            int index = 0, failed = 0;
            for (int i = 0; i < uCount; i++)
                for (int j = 0; j < vCount; j++, index++)
                {
                    if (FixBoundaries && IsBoundary(i, j, uCount, vCount)) continue;

                    Vector3d dirU, dirV, dirN;
                    if (!nSrf.Points.UVNDirectionsAt(i, j, out dirU, out dirV, out dirN)) { failed++; continue; }

                    Vector3d move = dirU * uVals[index % uVals.Count]
                                  + dirV * vVals[index % vVals.Count]
                                  + dirN * nVals[index % nVals.Count];
                    ControlPoint cp = nSrf.Points.GetControlPoint(i, j);
                    nSrf.Points.SetControlPoint(i, j, new ControlPoint(cp.Location + move, cp.Weight));
                }

            if (failed > 0)
                Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    failed + " control point(s) had no U/V/N directions (degenerate spot) and were not moved.");
            Component.Message = "UVN + Target Match v2.1 | UVN";
        }

        MovedSrf = nSrf;
    }

    // <Custom additional code>
    private static bool IsBoundary(int i, int j, int uCount, int vCount)
    {
        return i == 0 || i == uCount - 1 || j == 0 || j == vCount - 1;
    }

    private static Point3d ClosestOnTarget(Point3d p, Mesh mesh, Brep brep, Surface srf)
    {
        if (mesh != null)
        {
            Point3d q = mesh.ClosestPoint(p);
            return q.IsValid ? q : p;
        }
        if (brep != null)
        {
            Point3d q = brep.ClosestPoint(p);
            return q.IsValid ? q : p;
        }
        if (srf != null)
        {
            double u, v;
            if (srf.ClosestPoint(p, out u, out v)) return srf.PointAt(u, v);
        }
        return p;
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
