#region Metadata
/*
  Platform    : Rhino 8 | Grasshopper C#
  Component   : Coplanar Curve Filter
  NickName    : CoplanarFilter
  Message     : Coplanar Filter v2.1
  Description : Sorts curves into the ones lying IN a reference plane (within a
                distance tolerance) and the rest, with their original indices.

  Inputs:
    Curves    : List<Curve> (List) - curves to test
    RefPlane  : Plane       (Item) - reference plane (infinite). Missing = World XY
    Tolerance : double      (Item) - max distance from the plane. 0 = model tolerance

  Outputs:
    CoplanarCurves     : curves lying entirely in the plane
    NonCoplanarCurves  : all other curves
    CoplanarIndices    : their indices in Curves
    NonCoplanarIndices : their indices in Curves
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
    // Curves : list access | RefPlane, Tolerance : item access
    private void RunScript(
        List<Curve> Curves,
        Plane RefPlane,
        double Tolerance,
        ref object CoplanarCurves,
        ref object NonCoplanarCurves,
        ref object CoplanarIndices,
        ref object NonCoplanarIndices)
    {
        // Metadata + pin tooltips (once)
        if (Component != null && Component.Name != "Coplanar Curve Filter")
        {
            Component.Name        = "Coplanar Curve Filter";
            Component.NickName    = "CoplanarFilter";
            Component.Message     = "Coplanar Filter v2.1";
            Component.Description = "Filters a list of curves against a reference plane. "
                                  + "Outputs coplanar and non-coplanar curves with their original indices.";

            var pi = Component.Params.Input;
            SetTip(pi, "Curves", "Curves to test. List access.");
            SetTip(pi, "RefPlane", "Reference plane (infinite). Missing = World XY.");
            SetTip(pi, "Tolerance", "Max distance from the plane. 0 = model tolerance.");
            var po = Component.Params.Output;
            SetTip(po, "CoplanarCurves", "Curves lying entirely in the plane.");
            SetTip(po, "NonCoplanarCurves", "All other curves.");
            SetTip(po, "CoplanarIndices", "Indices of the coplanar curves in Curves.");
            SetTip(po, "NonCoplanarIndices", "Indices of the non-coplanar curves in Curves.");
        }

        // 1. Inputs
        if (Curves == null || Curves.Count == 0)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Please provide a valid list of Curves.");
            return;
        }

        Plane plane = RefPlane;
        if (!plane.IsValid)
        {
            plane = Plane.WorldXY;
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "No valid plane provided. Defaulting to World XY.");
        }

        double tol = Tolerance;
        if (tol <= 0.0)
        {
            RhinoDoc doc = RhinoDocument ?? RhinoDoc.ActiveDoc;
            tol = (doc != null) ? doc.ModelAbsoluteTolerance : 0.001;
        }

        var coplanar = new List<Curve>();
        var nonCoplanar = new List<Curve>();
        var coplanarIdx = new List<int>();
        var nonCoplanarIdx = new List<int>();
        int skipped = 0;

        // 2. Whole curve within tolerance of the plane?
        for (int i = 0; i < Curves.Count; i++)
        {
            Curve crv = Curves[i];
            if (crv == null || !crv.IsValid) { skipped++; continue; }

            if (crv.IsInPlane(plane, tol)) { coplanar.Add(crv); coplanarIdx.Add(i); }
            else                           { nonCoplanar.Add(crv); nonCoplanarIdx.Add(i); }
        }

        if (skipped > 0)
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                skipped + " null/invalid curve(s) skipped - they are in neither output.");

        Component.Message = "Coplanar Filter v2.1 | " + coplanar.Count + " in / " + nonCoplanar.Count + " out";

        CoplanarCurves = coplanar;
        NonCoplanarCurves = nonCoplanar;
        CoplanarIndices = coplanarIdx;
        NonCoplanarIndices = nonCoplanarIdx;
    }

    // <Custom additional code>
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
