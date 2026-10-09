/*
  Author: Rajeev Pulari + Gemini
  Rhino 8 | Grasshopper C#
  Version: 2025.11.05b
  Component: Brep Edge Analyzer (Tree Accurate)
    - Outer         : exterior naked edges (the outside border)
    - Inner         : interior naked edges (hole borders)
    - Naked         : all naked edges (valence = Naked)
    - Interior      : interior shared edges (valence = Interior)
    - NonManifold   : non-manifold edges (valence = NonManifold)
    - NakedIdx / InteriorIdx / NonManifoldIdx : BrepEdge indices matching
                      Naked / Interior / NonManifold 1:1

  IMPORTANT: set the "Breps" input Access to TREE.
             (Item access makes GH run this once per Brep -> single flat list.)
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

public class Script_Instance : GH_ScriptInstance
{
  private void RunScript(
		DataTree<object> Breps,
		ref object Outer,
		ref object Inner,
		ref object Naked,
		ref object Interior,
		ref object NonManifold,
		ref object NakedIdx,
		ref object InteriorIdx,
		ref object NonManifoldIdx)
  {
    // --- Metadata + pin tooltips (once) ---
    if (this.Component != null && this.Component.Name != "Brep Edge Analyzer")
    {
      this.Component.Name = "Brep Edge Analyzer";
      this.Component.NickName = "EdgeAn";
      this.Component.Message = "Brep Edge Analyzer v2.1";
      this.Component.Description = "Sorts a Brep's edges into outer/inner naked, interior and non-manifold, with matching edge indices. Set the Breps input to Tree access.";

      SetTip(this.Component.Params.Input, 0, "Breps",
        "The Breps to analyze. SET THIS INPUT TO TREE ACCESS - one result branch per input branch.");
      SetTip(this.Component.Params.Output, 0, "Outer",
        "Exterior naked edges - the outside border of each surface/polysurface.");
      SetTip(this.Component.Params.Output, 1, "Inner",
        "Interior naked edges - the borders of holes.");
      SetTip(this.Component.Params.Output, 2, "Naked",
        "All naked edges (edges on only one face). One curve per edge.");
      SetTip(this.Component.Params.Output, 3, "Interior",
        "Interior edges - shared between two faces.");
      SetTip(this.Component.Params.Output, 4, "NonManifold",
        "Non-manifold edges - shared by three or more faces (usually a modelling error).");
      SetTip(this.Component.Params.Output, 5, "NakedIdx",
        "Edge index of each curve in Naked (matches it 1:1).");
      SetTip(this.Component.Params.Output, 6, "InteriorIdx",
        "Edge index of each curve in Interior (matches it 1:1).");
      SetTip(this.Component.Params.Output, 7, "NonManifoldIdx",
        "Edge index of each curve in NonManifold (matches it 1:1).");
    }

    var outerTree = new DataTree<Curve>();
    var innerTree = new DataTree<Curve>();
    var nakedTree = new DataTree<Curve>();
    var interiorTree = new DataTree<Curve>();
    var nonManTree = new DataTree<Curve>();

    var nakedIdxTree = new DataTree<int>();
    var interiorIdxTree = new DataTree<int>();
    var nonManIdxTree = new DataTree<int>();

    var brepBranches = ExtractBrepBranches(Breps);

    if (brepBranches.Count == 0)
    {
      Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
        "No Breps found. Make sure the 'Breps' input Access is set to Tree.");
    }

    for (int bi = 0; bi < brepBranches.Count; bi++)
    {
      GH_Path path           = brepBranches[bi].Item1;
      List<Brep> branchBreps = brepBranches[bi].Item2;

      List<Curve> oCurves  = new List<Curve>();
      List<Curve> iCurves  = new List<Curve>();
      List<Curve> nkCurves = new List<Curve>();
      List<Curve> inCurves = new List<Curve>();
      List<Curve> nmCurves = new List<Curve>();

      List<int> nkIdx = new List<int>();
      List<int> inIdx = new List<int>();
      List<int> nmIdx = new List<int>();

      foreach (var brep in branchBreps)
      {
        if (brep == null || !brep.IsValid) continue;

        // --- Naked Edges (Outer vs Inner) ---
        Curve[] outerNaked = brep.DuplicateNakedEdgeCurves(true, false);
        if (outerNaked != null) oCurves.AddRange(outerNaked);

        Curve[] innerNaked = brep.DuplicateNakedEdgeCurves(false, true);
        if (innerNaked != null) iCurves.AddRange(innerNaked);

        // --- Edge Valence Analysis (drives Naked / Interior / NonManifold + indices) ---
        foreach (BrepEdge edge in brep.Edges)
        {
          Curve ec = edge.DuplicateCurve();
          if (ec == null) continue;

          switch (edge.Valence)
          {
            case EdgeAdjacency.Naked:
              nkCurves.Add(ec); nkIdx.Add(edge.EdgeIndex); break;

            case EdgeAdjacency.Interior:
              inCurves.Add(ec); inIdx.Add(edge.EdgeIndex); break;

            case EdgeAdjacency.NonManifold:
              nmCurves.Add(ec); nmIdx.Add(edge.EdgeIndex); break;
          }
        }
      }

      // EnsurePath keeps empty branches alive so structure mirrors the input
      outerTree.EnsurePath(path);    outerTree.AddRange(oCurves,  path);
      innerTree.EnsurePath(path);    innerTree.AddRange(iCurves,  path);
      nakedTree.EnsurePath(path);    nakedTree.AddRange(nkCurves, path);
      interiorTree.EnsurePath(path); interiorTree.AddRange(inCurves, path);
      nonManTree.EnsurePath(path);   nonManTree.AddRange(nmCurves, path);

      nakedIdxTree.EnsurePath(path);    nakedIdxTree.AddRange(nkIdx, path);
      interiorIdxTree.EnsurePath(path); interiorIdxTree.AddRange(inIdx, path);
      nonManIdxTree.EnsurePath(path);   nonManIdxTree.AddRange(nmIdx, path);
    }

    Outer          = outerTree;
    Inner          = innerTree;
    Naked          = nakedTree;
    Interior       = interiorTree;
    NonManifold    = nonManTree;
    NakedIdx       = nakedIdxTree;
    InteriorIdx    = interiorIdxTree;
    NonManifoldIdx = nonManIdxTree;
  }

  // ==================== STRUCTURE EXTRACTION ====================

  private List<Tuple<GH_Path, List<Brep>>> ExtractBrepBranches(object input)
  {
    var result = new List<Tuple<GH_Path, List<Brep>>>();
    if (input == null) return result;

    // 1) GH_Structure<T> - the normal Tree-access payload
    if (input is IGH_Structure ghs)
    {
      for (int i = 0; i < ghs.PathCount; i++)
      {
        GH_Path p = ghs.get_Path(i);
        var breps = new List<Brep>();
        IEnumerable branch = ghs.get_Branch(p);
        if (branch != null)
          foreach (object o in branch) AddBrep(o, breps);
        result.Add(Tuple.Create(p, breps));
      }
      return result;
    }

    // 2) DataTree<T> of any T - read via reflection, no cast guessing
    if (TryReadDataTree(input, result)) return result;

    // 3) Plain list, or nested list-of-lists -> sub-paths
    if (input is IEnumerable list && !(input is string))
    {
      WalkList(list, new GH_Path(0), result);
      return result;
    }

    // 4) Single item
    var single = new List<Brep>();
    AddBrep(input, single);
    if (single.Count > 0) result.Add(Tuple.Create(new GH_Path(0), single));
    return result;
  }

  private bool TryReadDataTree(object input, List<Tuple<GH_Path, List<Brep>>> result)
  {
    Type t = input.GetType();
    if (!t.IsGenericType || t.GetGenericTypeDefinition() != typeof(DataTree<>)) return false;

    PropertyInfo pathsProp = t.GetProperty("Paths");
    MethodInfo   branchMi  = t.GetMethod("Branch", new Type[] { typeof(GH_Path) });
    if (pathsProp == null || branchMi == null) return false;

    IEnumerable paths = pathsProp.GetValue(input, null) as IEnumerable;
    if (paths == null) return false;

    foreach (object po in paths)
    {
      GH_Path p = po as GH_Path;
      if (p == null) continue;

      var breps = new List<Brep>();
      IEnumerable branch = branchMi.Invoke(input, new object[] { p }) as IEnumerable;
      if (branch != null)
        foreach (object o in branch) AddBrep(o, breps);

      result.Add(Tuple.Create(p, breps));
    }
    return true;
  }

  // Recursively walks nested lists; each nesting level appends a path element
  private void WalkList(IEnumerable list, GH_Path path, List<Tuple<GH_Path, List<Brep>>> result)
  {
    var here = new List<Brep>();
    int sub = 0;

    foreach (object o in list)
    {
      if (o == null) continue;

      if (o is IEnumerable nested && !(o is string) && !(o is IGH_Goo) && !(o is GeometryBase))
        WalkList(nested, path.AppendElement(sub++), result);
      else
        AddBrep(o, here);
    }

    result.Add(Tuple.Create(path, here));
  }

  // Accepts Brep, GH_Brep, and anything GH can convert (Surface, Box, Extrusion, SubD...)
  private void AddBrep(object item, List<Brep> target)
  {
    if (item == null) return;

    if (item is Brep b) { target.Add(b); return; }

    if (item is GH_Brep gb) { if (gb.Value != null) target.Add(gb.Value); return; }

    Brep conv = null;
    if (GH_Convert.ToBrep(item, ref conv, GH_Conversion.Both) && conv != null)
    { target.Add(conv); return; }

    if (item is IGH_Goo goo)
    {
      Brep cast = null;
      if (goo.CastTo<Brep>(out cast) && cast != null) target.Add(cast);
    }
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
