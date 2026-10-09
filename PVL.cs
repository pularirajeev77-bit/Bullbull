/*
  Author: Rajeev Pulari + ChatGPT
  Rhino 8 | Grasshopper C#
  Version: 2025.11.09
  Component: PVL (Point Vector Line Bi-directional)

  Description:
    Creates three types of lines from a point, vector, and length:
      • Line     – Centered line (± L/2 about P)
      • PosLine  – From P in +V direction (length L)
      • NegLine  – From P in –V direction (length L)
    Fully adaptive to Item / List / Tree input.
    Preserves branch structure (if input is grafted → outputs grafted).
*/

using System;
using System.Collections.Generic;
using Rhino.Geometry;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;

public class Script_Instance : GH_ScriptInstance
{
  // P, V and L are all tree access. V and L used to be item access
  // (object), so wiring a list of vectors or lengths made Grasshopper run
  // the whole component once per item and duplicate every output.
  private void RunScript(
		DataTree<object> P,
		DataTree<object> V,
		DataTree<object> L,
		ref object Line,
		ref object PosLine,
		ref object NegLine)
  {
    SetPinTips();   // pin tooltips (set once, matched by name)

    this.Component.Message = "PVL v2.0";
    this.Component.NickName = "PVL";

    var lineTree = new DataTree<object>();
    var posTree  = new DataTree<object>();
    var negTree  = new DataTree<object>();

    var ptsTree  = ToTree<Point3d>(P);
    var vecsTree = ToTree<Vector3d>(V);
    var lensTree = ToTree<double>(L);

    if (ptsTree.BranchCount == 0 || vecsTree.BranchCount == 0 || lensTree.BranchCount == 0)
    {
      this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Connect all inputs (P, V, L)");
      Line = lineTree;
      PosLine = posTree;
      NegLine = negTree;
      return;
    }

    // Output paths come from the input with the most branches (P first).
    // Previously every output branch was renamed {0},{1},{2}..., so a
    // grafted input like {0;0},{0;1} came out as {0},{1} and no longer
    // matched the input structure.
    int branchCount = ptsTree.BranchCount;
    if (vecsTree.BranchCount > branchCount) branchCount = vecsTree.BranchCount;
    if (lensTree.BranchCount > branchCount) branchCount = lensTree.BranchCount;

    for (int i = 0; i < branchCount; i++)
    {
      GH_Path path;
      if (i < ptsTree.BranchCount) path = ptsTree.Paths[i];
      else if (i < vecsTree.BranchCount) path = vecsTree.Paths[i];
      else path = lensTree.Paths[i];

      List<Point3d> pts   = GetBranchSafe(ptsTree, i);
      List<Vector3d> vecs = GetBranchSafe(vecsTree, i);
      List<double> lens   = GetBranchSafe(lensTree, i);

      lineTree.EnsurePath(path);
      posTree.EnsurePath(path);
      negTree.EnsurePath(path);

      // An empty branch in any input: nothing to build. (Previously
      // j % pts.Count divided by zero here and crashed the component.)
      if (pts.Count == 0 || vecs.Count == 0 || lens.Count == 0) continue;

      int n = Math.Max(pts.Count, Math.Max(vecs.Count, lens.Count));

      for (int j = 0; j < n; j++)
      {
        // Shorter lists repeat their LAST item, like native Grasshopper
        // components. (j % Count cycled from the start: lengths [5, 10]
        // for 3 points gave 5, 10, 5 instead of 5, 10, 10.)
        Point3d p  = pts[Math.Min(j, pts.Count - 1)];
        Vector3d v = vecs[Math.Min(j, vecs.Count - 1)];
        double len = lens[Math.Min(j, lens.Count - 1)];

        // Zero/near-zero vector or zero length: output null instead of
        // skipping, so the outputs stay aligned item-for-item with the
        // inputs. (Unitize() also catches tiny vectors that IsZero missed.)
        if (len == 0.0 || !v.Unitize())
        {
          lineTree.Add(null, path);
          posTree.Add(null, path);
          negTree.Add(null, path);
          continue;
        }

        // Fully qualified: the output parameter is also named "Line"
        Point3d a = p - v * (len * 0.5);
        Point3d b = p + v * (len * 0.5);
        lineTree.Add(new Rhino.Geometry.Line(a, b), path);

        posTree.Add(new Rhino.Geometry.Line(p, p + v * len), path);
        negTree.Add(new Rhino.Geometry.Line(p, p - v * len), path);
      }
    }

    Line    = lineTree;
    PosLine = posTree;
    NegLine = negTree;
  }

  private DataTree<T> ToTree<T>(DataTree<object> input)
  {
    var tree = new DataTree<T>();
    if (input == null) return tree;

    foreach (GH_Path path in input.Paths)
    {
      foreach (object obj in input.Branch(path))
      {
        if (obj is T cast)
        {
          tree.Add(cast, path);
        }
        else
        {
          try { tree.Add((T)Convert.ChangeType(obj, typeof(T)), path); }
          catch { }
        }
      }
    }
    return tree;
  }

  // Fewer branches than needed: reuse the LAST branch, like native
  // Grasshopper components (was the first branch).
  private List<T> GetBranchSafe<T>(DataTree<T> tree, int index)
  {
    if (tree.BranchCount == 0) return new List<T>();
    return tree.Branch(Math.Min(index, tree.BranchCount - 1));
  }

  // ---------------------------------------------------------------- pin tooltips
  private bool _pinTipsSet = false;

  private void SetPinTips()
  {
    if (_pinTipsSet || Component == null) return;
    _pinTipsSet = true;
    TipPin(Component.Params.Input, "P", "Points the lines start from (tree).");
    TipPin(Component.Params.Input, "V", "Direction vectors — only the direction is used, not their length.");
    TipPin(Component.Params.Input, "L", "Line lengths (tree).");
    TipPin(Component.Params.Output, "Line", "Centered on the point: half the length each way (total length = L)");
    TipPin(Component.Params.Output, "PosLine", "From the point, length L in the vector direction.");
    TipPin(Component.Params.Output, "NegLine", "From the point, length L in the opposite direction.");
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
