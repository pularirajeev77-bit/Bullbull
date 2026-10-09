/*
  Author: Rajeev Pulari + Gemini
  Rhino 8 | Grasshopper C#
  Version: 2026.04.06
  Component: SharedNodes v2.0
  Description: Groups curves based on shared nodes/points with 6-decimal tolerance.
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
    private void RunScript(
		List<Curve> curves,
		List<Point3d> points,
		ref object lines,
		ref object lin_index,
		ref object pts,
		ref object pt_index,
		ref object unique_pts)
    {
    SetPinTips();   // pin tooltips (set once, matched by name)

        // --------------------------------------------------------------
        // 0️⃣ Set Component Metadata (Rhino 8 Native)
        // --------------------------------------------------------------
        if (this.Component != null)
        {
            this.Component.Name = "Group Curves Shared Nodes";
            this.Component.NickName = "sharedNodes";
            this.Component.Message = "Shared Nodes v2.0";
        }

        // 1. Initialize Outputs as DataTrees
        DataTree<Curve> treeLines = new DataTree<Curve>();
        DataTree<int> treeLinIndex = new DataTree<int>();
        DataTree<Point3d> treePts = new DataTree<Point3d>();
        DataTree<int> treePtIndex = new DataTree<int>();
        DataTree<Point3d> treeUniquePts = new DataTree<Point3d>();

        // 2. Setup Point Mapping (Tolerance-based key using a Tuple)
        var point_map = new Dictionary<Tuple<double, double, double>, int>();
        if (points != null)
        {
            for (int i = 0; i < points.Count; i++)
            {
                var key = PointKey(points[i]);
                if (!point_map.ContainsKey(key))
                {
                    point_map[key] = i;
                }
            }
        }

        // 3. Prepare list of lists for processing
        int count = points != null ? points.Count : 0;
        if (count == 0) return;

        List<Curve>[] groupedLines = new List<Curve>[count];
        List<int>[] groupedIndex = new List<int>[count];
        List<Point3d>[] groupedPts = new List<Point3d>[count];
        List<int>[] groupedPtIndex = new List<int>[count];

        for (int i = 0; i < count; i++)
        {
            groupedLines[i] = new List<Curve>();
            groupedIndex[i] = new List<int>();
            groupedPts[i] = new List<Point3d>();
            groupedPtIndex[i] = new List<int>();
        }

        // 4. Process Curves
        if (curves != null)
        {
            for (int i = 0; i < curves.Count; i++)
            {
                Curve crv = curves[i];
                if (crv == null) continue;

                Point3d[] ends = new Point3d[] { crv.PointAtStart, crv.PointAtEnd };

                foreach (Point3d pt in ends)
                {
                    var key = PointKey(pt);
                    if (point_map.ContainsKey(key))
                    {
                        int idx = point_map[key];
                        groupedLines[idx].Add(crv);
                        groupedIndex[idx].Add(i);
                        groupedPts[idx].Add(pt);
                        groupedPtIndex[idx].Add(idx);
                    }
                }
            }
        }

        // 5. Build Trees and Handle Unique Points per branch
        for (int i = 0; i < count; i++)
        {
            GH_Path path = new GH_Path(i);
            
            treeLines.AddRange(groupedLines[i], path);
            treeLinIndex.AddRange(groupedIndex[i], path);
            treePts.AddRange(groupedPts[i], path);
            treePtIndex.AddRange(groupedPtIndex[i], path);

            // Per-branch de-duplication logic
            HashSet<Tuple<double, double, double>> seenInBranch = new HashSet<Tuple<double, double, double>>();
            foreach (Point3d pt in groupedPts[i])
            {
                var k = PointKey(pt);
                if (!seenInBranch.Contains(k))
                {
                    seenInBranch.Add(k);
                    treeUniquePts.Add(pt, path);
                }
            }
        }

        // 6. Assign to Outputs
        lines = treeLines;
        lin_index = treeLinIndex;
        pts = treePts;
        pt_index = treePtIndex;
        unique_pts = treeUniquePts;
    }

    // Helper method for point key (Rounding to 6 decimals)
    private Tuple<double, double, double> PointKey(Point3d pt)
    {
        return new Tuple<double, double, double>(
            Math.Round(pt.X, 6),
            Math.Round(pt.Y, 6),
            Math.Round(pt.Z, 6)
            );
    }

  // ---------------------------------------------------------------- pin tooltips
  private bool _pinTipsSet = false;

  private void SetPinTips()
  {
    if (_pinTipsSet || Component == null) return;
    _pinTipsSet = true;
    TipPin(Component.Params.Input, "curves", "Curves (frame members) to group.");
    TipPin(Component.Params.Input, "points", "Node points.");
    TipPin(Component.Params.Output, "lines", "Curves touching each node.");
    TipPin(Component.Params.Output, "lin_index", "Index in curves of each of those curves.");
    TipPin(Component.Params.Output, "pts", "The matching curve end point, once per touching curve.");
    TipPin(Component.Params.Output, "pt_index", "Index in points of the node, once per touching curve.");
    TipPin(Component.Params.Output, "unique_pts", "The node point once per branch (duplicates removed)");
  }

  // Match pins by Name (the script variable), fall back to NickName.
  // Only NickName/Description are changed - never Name.
  private void TipPin(System.Collections.Generic.IList<Grasshopper.Kernel.IGH_Param> ps, string name, string tip)
  {
    if (ps == null) return;
    Grasshopper.Kernel.IGH_Param hit = null;
    foreach (Grasshopper.Kernel.IGH_Param p in ps)
      if (string.Equals(p.Name, name, System.StringComparison.OrdinalIgnoreCase)) { hit = p; break; }
    if (hit == null)
      foreach (Grasshopper.Kernel.IGH_Param p in ps)
        if (string.Equals(p.NickName, name, System.StringComparison.OrdinalIgnoreCase)) { hit = p; break; }
    if (hit == null) return;
    hit.NickName = name;
    hit.Description = tip;
  }
}
