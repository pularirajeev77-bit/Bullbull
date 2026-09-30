/*
  Rhino 8 | Grasshopper C#
  Component: Dynamic TreeSwap + Prefix v1.1
  Description: Decides from the Names count how to restructure Data:
                 2+ names -> swap the first two path indices {A;B;...} -> {B;A;...}
                 1 name   -> prefix every path with 0: {A;B} -> {0;A;B}
                 0 names  -> no output
               Keeps the tree shape consistent whether one or many items are fed in.
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
    // Names : list access | Data : tree access
    private void RunScript(
        List<string> Names,
        DataTree<object> Data,
        ref object SwappedTree)
    {
        // Metadata + pin tooltips (once)
        if (Component != null && Component.Name != "Dynamic Tree Swap + Prefix")
        {
            Component.Name = "Dynamic Tree Swap + Prefix";
            Component.NickName = "TreeSwap+";
            Component.Message = "Swap / Prefix {0}";
            Component.Description = "2+ Names: swap first two path indices. 1 Name: prefix paths with {0}. 0 Names: no output.";

            SetTip(Component.Params.Input, "Names", "Only the count matters: 2+ = swap, 1 = prefix {0}, 0 = no output. List access.");
            SetTip(Component.Params.Input, "Data", "Tree to restructure. Tree access.");
            SetTip(Component.Params.Output, "SwappedTree", "Restructured tree, branches sorted.");
        }

        SwappedTree = null;

        if (Data == null || Data.BranchCount == 0)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Connect 'Data'.");
            return;
        }
        int nameCount = (Names == null) ? 0 : Names.Count;
        if (nameCount == 0)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Names list is empty -> no output.");
            return;
        }

        try
        {
            bool swap = nameCount > 1;
            Component.Message = swap ? "Swap {A;B} -> {B;A}" : "Prefix {0}";

            var mapped = new List<KeyValuePair<GH_Path, List<object>>>();
            for (int i = 0; i < Data.BranchCount; i++)
            {
                int[] idx = Data.Paths[i].Indices;
                int[] newIdx;

                if (swap)
                {
                    if (idx.Length >= 2)
                    {
                        newIdx = (int[])idx.Clone();       // {A;B;...} -> {B;A;...}
                        newIdx[0] = idx[1];
                        newIdx[1] = idx[0];
                    }
                    else
                    {
                        newIdx = new int[] { 0, idx.Length > 0 ? idx[0] : 0 };   // {A} -> {0;A}
                    }
                }
                else
                {
                    newIdx = new int[idx.Length + 1];      // {A;B} -> {0;A;B}
                    Array.Copy(idx, 0, newIdx, 1, idx.Length);
                }

                mapped.Add(new KeyValuePair<GH_Path, List<object>>(new GH_Path(newIdx), Data.Branch(i)));
            }

            // Sorted output; empty branches kept; collisions merged with a warning
            var result = new DataTree<object>();
            int merged = 0;
            foreach (var kv in mapped.OrderBy(m => m.Key))
            {
                if (result.PathExists(kv.Key)) merged++;
                result.EnsurePath(kv.Key);
                result.AddRange(kv.Value, kv.Key);
            }
            if (merged > 0)
                Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    merged + " branch(es) landed on an existing path and were merged (mixed-depth input).");

            SwappedTree = result;
        }
        catch (Exception ex)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Error: " + ex.Message);
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
