/*
  Component : Grid In Boundary List  (nickname: GridBoundList)
  Author    : Rajeev Pulari
  Version   : 2.1  (2026-10-05)
  Platform  : Rhino 8 | Grasshopper C# Script

  Purpose
  -------
  Builds a U/V grid of lines inside a closed planar boundary, with a repeating
  list of spacings in each direction, measured from a start point. Returns the
  U lines, the V lines and every grid node (U x V crossings + boundary hits).

  Inputs
  ------
  Boundary     (item) Closed planar curve.
  USpacing     (list) Spacing between U lines, repeated (e.g. 1000, 1500).
  VSpacing     (list) Spacing between V lines, repeated.
  UDirection   (item) Direction the U lines RUN.
  VDirection   (item) Direction the V lines RUN. Empty = perpendicular to U.
  StartPoint   (item) Grid origin - a U line and a V line pass through it.

  Outputs
  -------
  UCurves             U lines trimmed to the boundary, in order across the grid.
  VCurves             V lines trimmed to the boundary, in order across the grid.
  IntersectionPoints  Unique grid nodes: U x V crossings + line ends on the boundary.

  Spacing: U lines are spaced along the in-plane perpendicular of UDirection
  (the V side), V lines along the perpendicular of VDirection. The spacing list
  repeats outward from StartPoint in both directions.

  v2.1 changes
  ------------
  - StartPoint off the boundary's plane put the grid in a parallel plane: the
    lines never crossed the boundary and whole untrimmed lines were output.
    StartPoint is now projected onto the boundary plane.
  - Directions with an out-of-plane part tilted the grid out of the plane; both
    directions are now projected into the boundary plane.
  - Lines came out as 0, +1, +2 ... then -1, -2 (not in order) -> sorted.
  - Overlaps (a grid line running along a straight boundary edge) only used one
    parameter; both overlap ends are now used.
  - VDirection parallel to UDirection gave duplicate lines -> warning.
  - Very small spacing could create millions of lines -> capped at 5000 per
    direction with a warning.
  - Non-planar boundary silently used World XY -> warning.
  - Renamed USpace, VSpace, uDirVect, vDirVect, IntersectPts ->
    USpacing, VSpacing, UDirection, VDirection, IntersectionPoints.
  - Metadata and tooltips set once; message shows the line/node counts.
*/

using Rhino;
using Rhino.Geometry;
using Grasshopper.Kernel;
using System;
using System.Collections.Generic;

public class Script_Instance : GH_ScriptInstance
{
    private const int MaxLinesPerDirection = 5000;
    private const int MaxSteps = 1000000;   // guards tiny spacing far from the start point
    private bool _metaSet = false;

    private void RunScript(
		Curve Boundary,
		List<double> USpacing,
		List<double> VSpacing,
		Vector3d UDirection,
		Vector3d VDirection,
		Point3d StartPoint,
		ref object UCurves,
		ref object VCurves,
		ref object IntersectionPoints)
    {
        SetMetadata();
        Component.Message = "U, V & Pts";

        // ---- Validation --------------------------------------------------
        if (Boundary == null || !Boundary.IsValid || !Boundary.IsClosed)
        { Warn("Boundary must be a valid, closed curve."); return; }
        if (USpacing == null || USpacing.Count == 0) { Warn("USpacing is empty."); return; }
        if (VSpacing == null || VSpacing.Count == 0) { Warn("VSpacing is empty."); return; }
        foreach (double s in USpacing) if (!(s > 0)) { Warn("All USpacing values must be greater than 0."); return; }
        foreach (double s in VSpacing) if (!(s > 0)) { Warn("All VSpacing values must be greater than 0."); return; }
        if (!UDirection.IsValid || UDirection.IsZero) { Warn("UDirection is empty or zero."); return; }
        if (!StartPoint.IsValid) { Warn("StartPoint is invalid."); return; }

        double tol = RhinoDocument != null ? RhinoDocument.ModelAbsoluteTolerance : 0.001;

        // ---- Boundary plane ---------------------------------------------
        Plane crvPlane;
        if (!Boundary.TryGetPlane(out crvPlane, tol))
        {
            crvPlane = Plane.WorldXY;
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                "Boundary is not planar - using World XY; results may be incomplete.");
        }
        Vector3d n = crvPlane.ZAxis;

