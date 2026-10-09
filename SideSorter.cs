/*
  Author: Rajeev Pulari + Gemini
  Rhino 8 | Grasshopper C#
  Version: 2025.11.03
  Component: Side Sorter
  Purpose: Separate points into "Left" and "Right" sides of a curve.
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
		Curve Crv,
		Plane Pln,
		ref object LeftPts,
		ref object RightPts,
		ref object LeftIdx,
		ref object RightIdx)
  {
    // --- Metadata + pin tooltips (once) ---
    if (this.Component != null && this.Component.Name != "Side Sorter")
    {
      this.Component.Name = "Side Sorter";
      this.Component.NickName = "SideSort";
      this.Component.Message = "Side Sorter v2.0";
      this.Component.Description = "Sorts points into Left and Right of a curve, judged in a plane.";

      SetTip(this.Component.Params.Input, 0, "Points",
        "The points to sort.");
      SetTip(this.Component.Params.Input, 1, "Crv",
        "The dividing curve. 'Left'/'Right' follow the curve's direction.");
      SetTip(this.Component.Params.Input, 2, "Pln",
        "Plane whose normal defines 'up' for the left/right test. Invalid = World XY.");
      SetTip(this.Component.Params.Output, 0, "LeftPts",
        "Points on the left of the curve.");
      SetTip(this.Component.Params.Output, 1, "RightPts",
        "Points on the right of the curve (points exactly on the curve go here).");
      SetTip(this.Component.Params.Output, 2, "LeftIdx",
        "Original indices of the left points.");
      SetTip(this.Component.Params.Output, 3, "RightIdx",
        "Original indices of the right points.");
    }

    // --- Safety checks ---
    if (Points == null || Points.Count == 0 || Crv == null || !Crv.IsValid)
    {
      LeftPts = new List<Point3d>();
      RightPts = new List<Point3d>();
      LeftIdx = new List<int>();
      RightIdx = new List<int>();
      return;
    }

    // Default to WorldXY if plane is invalid
    Plane plane = Pln.IsValid ? Pln : Plane.WorldXY;

    // --- Initialize outputs ---
    List<Point3d> leftPts = new List<Point3d>();
    List<Point3d> rightPts = new List<Point3d>();
    List<int> leftIdx = new List<int>();
    List<int> rightIdx = new List<int>();

    // --- Iterate through each point ---
    for (int i = 0; i < Points.Count; i++)
    {
      Point3d pt = Points[i];

      double t;
      if (!Crv.ClosestPoint(pt, out t)) continue;

      Point3d crvPt = Crv.PointAt(t);
      Vector3d vec = pt - crvPt;

      // Cross product of curve tangent and test vector
      Vector3d tangent = Crv.TangentAt(t);
      Vector3d cross = Vector3d.CrossProduct(tangent, vec);

      // Dot with plane normal to determine which side
      // side > 0 is "Left" in a right-handed coordinate system.
      // side == 0 (point on the curve, or straight above/below it out of the
      // plane) falls to Right.
      double side = Vector3d.Multiply(cross, plane.Normal);

      if (side > 0)
      {
        leftPts.Add(pt);
        leftIdx.Add(i);
      }
      else
      {
        rightPts.Add(pt);
        rightIdx.Add(i);
      }
    }

    // --- Outputs ---
    LeftPts = leftPts;
    RightPts = rightPts;
    LeftIdx = leftIdx;
    RightIdx = rightIdx;
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
