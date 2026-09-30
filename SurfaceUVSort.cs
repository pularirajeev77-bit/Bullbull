#region Metadata
/*
  Platform    : Rhino 8 | Grasshopper C#
  Component   : Surface Point UV Sorter
  NickName    : UVSort
  Message     : UV Sort v2.1
  Description : Sorts points by their UV parameters on a surface - row by row.
                Points whose primary parameter lies within Tolerance form one row;
                rows are sorted along the primary axis, points inside a row along
                the other axis. Returns the original input indices.

  Inputs:
    Points     : List<Point3d> (List) - Points to sort (on or near the surface)
    Surface    : Surface       (Item) - Target surface
    SortUFirst : bool          (Item) - True = rows by U (then V inside), False = rows by V (then U)
    Tolerance  : double        (Item) - Row grouping tolerance, in surface PARAMETER units (0 = 0.001)

  Outputs:
    SortedPoints    : List<Point3d> - Points in sorted order
    UVParams        : List<Point2d> - Their (u, v) on the surface
    OriginalIndices : List<int>     - Index of each sorted point in the input list
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

public class Script_Instance : GH_ScriptInstance
{
    // Points : list | Surface, SortUFirst, Tolerance : item
    private void RunScript(
        List<Point3d> Points,
        Surface Surface,
        bool SortUFirst,
        double Tolerance,
        ref object SortedPoints,
        ref object UVParams,
        ref object OriginalIndices)
    {
        // Metadata + pin tooltips (once)
        if (Component != null && Component.Name != "Surface Point UV Sorter")
        {
            Component.Name        = "Surface Point UV Sorter";
            Component.NickName    = "UVSort";
            Component.Message     = "UV Sort v2.1";
            Component.Description = "Sorts points by their UV parameters on a surface, row by row. "
                                  + "Toggle the primary axis between U and V. Returns original indices.";

            var pi = Component.Params.Input;
            SetTip(pi, "Points", "Points to sort (on or near the surface). List access.");
            SetTip(pi, "Surface", "Target surface.");
            SetTip(pi, "SortUFirst", "True = rows by U (then V inside a row). False = rows by V (then U).");
            SetTip(pi, "Tolerance", "Points whose primary parameter differs by less than this form one row. Surface parameter units. 0 = 0.001.");
            var po = Component.Params.Output;
            SetTip(po, "SortedPoints", "Points in sorted order.");
            SetTip(po, "UVParams", "(u, v) of each sorted point on the surface.");
            SetTip(po, "OriginalIndices", "Index of each sorted point in the input list.");
        }

        // 1. Validate
        if (Points == null || Points.Count == 0)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Provide Points.");
            return;
        }
        if (Surface == null)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Provide a Surface.");
            return;
        }
        double tol = (Tolerance > 0.0) ? Tolerance : 0.001;

        // 2. UV of every point
        var data = new List<Item>();
        int failed = 0;
        for (int i = 0; i < Points.Count; i++)
        {
            Point3d pt = Points[i];
            double u, v;
            if (pt.IsValid && Surface.ClosestPoint(pt, out u, out v))
                data.Add(new Item { Index = i, Pt = pt, U = u, V = v });
            else
                failed++;
        }
        if (failed > 0)
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                failed + " point(s) could not be projected to the surface and were left out.");

        // 3. Group into rows: sort on the primary parameter, then start a new row whenever
        //    the gap to the previous point exceeds the tolerance (no rounding-boundary splits)
        Func<Item, double> primary = SortUFirst ? (Func<Item, double>)(d => d.U) : (d => d.V);
        Func<Item, double> secondary = SortUFirst ? (Func<Item, double>)(d => d.V) : (d => d.U);

        var byPrimary = data.OrderBy(primary).ToList();
        var rows = new List<List<Item>>();
        foreach (var d in byPrimary)
        {
            if (rows.Count == 0 || primary(d) - primary(rows[rows.Count - 1][rows[rows.Count - 1].Count - 1]) > tol)
                rows.Add(new List<Item>());
            rows[rows.Count - 1].Add(d);
        }

        // 4. Inside each row sort along the other parameter
        var sortedPts = new List<Point3d>();
        var sortedUVs = new List<Point2d>();
        var sortedIdx = new List<int>();
        foreach (var row in rows)
            foreach (var d in row.OrderBy(secondary))
            {
                sortedPts.Add(d.Pt);
                sortedUVs.Add(new Point2d(d.U, d.V));
                sortedIdx.Add(d.Index);
            }

        SortedPoints    = sortedPts;
        UVParams        = sortedUVs;
        OriginalIndices = sortedIdx;
        Component.Message = "UV Sort v2.1 | " + rows.Count + " rows";
    }

    // <Custom additional code>
    private class Item
    {
        public int Index;
        public Point3d Pt;
        public double U, V;
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
