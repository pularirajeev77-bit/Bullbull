/*
  Component : Curve Intersect Grid  (nickname: CrvIntGrid)
  Author    : Rajeev Pulari
  Version   : 1.0  (2026-10-05)
  Platform  : Rhino 8 | Grasshopper C# Script

  Purpose
  -------
  Mimics PanelingTools "ptIntersect": intersects a set of curves running in two
  directions and returns an ordered POINT GRID - one branch per row, one item per
  column - ready for paneling components.

  Addition: TOLERANCE. Curves that do not quite touch (a gap, a curve stopping
  short, slightly different heights) still give a grid node when they come
  within Tolerance of each other. The node is placed midway across the gap.

  Inputs
  ------
  Curves          (list) All grid curves, both directions. Split automatically
                         into two families by direction.
  Tolerance       (item) Max gap that still counts as an intersection.
                         0 / empty = model absolute tolerance (exact only).
  SwapDirections  (item) True = swap rows and columns.

  Outputs
  -------
  Grid        Tree {row} of points - row i = all nodes on row curve i, in column
              order. null where a row and column curve do not meet, so every
              branch has the same length and columns stay aligned.
  RowCurves   The row-direction curves, sorted across the grid.
  ColCurves   The column-direction curves, sorted across the grid.
  NodeType    Tree matching Grid: 0 = exact intersection, 1 = within tolerance
              (gap closed), -1 = no node.
*/

using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Geometry;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;

public class Script_Instance : GH_ScriptInstance
{
    private bool _metaSet = false;

    private void RunScript(
		List<Curve> Curves,
		double Tolerance,
		bool SwapDirections,
		ref object Grid,
		ref object RowCurves,
		ref object ColCurves,
		ref object NodeType)
    {
        SetMetadata();
        Component.Message = "CrvIntGrid v1.0";

        // ---- Inputs -------------------------------------------------------
        List<Curve> crvs = new List<Curve>();
        int bad = 0;
        if (Curves != null)
            foreach (Curve c in Curves)
                if (c != null && c.IsValid) crvs.Add(c); else bad++;

        if (crvs.Count < 2) { Warn("Connect at least two curves (one in each direction)."); return; }
        if (bad > 0) Remark(bad + " null/invalid curve(s) ignored.");

        double modelTol = RhinoDocument != null ? RhinoDocument.ModelAbsoluteTolerance : 0.001;
        double tol = (Tolerance > 0 && !double.IsNaN(Tolerance)) ? Tolerance : modelTol;
        if (tol < modelTol) tol = modelTol;

        // ---- Split into two families by direction ----------------------
        List<Curve> famA, famB;
        Vector3d dirA, dirB;
        if (!SplitFamilies(crvs, out famA, out famB, out dirA, out dirB))
        {
            Warn("All curves run in roughly the same direction - need curves in two directions.");
            return;
        }

        List<Curve> rows = SwapDirections ? famB : famA;
        List<Curve> cols = SwapDirections ? famA : famB;
        Vector3d rowDir = SwapDirections ? dirB : dirA;
        Vector3d colDir = SwapDirections ? dirA : dirB;

        // Rows are stacked across rowDir (along the column direction) and vice versa
        rows = SortAcross(rows, rowDir, colDir);
        cols = SortAcross(cols, colDir, rowDir);

        // ---- Nodes --------------------------------------------------------
        var grid = new DataTree<object>();
        var types = new DataTree<int>();
        int exact = 0, near = 0, missing = 0, multi = 0;

        for (int i = 0; i < rows.Count; i++)
        {
            GH_Path path = new GH_Path(i);
            for (int j = 0; j < cols.Count; j++)
            {
                Point3d node;
                bool several;
                int type = FindNode(rows[i], cols[j], tol, modelTol, out node, out several);
                if (several) multi++;

                if (type < 0) { grid.Add(null, path); missing++; }
                else { grid.Add(node, path); if (type == 0) exact++; else near++; }
                types.Add(type, path);
            }
        }

        if (near > 0)
            Remark(near + " node(s) closed a gap within tolerance " + tol + ".");
        if (missing > 0)
            Warn(missing + " row/column pair(s) do not meet within tolerance - null in Grid.");
        if (multi > 0)
            Remark(multi + " pair(s) cross more than once - the crossing nearest the row start is used.");

        Grid = grid;
        RowCurves = rows;
        ColCurves = cols;
        NodeType = types;

        Component.Message = rows.Count + " x " + cols.Count +
            (near > 0 ? "\n" + near + " near" : "") +
            (missing > 0 ? "\n" + missing + " missing" : "");
    }

    // ----------------------------------------------------------------- node

