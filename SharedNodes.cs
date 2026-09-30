/*
  Author: Rajeev Pulari + Gemini
  Rhino 8 | Grasshopper C#
  Version: 2026.09.30
  Component: Shared Nodes v2.2
  Description: For every node (point), collects the curves whose start or end
               touches it (within Tolerance). One tree branch per node, aligned
               with the Points input list.
*/

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
    // Full-word names (were curves/points and lines/lin_index/pts/pt_index/unique_pts)
    private void RunScript(
        List<Curve> Curves,
        List<Point3d> Points,
        double Tolerance,
        ref object NodeCurves,
        ref object CurveIndices,
        ref object NodePoints,
        ref object NodeIndices,
        ref object UniqueNodes)
    {
        // Metadata + pin tooltips (once)
        if (this.Component != null && this.Component.Name != "Group Curves Shared Nodes")
        {
            this.Component.Name = "Group Curves Shared Nodes";
            this.Component.NickName = "sharedNodes";
            this.Component.Message = "Shared Nodes v2.2";
            this.Component.Description = "Groups curves by the nodes (points) they start or end at. One tree branch per node.";

            SetTip(this.Component.Params.Input, 0, "Curves", "Curves (e.g. frame members) to group by their end points.");
            SetTip(this.Component.Params.Input, 1, "Points", "Nodes to test. Branch i of every output belongs to Points[i].");
            SetTip(this.Component.Params.Input, 2, "Tolerance", "A curve end within this distance of a node counts as touching it. Zero or less uses the model tolerance. Item access.");
            SetTip(this.Component.Params.Output, 0, "NodeCurves", "Curves touching each node (one branch per node).");
            SetTip(this.Component.Params.Output, 1, "CurveIndices", "Index in Curves of each curve found at the node.");
            SetTip(this.Component.Params.Output, 2, "NodePoints", "The node point repeated once per touching curve.");
            SetTip(this.Component.Params.Output, 3, "NodeIndices", "Index in Points of the node, repeated once per touching curve.");
            SetTip(this.Component.Params.Output, 4, "UniqueNodes", "One point per node that has at least one curve.");
        }

        if (Points == null || Points.Count == 0)
        {
            this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No Points supplied.");
            return;
        }
        if (Curves == null || Curves.Count == 0)
        {
            this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No Curves supplied.");
            return;
        }

        // GH_Structure with explicit GH types: the classic, reliable way to return trees
        // (a DataTree<Curve> can arrive empty because Curve is an abstract type)
        var treeCurves = new GH_Structure<GH_Curve>();
        var treeCurveIdx = new GH_Structure<GH_Integer>();
        var treePts = new GH_Structure<GH_Point>();
        var treePtIdx = new GH_Structure<GH_Integer>();
        var treeUnique = new GH_Structure<GH_Point>();

        // Tolerance: input, else model tolerance, else 0.001
        double tol = Tolerance;
        if (tol <= 0)
            tol = (RhinoDoc.ActiveDoc != null) ? RhinoDoc.ActiveDoc.ModelAbsoluteTolerance : 0.001;
        if (tol <= 0) tol = 0.001;
        if (this.Component != null)
            this.Component.Message = "Shared Nodes v2.2 | tol " + tol.ToString("0.######");

        // Spatial hash: cell size = tol, so a match is always in the 3x3x3 neighbouring cells
        var grid = new Dictionary<Tuple<long, long, long>, List<int>>();
        for (int i = 0; i < Points.Count; i++)
        {
            var cell = CellOf(Points[i], tol);
            List<int> bucket;
            if (!grid.TryGetValue(cell, out bucket))
            {
                bucket = new List<int>();
                grid[cell] = bucket;
            }
            bucket.Add(i);
        }

        int count = Points.Count;
        var groupedCurves = new List<Curve>[count];
        var groupedIndex = new List<int>[count];
        var groupedPts = new List<Point3d>[count];
        for (int i = 0; i < count; i++)
        {
            groupedCurves[i] = new List<Curve>();
            groupedIndex[i] = new List<int>();
            groupedPts[i] = new List<Point3d>();
        }

        for (int i = 0; i < Curves.Count; i++)
        {
            Curve crv = Curves[i];
            if (crv == null) continue;

            Point3d start = crv.PointAtStart;
            Point3d end = crv.PointAtEnd;

            int startIdx = FindNode(grid, Points, start, tol);
            int endIdx = FindNode(grid, Points, end, tol);

            if (startIdx >= 0)
            {
                groupedCurves[startIdx].Add(crv);
                groupedIndex[startIdx].Add(i);
                groupedPts[startIdx].Add(start);
            }
            // A curve whose two ends land on the same node is counted once
            if (endIdx >= 0 && endIdx != startIdx)
            {
                groupedCurves[endIdx].Add(crv);
                groupedIndex[endIdx].Add(i);
                groupedPts[endIdx].Add(end);
            }
        }

        int matchedNodes = 0;
        int matchedEnds = 0;
        for (int i = 0; i < count; i++)
        {
            GH_Path path = new GH_Path(i);
            treeCurves.EnsurePath(path);
            treeCurveIdx.EnsurePath(path);
            treePts.EnsurePath(path);
            treePtIdx.EnsurePath(path);
            treeUnique.EnsurePath(path);

            for (int k = 0; k < groupedCurves[i].Count; k++)
            {
                treeCurves.Append(new GH_Curve(groupedCurves[i][k].DuplicateCurve()), path);
                treeCurveIdx.Append(new GH_Integer(groupedIndex[i][k]), path);
                treePts.Append(new GH_Point(groupedPts[i][k]), path);
                treePtIdx.Append(new GH_Integer(i), path);
            }
            matchedEnds += groupedCurves[i].Count;

            // every point in a branch is the same node -> one unique point
            if (groupedPts[i].Count > 0)
            {
                treeUnique.Append(new GH_Point(Points[i]), path);
                matchedNodes++;
            }
        }

        if (matchedEnds == 0)
            this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                "No curve end is within " + tol.ToString("0.######") + " of any point. Check that Points are the curve end points, or raise Tolerance.");
        else
            this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                matchedNodes + " of " + count + " nodes have curves; " + matchedEnds + " curve ends matched (of " + (Curves.Count * 2) + ").");

        NodeCurves = treeCurves;
        CurveIndices = treeCurveIdx;
        NodePoints = treePts;
        NodeIndices = treePtIdx;
        UniqueNodes = treeUnique;
    }

    private Tuple<long, long, long> CellOf(Point3d pt, double tol)
    {
        return new Tuple<long, long, long>(
            (long)Math.Floor(pt.X / tol),
            (long)Math.Floor(pt.Y / tol),
            (long)Math.Floor(pt.Z / tol));
    }

    // Index of the closest node within tol of pt, or -1 (ties: lowest index)
    private int FindNode(Dictionary<Tuple<long, long, long>, List<int>> grid, List<Point3d> nodes, Point3d pt, double tol)
    {
        var c = CellOf(pt, tol);
        int best = -1;
        double bestDist = double.MaxValue;
        for (long dx = -1; dx <= 1; dx++)
            for (long dy = -1; dy <= 1; dy++)
                for (long dz = -1; dz <= 1; dz++)
                {
                    List<int> bucket;
                    if (!grid.TryGetValue(new Tuple<long, long, long>(c.Item1 + dx, c.Item2 + dy, c.Item3 + dz), out bucket))
                        continue;
                    foreach (int idx in bucket)
                    {
                        double d = nodes[idx].DistanceTo(pt);
                        if (d <= tol && (d < bestDist || (d == bestDist && idx < best)))
                        {
                            bestDist = d;
                            best = idx;
                        }
                    }
                }
        return best;
    }

    private void SetTip(IList<IGH_Param> ps, int i, string name, string tip)
    {
        if (ps == null || i < 0 || i >= ps.Count) return;
        ps[i].NickName = name;
        ps[i].Description = tip;
    }
}
