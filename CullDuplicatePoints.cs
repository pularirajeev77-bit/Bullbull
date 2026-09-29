/*
  Author: Rajeev Pulari + Gemini
  Rhino 8 | Grasshopper C#
  Version: 2025.11.03
  Component: Cull Duplicate Points
  Purpose: Remove duplicate points from a list, within a distance tolerance.
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
  private void RunScript(List<Point3d> Points, double Tolerance, ref object UniquePts)
  {
    // --- Metadata + pin tooltips (once) ---
    if (this.Component != null && this.Component.Name != "Cull Duplicate Points")
    {
      this.Component.Name = "Cull Duplicate Points";
      this.Component.NickName = "CullDupPt";
      this.Component.Message = "Cull Duplicate Points";
      this.Component.Description = "Removes duplicate points from a list, within a distance tolerance.";

      SetTip(this.Component.Params.Input, 0, "Points",
        "The points to de-duplicate (list).");
      SetTip(this.Component.Params.Input, 1, "Tolerance",
        "Two points closer than this are treated as the same. 0 or less uses the model tolerance.");
      SetTip(this.Component.Params.Output, 0, "UniquePts",
        "The points with duplicates removed, first occurrence of each kept.");
    }

    // --- Safety: handle empty input ---
    if (Points == null || Points.Count == 0)
    {
      UniquePts = new List<Point3d>();
      return;
    }

    // Tolerance: fall back to the model tolerance when 0/negative
    double tol = (Tolerance > 0)
      ? Tolerance
      : (RhinoDoc.ActiveDoc != null ? RhinoDoc.ActiveDoc.ModelAbsoluteTolerance : 0.001);

    // Drop invalid points up front
    var valid = new List<Point3d>();
    foreach (Point3d pt in Points)
      if (pt.IsValid) valid.Add(pt);

    if (valid.Count == 0)
    {
      UniquePts = new List<Point3d>();
      return;
    }

    // True distance-based de-duplication. The original built a string key by
    // rounding each coordinate to 6 decimals, which is NOT a real tolerance
    // test: two points a hair apart but across a rounding boundary (e.g.
    // 0.12345649 and 0.12345651) round to different strings and were both
    // kept, while the tolerance was fixed at 1e-6 regardless of model units.
    // Point3d.CullDuplicates does a proper within-tolerance cull.
    Point3d[] culled = Point3d.CullDuplicates(valid, tol);

    UniquePts = (culled != null) ? new List<Point3d>(culled) : valid;
  }

  private void SetTip(System.Collections.Generic.IList<IGH_Param> ps, int i, string name, string tip)
  {
    if (ps == null || i < 0 || i >= ps.Count) return;
    ps[i].Name = name;
    ps[i].NickName = name;
    ps[i].Description = tip;
  }
}
