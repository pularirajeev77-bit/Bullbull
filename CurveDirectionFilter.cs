#region Metadata
/*
  Platform    : Rhino 8 | Grasshopper C#
  Component   : Curve Direction Filter
  NickName    : DirFilter
  Message     : Direction Filter v2.1
  Description : Sorts curves by how well their overall direction (start -> end)
                matches a target vector, within an angle tolerance in degrees.
                Bidirectional also accepts curves pointing the opposite way.

  Inputs:
    Curves         : List<Curve> (List) - curves to test
    TargetVector   : Vector3d    (Item) - direction to match
    AngleTolerance : double      (Item) - max deviation in DEGREES. 0 = model angle tolerance
    Bidirectional  : bool        (Item) - True = reversed (anti-parallel) curves also match

  Outputs:
    SelectedCurves  : curves within the tolerance
    RejectedCurves  : all other curves (incl. closed / zero-length ones)
    SelectedIndices : their indices in Curves
    RejectedIndices : their indices in Curves
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
    // Curves : list access | TargetVector, AngleTolerance, Bidirectional : item access
    private void RunScript(
        List<Curve> Curves,
        Vector3d TargetVector,
        double AngleTolerance,
        bool Bidirectional,
        ref object SelectedCurves,
        ref object RejectedCurves,
        ref object SelectedIndices,
        ref object RejectedIndices)
    {
        // Metadata + pin tooltips (once)
        if (Component != null && Component.Name != "Curve Direction Filter")
        {
            Component.Name        = "Curve Direction Filter";
            Component.NickName    = "DirFilter";
            Component.Message     = "Direction Filter v2.1";
            Component.Description = "Filters curves by alignment to a target vector within an angular tolerance. "
                                  + "Supports bidirectional matching for anti-parallel curves. "
                                  + "Outputs selected and rejected curves with their original indices.";

            var pi = Component.Params.Input;
            SetTip(pi, "Curves", "Curves to test (direction = start point -> end point). List access.");
            SetTip(pi, "TargetVector", "Direction to match.");
            SetTip(pi, "AngleTolerance", "Max deviation in DEGREES. 0 = model angle tolerance.");
            SetTip(pi, "Bidirectional", "True = curves pointing the opposite way also match.");
            var po = Component.Params.Output;
            SetTip(po, "SelectedCurves", "Curves within the angle tolerance.");
            SetTip(po, "RejectedCurves", "All other curves, incl. closed / zero-length ones.");
            SetTip(po, "SelectedIndices", "Indices of the selected curves in Curves.");
            SetTip(po, "RejectedIndices", "Indices of the rejected curves in Curves.");
        }

        // 1. Inputs
        if (Curves == null || Curves.Count == 0)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Please provide a valid list of Curves.");
            return;
        }
        if (!TargetVector.IsValid || TargetVector.IsTiny())
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Please provide a valid, non-zero Target Vector.");
            return;
        }

        RhinoDoc doc = RhinoDocument ?? RhinoDoc.ActiveDoc;
        double absTol = (doc != null) ? doc.ModelAbsoluteTolerance : 0.001;

        // 0 / unset used to mean "exact match only", which almost never happens with real geometry
        double tolDeg = Math.Abs(AngleTolerance);
        if (tolDeg <= 0) tolDeg = (doc != null) ? doc.ModelAngleToleranceDegrees : 1.0;
        double tolRad = RhinoMath.ToRadians(tolDeg);

        Vector3d target = TargetVector;
        target.Unitize();

        var selected = new List<Curve>();
        var rejected = new List<Curve>();
        var selIdx = new List<int>();
        var rejIdx = new List<int>();
        int skipped = 0, noDirection = 0;

        // 2. Compare each curve's overall direction with the target
        for (int i = 0; i < Curves.Count; i++)
        {
            Curve crv = Curves[i];
            if (crv == null || !crv.IsValid) { skipped++; continue; }

            Vector3d dir = crv.PointAtEnd - crv.PointAtStart;

            // Closed or (nearly) zero-length: no direction -> rejected
            if (dir.Length <= absTol)
            {
                noDirection++;
                rejected.Add(crv); rejIdx.Add(i);
                continue;
            }
            dir.Unitize();

            double angle = Vector3d.VectorAngle(target, dir);
            bool match = angle <= tolRad || (Bidirectional && Math.Abs(angle - Math.PI) <= tolRad);

            if (match) { selected.Add(crv); selIdx.Add(i); }
            else       { rejected.Add(crv); rejIdx.Add(i); }
        }

        if (skipped > 0)
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, skipped + " null/invalid curve(s) skipped - they are in neither output.");
        if (noDirection > 0)
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, noDirection + " closed / zero-length curve(s) have no direction and were rejected.");

        Component.Message = "Direction Filter v2.1 | " + selected.Count + " in / " + rejected.Count + " out";

        SelectedCurves = selected;
        RejectedCurves = rejected;
        SelectedIndices = selIdx;
        RejectedIndices = rejIdx;
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