        // Grid origin on the boundary plane
        Point3d origin = crvPlane.ClosestPoint(StartPoint);

        // ---- Directions, in the boundary plane --------------------------
        Vector3d uDir = UDirection - (UDirection * n) * n;
        if (!uDir.Unitize()) { Warn("UDirection is perpendicular to the boundary plane."); return; }

        Vector3d vDir;
        if (!VDirection.IsValid || VDirection.IsZero)
            vDir = Vector3d.CrossProduct(n, uDir);           // default: perpendicular to U
        else
            vDir = VDirection - (VDirection * n) * n;
        if (!vDir.Unitize()) { Warn("VDirection is perpendicular to the boundary plane."); return; }

        if (Math.Abs(uDir * vDir) > 0.9999)
        { Warn("UDirection and VDirection are parallel - the U and V lines would coincide."); return; }

        // ---- Lines -------------------------------------------------------
        List<Curve> uCrvs = GenerateLines(Boundary, crvPlane, origin, uDir, USpacing, tol, "U");
        List<Curve> vCrvs = GenerateLines(Boundary, crvPlane, origin, vDir, VSpacing, tol, "V");

        // ---- Nodes -------------------------------------------------------
        List<Point3d> pts = new List<Point3d>();
        foreach (Curve u in uCrvs)
        {
            foreach (Curve v in vCrvs)
            {
                var events = Rhino.Geometry.Intersect.Intersection.CurveCurve(u, v, tol, tol);
                if (events == null) continue;
                foreach (var ev in events) pts.Add(ev.PointA);
            }
        }
        // Trimmed lines start and end on the boundary -> perimeter nodes
        foreach (Curve u in uCrvs) { pts.Add(u.PointAtStart); pts.Add(u.PointAtEnd); }
        foreach (Curve v in vCrvs) { pts.Add(v.PointAtStart); pts.Add(v.PointAtEnd); }

        Point3d[] uniquePts = pts.Count > 0 ? Point3d.CullDuplicates(pts, tol) : new Point3d[0];

        UCurves = uCrvs;
        VCurves = vCrvs;
        IntersectionPoints = uniquePts;

