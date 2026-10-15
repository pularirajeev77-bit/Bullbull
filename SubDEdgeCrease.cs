/*
  Component : SubD Edge Crease Manager  (nickname: SubDCrease)
  Author    : Rajeev Pulari
  Version   : 2.1  (2026-10-15)
  Platform  : Rhino 8 | Grasshopper C# Script

  Purpose
  -------
  Creases or smooths chosen edges of SubD objects by edge index. Crease
  edges give sharp lines on the SubD limit surface; smooth edges blend.
  The input SubDs are copied - Rhino geometry is never changed.

  Inputs (tree access - branches are matched by path)
  ------
  SubDs         SubD objects to modify.
  EdgeIndices   Edge indices to change, in Rhino's edge order (0 = first edge,
                the same order Grasshopper's SubD edge components list them).
  CreaseValues  One value per index: > 0 = Crease, <= 0 = Smooth. If the list
                is shorter, its last value is reused. Empty = all Crease.

  Outputs
  -------
  CreasedSubDs  Copies of the SubDs with the new edge tags (same tree).
  ChangedEdges  Indices that were actually changed, per SubD.

  Branch matching: each SubD branch uses the EdgeIndices / CreaseValues branch
  with the same path; if that path does not exist, branch {0} is used for all.

  v2.1 changes
  ------------
  - Wrong edges: edges were looked up with Edges.Find(index), which searches
    by edge ID, not by position. Rhino's edge IDs do not start at 0, so index
    0 matched nothing, every other index hit a different edge, and the last
    edge was refused by the bounds check. Edges are now taken by position in
    Rhino's edge order.
  - Vertex tags were not updated after re-tagging edges, so new creases could
    evaluate wrongly; tags and sector coefficients are now updated.
  - Boundary (naked) edges must stay creased in a SubD; asking to smooth them
    is now skipped with a remark instead of producing an invalid SubD.
  - Unconnected EdgeIndices / CreaseValues crashed (null tree); now handled.
  - Weights were described as "continuous" but only on/off is applied; the
    input is renamed CreaseValues and documented as > 0 = crease.
  - Renamed SubD_Geometries, Edge_Indices, Edge_Weights / Modified_SubD ->
    SubDs, EdgeIndices, CreaseValues / CreasedSubDs; new ChangedEdges output;
    metadata, tooltips and a status message.
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
    private bool _metaSet = false;

    private void RunScript(
		DataTree<SubD> SubDs,
		DataTree<int> EdgeIndices,
		DataTree<double> CreaseValues,
		ref object CreasedSubDs,
		ref object ChangedEdges)
    {
        SetMetadata();
        Component.Message = "SubD Crease v2.1";

        var outTree = new DataTree<SubD>();
        var changedTree = new DataTree<int>();

        if (SubDs == null || SubDs.DataCount == 0)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "SubDs: connect at least one SubD.");
            return;
        }
        if (EdgeIndices == null || EdgeIndices.DataCount == 0)
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "No EdgeIndices - SubDs are passed through unchanged.");

        int creased = 0, smoothed = 0, outOfRange = 0, boundaryKept = 0;

        for (int i = 0; i < SubDs.BranchCount; i++)
        {
            GH_Path path = SubDs.Path(i);
            List<SubD> subdList = SubDs.Branch(i);

            List<int> indices = BranchFor(EdgeIndices, path);
            List<double> values = BranchFor(CreaseValues, path);

            changedTree.EnsurePath(path);

            for (int j = 0; j < subdList.Count; j++)
            {
                SubD original = subdList[j];
                GH_Path itemPath = path.AppendElement(j);
                changedTree.EnsurePath(itemPath);

                if (original == null)
                {
                    outTree.Add(null, path);
                    continue;
                }

                // Copy - never change the Rhino / upstream geometry
                SubD dup = (SubD)original.Duplicate();

                // Edges by POSITION in Rhino's edge order (not by edge ID)
                var edges = new List<SubDEdge>();
                foreach (SubDEdge e in dup.Edges) edges.Add(e);

                bool modified = false;
                for (int k = 0; k < indices.Count; k++)
                {
                    int idx = indices[k];
                    if (idx < 0 || idx >= edges.Count) { outOfRange++; continue; }

                    double v = values.Count == 0 ? 1.0
                             : (k < values.Count ? values[k] : values[values.Count - 1]);
                    bool crease = v > 0.0;

                    SubDEdge edge = edges[idx];
                    if (edge == null) continue;

                    if (!crease && edge.FaceCount < 2)
                    {
                        boundaryKept++;   // boundary edges must stay creased
                        continue;
                    }

                    SubDEdgeTag want = crease ? SubDEdgeTag.Crease : SubDEdgeTag.Smooth;
                    if (edge.Tag == want) continue;

                    edge.Tag = want;
                    modified = true;
                    changedTree.Add(idx, itemPath);
                    if (crease) creased++; else smoothed++;
                }

                if (modified)
                {
                    // Vertex tags follow the edge tags; then rebuild the limit surface
                    dup.UpdateAllTagsAndSectorCoefficients();
                    dup.ClearEvaluationCache();
                }

                outTree.Add(dup, path);
            }
        }

        if (outOfRange > 0)
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                outOfRange + " edge index(es) out of range were skipped.");
        if (boundaryKept > 0)
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                boundaryKept + " boundary edge(s) kept creased - SubD boundary edges cannot be smooth.");

        CreasedSubDs = outTree;
        ChangedEdges = changedTree;
        Component.Message = "SubD Crease v2.1\n" + creased + " creased, " + smoothed + " smoothed";
    }

    // Branch with the same path, else branch {0}, else empty.
    private static List<T> BranchFor<T>(DataTree<T> tree, GH_Path path)
    {
        if (tree == null || tree.BranchCount == 0) return new List<T>();
        if (tree.PathExists(path)) return tree.Branch(path);
        return tree.Branch(0);
    }

    // ---------------------------------------------------------------- metadata

    private void SetMetadata()
    {
        if (_metaSet) return;
        _metaSet = true;

        Component.Name = "SubD Edge Crease Manager";
        Component.NickName = "SubDCrease";
        Component.Description =
            "Creases or smooths SubD edges by edge index (Rhino's edge order). Works on copies; " +
            "vertex tags are updated so the limit surface evaluates correctly. Rajeev Pulari, v2.1.";

        var ins = Component.Params.Input;
        SetTip(ins, "SubDs", "SubD objects to modify (tree). They are copied - the originals are not changed.");
        SetTip(ins, "EdgeIndices", "Edge indices to change, in Rhino's edge order (0 = first edge). Branch matched by path, else branch {0}.");
        SetTip(ins, "CreaseValues", "Per index: > 0 = Crease, <= 0 = Smooth. Shorter list reuses its last value. Empty = all Crease.");

        var outs = Component.Params.Output;
        SetTip(outs, "CreasedSubDs", "Copies of the SubDs with the new edge tags (same tree as SubDs).");
        SetTip(outs, "ChangedEdges", "Edge indices actually changed, one branch per SubD ({path;item}).");
    }

    // Match pins by Name (the script variable), fall back to NickName.
    // Only NickName/Description are changed - never Name.
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
}
