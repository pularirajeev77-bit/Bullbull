#region Metadata
/*
  Platform    : Rhino 8 | Grasshopper C#
  Component   : Curve Plane Intersection Filter
  NickName    : CrvPlaneFilter
  Message     : Crv Plane Filter v2.1
  Description : Sorts curves into the ones that cross / touch a reference plane
                and the ones that don't, with their original list indices.

  Inputs:
    Curves    : List<Curve> (List) - curves to test
    RefPlane  : Plane       (Item) - reference plane (infinite). Missing = World XY
    Tolerance : double      (Item) - intersection tolerance. 0 = model tolerance

  Outputs:
    IntersectingCurves     : curves that cross, touch or lie in the plane
    NonIntersectingCurves  : curves entirely on one side of the plane
    IntersectingIndices    : their indices in Curves
    NonIntersectingIndices : their indices in Curves
*/
#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Rhino;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;

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
        ref object IntersectingCurves,
        ref object NonIntersectingCurves,
        ref object IntersectingIndices,
        ref object NonIntersectingIndices)
    {
        // Metadata + pin tooltips (once)
        if (Component != null && Component.Name != "Curve Plane Intersection Filter")
        {
            Component.Name        = "Curve Plane Intersection Filter";
            Component.NickName    = "CrvPlaneFilter";
            Component.Message     = "Crv Plane Filter v2.1";
            Component.Description = "Filters curves by intersection against a reference plane. "
                                  + "Outputs intersecting and non-intersecting curves with their original indices.";

            var pi = Component.Params.Input;
            SetTip(pi, "Curves", "Curves to test. List access.");
            SetTip(pi, "RefPlane", "Reference plane (infinite). Missing = World XY.");
            SetTip(pi, "Tolerance", "Intersection tolerance. 0 = model tolerance.");
            var po = Component.Params.Output;
            SetTip(po, "IntersectingCurves", "Curves that cross, touch or lie in the plane.");
            SetTip(po, "NonIntersectingCurves", "Curves entirely on one side of the plane.");
            SetTip(po, "IntersectingIndices", "Indices of the intersecting curves in Curves.");
            SetTip(po, "NonIntersectingIndices", "Indices of the non-intersecting curves in Curves.");
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

        var intersecting = new List<Curve>();
        var nonIntersecting = new List<Curve>();
        var intersectingIdx = new List<int>();
        var nonIntersectingIdx = new List<int>();
        int skipped = 0;

        // 2. Test each curve against the infinite plane
        for (int i = 0; i < Curves.Count; i++)
        {
            Curve crv = Curves[i];
            if (crv == null || !crv.IsValid) { skipped++; continue; }

            CurveIntersections events = Intersection.CurvePlane(crv, plane, tol);
            if (events != null && events.Count > 0)
            {
                intersecting.Add(crv);
                intersectingIdx.Add(i);
            }
            else
            {
                nonIntersecting.Add(crv);
                nonIntersectingIdx.Add(i);
            }
        }

        if (skipped > 0)
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                skipped + " null/invalid curve(s) skipped - they are in neither output.");

        Component.Message = "Crv Plane Filter v2.1 | " + intersecting.Count + " hit / " + nonIntersecting.Count + " miss";

        IntersectingCurves = intersecting;
        NonIntersectingCurves = nonIntersecting;
        IntersectingIndices = intersectingIdx;
        NonIntersectingIndices = nonIntersectingIdx;
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
