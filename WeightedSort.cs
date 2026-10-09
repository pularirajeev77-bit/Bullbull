/*
  Author: Rajeev Pulari + Gemini
  Rhino 8 | Grasshopper C#
  Version: 2025.11.03
  Component: Weighted Sort
  Purpose: Sort points by a weighted key built from their X, Y, Z.
*/

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
  private void RunScript(
		List<Point3d> Points,
		double X_Mult,
		double Y_Mult,
		double Z_Mult,
		ref object SortedPts,
		ref object Indices,
		ref object Keys)
  {
    // --- Metadata + pin tooltips (once) ---
    if (this.Component != null && this.Component.Name != "Weighted Sort")
    {
      this.Component.Name = "Weighted Sort";
      this.Component.NickName = "WeightSort";
      this.Component.Message = "Weighted Sort v2.0";
      this.Component.Description = "Sorts points by a weighted key: X_Mult*X^2 + Y_Mult*Y^2 + Z_Mult*Z^2.";

      SetTip(this.Component.Params.Input, 0, "Points",
        "The points to sort.");
      SetTip(this.Component.Params.Input, 1, "X_Mult",
        "Weight on X. The key uses X_Mult * X squared (X's sign is lost).");
      SetTip(this.Component.Params.Input, 2, "Y_Mult",
        "Weight on Y. The key uses Y_Mult * Y squared.");
      SetTip(this.Component.Params.Input, 3, "Z_Mult",
        "Weight on Z. The key uses Z_Mult * Z squared.");
      SetTip(this.Component.Params.Output, 0, "SortedPts",
        "The points ordered by their key, smallest first.");
      SetTip(this.Component.Params.Output, 1, "Indices",
        "Original index of each sorted point.");
      SetTip(this.Component.Params.Output, 2, "Keys",
        "The sort key value of each point, in sorted order.");
    }

    // --- Safety check ---
    if (Points == null || Points.Count == 0)
    {
      SortedPts = new List<Point3d>();
      Indices = new List<int>();
      Keys = new List<double>();
      return;
    }

    // --- Create indexed list with computed sort keys ---
    // Tuple stores: <SortKey, OriginalIndex, Point3d>
    List<Tuple<double, int, Point3d>> indexedPts = new List<Tuple<double, int, Point3d>>();

    for (int i = 0; i < Points.Count; i++)
    {
      Point3d pt = Points[i];

      double u = pt.X * X_Mult;
      double v = pt.Y * Y_Mult;
      double w = pt.Z * Z_Mult;

      // Sort key: X_Mult*X^2 + Y_Mult*Y^2 + Z_Mult*Z^2. Note each term is
      // squared, so a coordinate's SIGN does not affect the key (x = -5 and
      // x = 5 weigh the same). For a plain signed sort along an axis, this is
      // not what you want - use a Sort with a value from a dot product instead.
      double sortKey = u * pt.X + v * pt.Y + w * pt.Z;

      indexedPts.Add(new Tuple<double, int, Point3d>(sortKey, i, pt));
    }

    // --- Sort by the computed key ---
    indexedPts.Sort((a, b) => a.Item1.CompareTo(b.Item1));

    // --- Extract sorted results ---
    List<Point3d> sortedPts = new List<Point3d>();
    List<int> sortedIdx = new List<int>();
    List<double> sortedKeys = new List<double>();

    foreach (var item in indexedPts)
    {
      sortedKeys.Add(item.Item1);
      sortedIdx.Add(item.Item2);
      sortedPts.Add(item.Item3);
    }

    // --- Assign Outputs ---
    SortedPts = sortedPts;
    Indices = sortedIdx;
    Keys = sortedKeys;
  }

  private void SetTip(System.Collections.Generic.IList<IGH_Param> ps, int i, string name, string tip)
  {
    // Pins are matched by NAME (the script variable), not by position 'i':
    // Rhino 8 script components can have an extra "out" pin first, which
    // shifted position-based tooltips onto the wrong pins. Name is never
    // changed (it is the script variable) - only NickName and Description.
    if (ps == null) return;
    IGH_Param hit = null;
    foreach (IGH_Param p in ps)
      if (string.Equals(p.Name, name, System.StringComparison.OrdinalIgnoreCase)) { hit = p; break; }
    if (hit == null)
      foreach (IGH_Param p in ps)
        if (string.Equals(p.NickName, name, System.StringComparison.OrdinalIgnoreCase)) { hit = p; break; }
    if (hit == null) return;
    hit.NickName = name;
    hit.Description = tip;
  }
}