        Component.Message = "U " + uCrvs.Count + " | V " + vCrvs.Count + " | Pts " + uniquePts.Length;
    }

    // Lines that RUN along runDir, spaced along its in-plane perpendicular,
    // trimmed to the boundary. Returned in order across the grid.
    private List<Curve> GenerateLines(Curve boundary, Plane crvPlane, Point3d origin,
        Vector3d runDir, List<double> spacing, double tol, string label)
    {
        List<Curve> result = new List<Curve>();

        Vector3d perp = Vector3d.CrossProduct(crvPlane.ZAxis, runDir);
        if (!perp.Unitize()) return result;
        Plane linePlane = new Plane(origin, runDir, perp);   // X = run, Y = spacing

        Box box;
        boundary.GetBoundingBox(linePlane, out box);
        if (!box.IsValid) return result;

        double extend = Math.Sqrt(box.X.Length * box.X.Length + box.Y.Length * box.Y.Length) * 0.1 + tol * 10;

        bool capped;
        List<double> stations = MakeStations(box.Y.Min, box.Y.Max, spacing, out capped);
        if (capped)
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                label + " spacing is too small for this boundary - capped at " + MaxLinesPerDirection + " lines.");

        foreach (double s in stations)
        {
            Point3d p1 = linePlane.PointAt(box.X.Min - extend, s);
            Point3d p2 = linePlane.PointAt(box.X.Max + extend, s);
            result.AddRange(InsideSegments(new LineCurve(p1, p2), boundary, crvPlane, tol));
        }
        return result;
    }

    // Stations (Y offsets from the origin) from a repeating spacing list,
    // walking outward both ways, sorted low -> high.
    private List<double> MakeStations(double min, double max, List<double> spacing, out bool capped)
    {
        capped = false;
        List<double> coords = new List<double>();
        if (min <= 0 && max >= 0) coords.Add(0.0);

        double cur = 0.0;
        int idx = 0;
        while (cur < max)
        {
            cur += spacing[idx % spacing.Count];
            idx++;
            if (cur >= min && cur <= max) coords.Add(cur);
            if (coords.Count > MaxLinesPerDirection || idx > MaxSteps) { capped = true; break; }
        }

        cur = 0.0;
        idx = 0;
        while (!capped && cur > min)
        {
            cur -= spacing[idx % spacing.Count];
            idx++;
            if (cur >= min && cur <= max) coords.Add(cur);
            if (coords.Count > MaxLinesPerDirection || idx > MaxSteps) { capped = true; break; }
        }

        coords.Sort();
        return coords;
    }

    // Pieces of a line that lie inside the closed boundary.
    private List<Curve> InsideSegments(Curve lineCrv, Curve boundary, Plane crvPlane, double tol)
    {
        List<Curve> segments = new List<Curve>();
        var events = Rhino.Geometry.Intersect.Intersection.CurveCurve(lineCrv, boundary, tol, tol);

        if (events == null || events.Count == 0)
        {
            if (boundary.Contains(lineCrv.PointAtNormalizedLength(0.5), crvPlane, tol) != PointContainment.Outside)
                segments.Add(lineCrv);
            return segments;
        }

        List<double> tParams = new List<double> { lineCrv.Domain.Min, lineCrv.Domain.Max };
        foreach (var ev in events)
        {
            if (ev.IsOverlap) { tParams.Add(ev.OverlapA.T0); tParams.Add(ev.OverlapA.T1); }
            else tParams.Add(ev.ParameterA);
        }
        tParams.Sort();

        for (int i = 0; i < tParams.Count - 1; i++)
        {
            double t0 = tParams[i], t1 = tParams[i + 1];
            if (t1 - t0 <= tol) continue;
            Curve segment = lineCrv.Trim(t0, t1);
            if (segment == null) continue;
            if (boundary.Contains(segment.PointAtNormalizedLength(0.5), crvPlane, tol) != PointContainment.Outside)
                segments.Add(segment);
        }
        return segments;
    }

    // ---------------------------------------------------------------- helpers

    private void Warn(string msg)
    {
        Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, msg);
    }

    private void SetMetadata()
    {
        if (_metaSet) return;
        _metaSet = true;

        Component.Name = "Grid In Boundary List";
        Component.NickName = "GridBoundList";
        Component.Description =
            "U/V grid of lines inside a closed planar boundary, with repeating spacing lists, " +
            "custom directions and a start point. Outputs U lines, V lines and grid nodes. Rajeev Pulari, v2.1.";

        var ins = Component.Params.Input;
        SetTip(ins, "Boundary", "Closed planar curve the grid is trimmed to.");
        SetTip(ins, "USpacing", "Spacing between U lines (list, repeats). All values > 0.");
        SetTip(ins, "VSpacing", "Spacing between V lines (list, repeats). All values > 0.");
        SetTip(ins, "UDirection", "Direction the U lines run (projected into the boundary plane).");
        SetTip(ins, "VDirection", "Direction the V lines run. Empty = perpendicular to UDirection.");
        SetTip(ins, "StartPoint", "Grid origin - one U and one V line pass through it (projected onto the boundary plane).");

        var outs = Component.Params.Output;
        SetTip(outs, "UCurves", "U lines trimmed to the boundary, in order across the grid.");
        SetTip(outs, "VCurves", "V lines trimmed to the boundary, in order across the grid.");
        SetTip(outs, "IntersectionPoints", "Unique grid nodes: U x V crossings plus line ends on the boundary.");
    }

    // Match pins by Name (the script variable), fall back to NickName.
    // Only NickName/Description are changed - never Name.
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
}
