/*
  Author: Rajeev Pulari + Gemini
  Rhino 8 | Grasshopper C#
  Version: 2025.11.04
  Component: Radial Sort CCW
  Purpose: Sort a list of points by angle around a plane, counter-clockwise.
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
  private void RunScript(List<Point3d> Pts, Plane Pln, ref object SortedPts)
  {
    // --- Metadata + pin tooltips (once) ---
    if (this.Component != null && this.Component.Name != "Radial Sort CCW")
    {
      this.Component.Name = "Radial Sort CCW";
      this.Component.NickName = "RadSort";
      this.Component.Message = "Radial Sort CCW v2.0";
      this.Component.Description = "Sorts points by angle around a plane, counter-clockwise.";

      SetTip(this.Component.Params.Input, 0, "Pts",
        "The points to sort.");
      SetTip(this.Component.Params.Input, 1, "Pln",
        "Plane to sort around. Its origin is the centre, its X axis is angle 0, and its normal sets the CCW direction. Invalid = World XY.");
      SetTip(this.Component.Params.Output, 0, "SortedPts",
        "The points ordered counter-clockwise, starting from the plane's X axis.");
    }

    // --- Safety ---
    if (Pts == null || Pts.Count == 0)
    {
      SortedPts = new List<Point3d>();
      return;
    }

    // Default to WorldXY if plane is invalid
    Plane plane = Pln.IsValid ? Pln : Plane.WorldXY;

    Point3d origin = plane.Origin;
    Vector3d xAxis = plane.XAxis;
    Vector3d yAxis = plane.YAxis;

    // --- Compute angle for each point ---
    // Tuple stores: <Angle, OriginalPoint>
    List<Tuple<double, Point3d>> anglePairs = new List<Tuple<double, Point3d>>();

    foreach (Point3d pt in Pts)
    {
      Vector3d vec = pt - origin;

      // Project onto the plane's axes
      double x = vec * xAxis;
      double y = vec * yAxis;

      // Angle 0..2PI, CCW from the plane X axis
      double angle = Math.Atan2(y, x);
      if (angle < 0) angle += 2 * Math.PI;

      anglePairs.Add(new Tuple<double, Point3d>(angle, pt));
    }

    // --- Sort by angle ---
    anglePairs.Sort((a, b) => a.Item1.CompareTo(b.Item1));

    // --- Collect sorted points ---
    List<Point3d> sorted = new List<Point3d>();
    foreach (var pair in anglePairs)
      sorted.Add(pair.Item2);

    SortedPts = sorted;
  }

  private void SetTip(System.Collections.Generic.IList<IGH_Param> ps, int i, string name, string tip)
  {
    if (ps == null || i < 0 || i >= ps.Count) return;
    ps[i].Name = name;
    ps[i].NickName = name;
    ps[i].Description = tip;
  }
}