    // 0 = exact intersection, 1 = closest points within tolerance, -1 = none.
    private int FindNode(Curve a, Curve b, double tol, double modelTol, out Point3d node, out bool several)
    {
        node = Point3d.Unset;
        several = false;

        // 1. Exact intersection
        var events = Rhino.Geometry.Intersect.Intersection.CurveCurve(a, b, modelTol, modelTol);
        if (events != null && events.Count > 0)
        {
            several = events.Count > 1;
            double bestT = double.MaxValue;
            foreach (var ev in events)
            {
                double t = ev.IsOverlap ? ev.OverlapA.Mid : ev.ParameterA;
                if (t < bestT)
                {
                    bestT = t;
                    node = ev.IsOverlap ? a.PointAt(ev.OverlapA.Mid) : ev.PointA;
                }
            }
            return 0;
        }

        // 2. Near miss: closest points between the two curves
        Point3d pa, pb;
        if (a.ClosestPoints(b, out pa, out pb) && pa.IsValid && pb.IsValid)
        {
            if (pa.DistanceTo(pb) <= tol)
            {
                node = new Point3d((pa.X + pb.X) * 0.5, (pa.Y + pb.Y) * 0.5, (pa.Z + pb.Z) * 0.5);
                return 1;
            }
        }
        return -1;
    }

    // ---------------------------------------------------------- families

    private static Vector3d Chord(Curve c)
    {
        Vector3d d = c.PointAtEnd - c.PointAtStart;
        if (d.Length < RhinoMath.SqrtEpsilon) d = c.TangentAtStart;   // closed curve
        d.Unitize();
        return d;
    }

    // Reference = chord of the longest curve. Within 45 deg of it -> family A.
    private bool SplitFamilies(List<Curve> crvs, out List<Curve> a, out List<Curve> b,
        out Vector3d dirA, out Vector3d dirB)
    {
        a = new List<Curve>(); b = new List<Curve>();

        Curve longest = crvs[0];
        foreach (Curve c in crvs) if (c.GetLength() > longest.GetLength()) longest = c;
        Vector3d reference = Chord(longest);

        dirA = Vector3d.Zero; dirB = Vector3d.Zero;
        Vector3d refB = Vector3d.Unset;
        double cos45 = Math.Cos(Math.PI / 4.0);

        foreach (Curve c in crvs)
        {
            Vector3d d = Chord(c);
            double dot = d * reference;
            if (Math.Abs(dot) >= cos45)
            {
                a.Add(c);
                dirA += (dot < 0) ? -d : d;               // align signs before averaging
            }
            else
            {
                b.Add(c);
                if (!refB.IsValid) refB = d;
                dirB += (d * refB < 0) ? -d : d;
            }
        }

        if (a.Count == 0 || b.Count == 0) return false;
        dirA.Unitize();
        dirB.Unitize();
        return true;
    }

    // Order curves of one family across the grid: by their midpoint measured
    // along 'acrossDir' with the family's own direction removed.
    private static List<Curve> SortAcross(List<Curve> fam, Vector3d ownDir, Vector3d acrossDir)
    {
        Vector3d axis = acrossDir - (acrossDir * ownDir) * ownDir;
        if (!axis.Unitize()) axis = acrossDir;

        var keyed = new List<KeyValuePair<double, Curve>>();
        foreach (Curve c in fam)
        {
            Point3d mid = c.PointAtNormalizedLength(0.5);
            keyed.Add(new KeyValuePair<double, Curve>(new Vector3d(mid) * axis, c));
        }
        keyed.Sort((x, y) => x.Key.CompareTo(y.Key));

        var sorted = new List<Curve>();
        foreach (var kv in keyed) sorted.Add(kv.Value);
        return sorted;
    }

    // ------------------------------------------------------------ helpers

    private void Warn(string msg) { Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, msg); }
    private void Remark(string msg) { Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, msg); }

    private void SetMetadata()
    {
        if (_metaSet) return;
        _metaSet = true;

        Component.Name = "Curve Intersect Grid";
        Component.NickName = "CrvIntGrid";
        Component.Description =
            "Intersects curves running in two directions into an ordered point grid (one branch per row), " +
            "like PanelingTools ptIntersect - plus a Tolerance so near-miss curves still make nodes. Rajeev Pulari, v1.0.";

        var ins = Component.Params.Input;
        SetTip(ins, "Curves", "All grid curves, both directions (list). Split automatically by direction.");
        SetTip(ins, "Tolerance", "Max gap that still counts as an intersection. 0 = model tolerance (exact only).");
        SetTip(ins, "SwapDirections", "True = swap rows and columns.");

        var outs = Component.Params.Output;
        SetTip(outs, "Grid", "Point grid: branch {row}, items in column order. null where curves do not meet.");
        SetTip(outs, "RowCurves", "Row-direction curves, sorted across the grid.");
        SetTip(outs, "ColCurves", "Column-direction curves, sorted across the grid.");
        SetTip(outs, "NodeType", "Per node: 0 = exact, 1 = gap closed within tolerance, -1 = no node.");
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
