#region Metadata
/*
 * Author: Rajeev Pulari
 * Rhino 8 | Grasshopper C#
 * Component: CreateTree v1.1
 * Description: Builds a data tree from a flat list: item i goes into branch
 *              {BranchIndex[i]} (mimics Elefront "Create Tree").
 */
#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

public class Script_Instance : GH_ScriptInstance
{
    // Data, BranchIndex : list access
    private void RunScript(List<object> Data, List<int> BranchIndex, ref object Tree)
    {
        // Metadata + pin tooltips (once)
        if (Component != null && Component.Name != "CreateTree")
        {
            Component.Name = "CreateTree";
            Component.NickName = "CreateTree";
            Component.Message = "Mimic Elefront";
            Component.Description = "Sorts a flat list into a tree: item i goes into branch {BranchIndex[i]}.";

            SetTip(Component.Params.Input, "Data", "Items to sort into branches. List access.");
            SetTip(Component.Params.Input, "BranchIndex", "Branch number for each item (cycles if shorter than Data). List access.");
            SetTip(Component.Params.Output, "Tree", "The resulting tree, branches in ascending order.");
        }

        // 1. Validation
        if (Data == null || Data.Count == 0)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Data input is empty.");
            return;
        }
        if (BranchIndex == null || BranchIndex.Count == 0)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "BranchIndex input is empty.");
            return;
        }
        if (BranchIndex.Count != Data.Count)
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                "BranchIndex (" + BranchIndex.Count + ") and Data (" + Data.Count + ") differ in length; BranchIndex cycles.");

        // 2. Group by branch (nulls are kept so item counts stay aligned with the input)
        var groups = new SortedDictionary<int, List<object>>();
        int negative = 0;
        for (int i = 0; i < Data.Count; i++)
        {
            int b = BranchIndex[i % BranchIndex.Count];
            if (b < 0) { negative++; continue; }   // negative path indices are not valid in Grasshopper

            List<object> list;
            if (!groups.TryGetValue(b, out list)) { list = new List<object>(); groups[b] = list; }
            list.Add(Data[i]);
        }
        if (negative > 0)
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                negative + " item(s) had a negative BranchIndex and were skipped.");

        // 3. Build the tree in ascending branch order (independent of input order)
        var tree = new DataTree<object>();
        foreach (var kv in groups)
            tree.AddRange(kv.Value, new GH_Path(kv.Key));

        Tree = tree;
    }

    // Find the pin by its variable name (not index), so an extra "out" pin can't shift names
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
