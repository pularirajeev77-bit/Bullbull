/*
  Rhino 8 | Grasshopper C#
  Component: Dynamic TreeSwap v1.1
  Description: Swaps the first two indices of every branch path
               ({A;B;...} -> {B;A;...}), e.g. rows <-> columns.
               A flat tree {i} becomes {0;i}.
*/

using System;
using System.Collections.Generic;
using System.Linq;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

public class Script_Instance : GH_ScriptInstance
{
    // Data : tree access
    private void RunScript(DataTree<object> Data, ref object SwappedTree)
    {
        // Metadata + pin tooltips (once)
        if (Component != null && Component.Name != "Dynamic Tree Swap")
        {
            Component.Name = "Dynamic Tree Swap";
            Component.NickName = "Dynamic TreeSwap";
            Component.Message = "Dynamic Data Tree Swapping";
            Component.Description = "Swaps the first two path indices of every branch: {A;B;...} -> {B;A;...}. Flat {i} -> {0;i}.";

            SetTip(Component.Params.Input, "Data", "Tree to swap. Tree access.");
            SetTip(Component.Params.Output, "SwappedTree", "Tree with the first two path indices swapped, branches sorted.");
        }

        var result = new DataTree<object>();
        SwappedTree = result;

        if (Data == null || Data.BranchCount == 0)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Data input is empty.");
            return;
        }

        try
        {
            // 1. Build the new path for each branch
            var mapped = new List<KeyValuePair<GH_Path, List<object>>>();
            for (int i = 0; i < Data.BranchCount; i++)
            {
                int[] idx = Data.Paths[i].Indices;
                int[] newIdx;

                if (idx.Length >= 2)
                {
                    // {A;B;...} -> {B;A;...}
                    newIdx = (int[])idx.Clone();
                    newIdx[0] = idx[1];
                    newIdx[1] = idx[0];
                }
                else
                {
                    // {i} -> {0;i}
                    newIdx = new int[] { 0, idx.Length > 0 ? idx[0] : 0 };
                }

                mapped.Add(new KeyValuePair<GH_Path, List<object>>(new GH_Path(newIdx), Data.Branch(i)));
            }

            // 2. Sorted output, so the new first index groups neatly ({0;0},{0;1},...,{1;0},...)
            int merged = 0;
            foreach (var kv in mapped.OrderBy(m => m.Key))
            {
                if (result.PathExists(kv.Key)) merged++;
                result.EnsurePath(kv.Key);                  // keep empty branches too
                result.AddRange(kv.Value, kv.Key);
            }

            if (merged > 0)
                Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    merged + " branch(es) landed on an existing path and were merged (mixed-depth input like {1} and {1;0}).");
        }
        catch (Exception ex)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "TreeSwap failed: " + ex.Message);
        }
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
