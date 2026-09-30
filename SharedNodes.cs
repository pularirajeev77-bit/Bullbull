/*
  Author: Rajeev Pulari + Gemini
  Rhino 8 | Grasshopper C#
  Version: 2026.09.30
  Component: Shared Nodes v2.1
  Description: For every node (point), collects the curves whose start or end
               touches it (6-decimal match). One tree branch per node, aligned
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
            this.Component.Message = "Shared Nodes v2.1";
            this.Component.Description = "Groups curves by the nodes (points) they start or end at. One tree branch per node.";

            SetTip(this.Component.Params.Input, 0, "Curves", "Curves (e.g. frame members) to group by their end points.");
            SetTip(this.Component.Params.Input, 1, "Points", "Nodes to test. Branch i of every output belongs to Points[i].");
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

        DataTree<Curve> treeCurves = new DataTree<Curve>();
        DataTree<int> treeCurveIdx = new DataTree<int>();
        DataTree<Point3d> treePts = new DataTree<Point3d>();
        DataTree<int> treePtIdx = new DataTree<int>();
        DataTree<Point3d> treeUnique = new DataTree<Point3d>();

        // Point -> index lookup (first occurrence wins for duplicate points)
        var pointMap = new Dictionary<Tuple<double, double, double>, int>();
        for (int i = 0; i < Points.Count; i++)
        {
            var key = PointKey(Points[i]);
            if (!pointMap.ContainsKey(key)) pointMap[key] = i;
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
            var startKey = PointKey(start);
            var endKey = PointKey(end);

            int idx;
            if (pointMap.TryGetValue(startKey, out idx))
            {
                groupedCurves[idx].Add(crv);
                groupedIndex[idx].Add(i);
                groupedPts[idx].Add(start);
            }
            // A closed curve starts and ends on the same node: count it once
            if (!endKey.Equals(startKey) && pointMap.TryGetValue(endKey, out idx))
            {
                groupedCurves[idx].Add(crv);
                groupedIndex[idx].Add(i);
                groupedPts[idx].Add(end);
            }
        }

        for (int i = 0; i < count; i++)
        {
            GH_Path path = new GH_Path(i);
            treeCurves.EnsurePath(path);
            treeCurveIdx.EnsurePath(path);
            treePts.EnsurePath(path);
            treePtIdx.EnsurePath(path);
            treeUnique.EnsurePath(path);

            treeCurves.AddRange(groupedCurves[i], path);
            treeCurveIdx.AddRange(groupedIndex[i], path);
            treePts.AddRange(groupedPts[i], path);
            for (int k = 0; k < groupedPts[i].Count; k++) treePtIdx.Add(i, path);

            // every point in a branch is the same node -> one unique point
            if (groupedPts[i].Count > 0) treeUnique.Add(groupedPts[i][0], path);
        }

        NodeCurves = treeCurves;
        CurveIndices = treeCurveIdx;
        NodePoints = treePts;
        NodeIndices = treePtIdx;
        UniqueNodes = treeUnique;
    }

    // Point key (rounded to 6 decimals; +0 avoids a separate "-0" key)
    private Tuple<double, double, double> PointKey(Point3d pt)
    {
        return new Tuple<double, double, double>(
            Math.Round(pt.X, 6) + 0.0,
            Math.Round(pt.Y, 6) + 0.0,
            Math.Round(pt.Z, 6) + 0.0);
    }

    private void SetTip(IList<IGH_Param> ps, int i, string name, string tip)
    {
        if (ps == null || i < 0 || i >= ps.Count) return;
        ps[i].NickName = name;
        ps[i].Description = tip;
    }
}
