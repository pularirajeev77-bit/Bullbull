/*
  Author: Rajeev Pulari + Gemini
  Rhino 8 | Grasshopper C#
  Version: 2025.11.03
  Component: Point On Curve
  Purpose: Check which points lie on the curve(s) of each branch (tree-safe).
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
		DataTree<Curve> Crv,
		List<Point3d> Pts,
		double Tol,
		ref object OnCrv,
		ref object OnIdx,
		ref object OffIdx)
  {
    // --- Metadata + pin tooltips (once) ---
    // Original swapped these: NickName held the full label and Message had a
    // stray leading space. Fixed to a short nickname + clean message.
    if (this.Component != null && this.Component.Name != "Point On Curve")
    {
      this.Component.Name = "Point On Curve";
      this.Component.NickName = "PtOnCrv";
      this.Component.Message = "Point On Curve v2.0";
      this.Component.Description = "For each branch of curves, tests which of the points lie on them, within a tolerance.";

      SetTip(this.Component.Params.Input, 0, "Crv",
        "Curves to test against (tree). Each branch is tested separately.");
      SetTip(this.Component.Params.Input, 1, "Pts",
        "The points to test (the same list is tested against every branch).");
      SetTip(this.Component.Params.Input, 2, "Tol",
        "A point counts as on the curve if it is within this distance. 0 or less uses the model tolerance.");
      SetTip(this.Component.Params.Output, 0, "OnCrv",
        "Per branch: True/False for each point - is it on any curve in that branch?");
      SetTip(this.Component.Params.Output, 1, "OnIdx",
        "Per branch: indices of the points that ARE on a curve.");
      SetTip(this.Component.Params.Output, 2, "OffIdx",
        "Per branch: indices of the points that are NOT on any curve.");
    }

    // --- Safety defaults ---
    // Fall back to the model tolerance (was a fixed 0.001, wrong for m files
    // or very fine mm work).
    if (Tol <= 0)
      Tol = (RhinoDoc.ActiveDoc != null) ? RhinoDoc.ActiveDoc.ModelAbsoluteTolerance : 0.001;

    if (Crv == null || Crv.BranchCount == 0 || Pts == null || Pts.Count == 0)
    {
      OnCrv = new DataTree<bool>();
      OnIdx = new DataTree<int>();
      OffIdx = new DataTree<int>();
      return;
    }

    // --- Prepare output DataTrees ---
    DataTree<bool> onTree = new DataTree<bool>();
    DataTree<int> posTree = new DataTree<int>();
    DataTree<int> negTree = new DataTree<int>();

    // --- Loop through each branch of curves ---
    for (int bi = 0; bi < Crv.BranchCount; bi++)
    {
      GH_Path path = Crv.Path(bi);
      List<Curve> crvBranch = Crv.Branch(bi);

      // Ensure the branch exists in every output, even if empty
      onTree.EnsurePath(path);
      posTree.EnsurePath(path);
      negTree.EnsurePath(path);

      List<bool> localOn  = new List<bool>();
      List<int>  localPos = new List<int>();
      List<int>  localNeg = new List<int>();

      for (int i = 0; i < Pts.Count; i++)
      {
        Point3d pt = Pts[i];
        bool found = false;

        foreach (Curve c in crvBranch)
        {
          if (c == null || !c.IsValid) continue;

          double t;
          if (c.ClosestPoint(pt, out t))
          {
            Point3d testPt = c.PointAt(t);
            if (pt.DistanceTo(testPt) <= Tol)
            {
              found = true;
              break; // on one curve in the branch is enough
            }
          }
        }

        localOn.Add(found);
        if (found) localPos.Add(i);
        else       localNeg.Add(i);
      }

      onTree.AddRange(localOn, path);
      posTree.AddRange(localPos, path);
      negTree.AddRange(localNeg, path);
    }

    // --- Outputs ---
    OnCrv  = onTree;
    OnIdx  = posTree;
    OffIdx = negTree;
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
