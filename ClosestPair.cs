/*
  Author: Rajeev Pulari + Gemini
  Rhino 8 | Grasshopper C#
  Version: 2025.11.03
  Component: Closest Pair
  Purpose: Find the two points in a list that are closest together.
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
		ref object PtA,
		ref object PtB,
		ref object MinDist)
  {
    // --- Metadata + pin tooltips (once) ---
    if (this.Component != null && this.Component.Name != "Closest Pair")
    {
      this.Component.Name = "Closest Pair";
      this.Component.NickName = "ClosePts";
      this.Component.Message = "Closest Pair v2.0";
      this.Component.Description = "Finds the two points in a list that are closest together.";

      SetTip(this.Component.Params.Input, 0, "Points",
        "The points to search (need at least 2).");
      SetTip(this.Component.Params.Output, 0, "PtA",
        "One of the two closest points.");
      SetTip(this.Component.Params.Output, 1, "PtB",
        "The other of the two closest points.");
      SetTip(this.Component.Params.Output, 2, "MinDist",
        "Distance between PtA and PtB.");
    }

    // --- Safety: handle empty or insufficient input ---
    if (Points == null || Points.Count < 2)
    {
      PtA = Point3d.Unset;
      PtB = Point3d.Unset;
      MinDist = 0.0;
      return;
    }

    // --- Initialize ---
    double minDist = double.MaxValue;
    Point3d pointA = Point3d.Unset;
    Point3d pointB = Point3d.Unset;

    // --- Double loop to find closest pair ---
    // Compares every pair (O(n^2)); fine for typical lists, but a few
    // thousand points and up will get slow.
    for (int i = 0; i < Points.Count; i++)
    {
      for (int j = i + 1; j < Points.Count; j++)
      {
        double dist = Points[i].DistanceTo(Points[j]);
        if (dist < minDist)
        {
          minDist = dist;
          pointA = Points[i];
          pointB = Points[j];
        }
      }
    }

    // --- Outputs ---
    PtA = pointA;
    PtB = pointB;
    MinDist = minDist;
  }

  private void SetTip(System.Collections.Generic.IList<IGH_Param> ps, int i, string name, string tip)
  {
    if (ps == null || i < 0 || i >= ps.Count) return;
    ps[i].Name = name;
    ps[i].NickName = name;
    ps[i].Description = tip;
  }
}
