/*
  Author: Rajeev Pulari + Gemini
  Rhino 8 | Grasshopper C#
  Version: 2025.11.23 (v2.3)
  Component: Point On Curve
  Purpose: For each branch, tell which CURVES have a point on them (tree-safe).

  Pairing curve branches with point branches:
    - If every Crv path also exists in Pts, branches are paired by PATH
      (safe after Sort / Flip / reorder).
    - Otherwise by ORDER: Crv branch i with Pts branch i.
      One Pts branch only = used for every curve branch.
  Outputs are per CURVE, so they line up with the Crv tree:
    OnCrv  = True/False for each curve
    OnIdx  = indices of the curves that have a point on them
    OffIdx = indices of the curves that have no point on them
  Invalid / empty points are skipped (with a warning).
  Tested on 18 cases (2025.11.23).
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
		DataTree<Point3d> Pts,
		double Tol,
		ref object OnCrv,
		ref object OnIdx,
		ref object OffIdx)
  {
    // --- Metadata + pin tooltips (once) ---
    if (this.Component != null && this.Component.Message != "Point On Curve v2.3")
    {
      this.Component.Name = "Point On Curve";
      this.Component.NickName = "PtOnCrv";
      this.Component.Message = "Point On Curve v2.3";
      this.Component.Description = "For each branch, tells which curves have one of the branch's points on them, within a tolerance.";

      SetTip(this.Component.Params.Input, 0, "Crv",
        "Curves to test (tree). Each branch is tested separately.");
      SetTip(this.Component.Params.Input, 1, "Pts",
        "Points (tree). Paired with Crv by matching path, else by branch order. One branch only = used for every curve branch.");
      SetTip(this.Component.Params.Input, 2, "Tol",
        "A point counts as on the curve if it is within this distance. 0 or less uses the model tolerance.");
      SetTip(this.Component.Params.Output, 0, "OnCrv",
        "Per branch: True/False for each curve - does any point lie on it?");
      SetTip(this.Component.Params.Output, 1, "OnIdx",
        "Per branch: indices of the curves that have a point on them (True).");
      SetTip(this.Component.Params.Output, 2, "OffIdx",
        "Per branch: indices of the curves with no point on them (False).");
    }

    // --- Safety defaults ---
    if (Tol <= 0)
      Tol = (RhinoDoc.ActiveDoc != null) ? RhinoDoc.ActiveDoc.ModelAbsoluteTolerance : 0.001;

    if (Crv == null || Crv.BranchCount == 0 || Pts == null || Pts.BranchCount == 0)
    {
      OnCrv = new DataTree<bool>();
      OnIdx = new DataTree<int>();
      OffIdx = new DataTree<int>();
      return;
    }

    // --- Pair by path when every curve path exists in Pts, else by order ---
    bool byPath = true;
    for (int bi = 0; bi < Crv.BranchCount; bi++)
      if (!Pts.PathExists(Crv.Path(bi))) { byPath = false; break; }

    if (!byPath && Pts.BranchCount != 1 && Pts.BranchCount != Crv.BranchCount)
    {
      string why = (Pts.BranchCount > Crv.BranchCount)
        ? "the extra point branches are ignored."
        : "the extra curve branches reuse the last point branch.";
      this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
        string.Format("Crv has {0} branches but Pts has {1}: {2}", Crv.BranchCount, Pts.BranchCount, why));
    }

    // --- Prepare output DataTrees ---
    DataTree<bool> onTree = new DataTree<bool>();
    DataTree<int> posTree = new DataTree<int>();
    DataTree<int> negTree = new DataTree<int>();
    int badPts = 0;

    // --- Loop through each branch of curves ---
    for (int bi = 0; bi < Crv.BranchCount; bi++)
    {
      GH_Path path = Crv.Path(bi);
      List<Curve> crvBranch = Crv.Branch(bi);
      List<Point3d> ptBranch = byPath ? Pts.Branch(path) : Pts.Branch(Math.Min(bi, Pts.BranchCount - 1));

      // Ensure the branch exists in every output, even if empty
      onTree.EnsurePath(path);
      posTree.EnsurePath(path);
      negTree.EnsurePath(path);

      foreach (Point3d pt in ptBranch)
        if (!pt.IsValid) badPts++;

      for (int j = 0; j < crvBranch.Count; j++)
      {
        Curve c = crvBranch[j];
        bool found = false;

        if (c != null && c.IsValid)
        {
          foreach (Point3d pt in ptBranch)
          {
            if (!pt.IsValid) continue; // empty / null point: skip

            double t;
            if (c.ClosestPoint(pt, out t, Tol) && pt.DistanceTo(c.PointAt(t)) <= Tol)
            {
              found = true;
              break; // one point on the curve is enough
            }
          }
        }

        onTree.Add(found, path);
        if (found) posTree.Add(j, path);
        else       negTree.Add(j, path);
      }
    }

    if (badPts > 0)
      this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
        badPts + " empty or invalid point(s) in Pts were skipped.");

    // --- Outputs ---
    OnCrv  = onTree;
    OnIdx  = posTree;
    OffIdx = negTree;
  }

  private void SetTip(System.Collections.Generic.IList<IGH_Param> ps, int i, string name, string tip)
  {
    // Pins are matched by NAME (the script variable), not by position 'i'.
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
