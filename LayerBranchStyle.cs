using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

using Rhino;
using Rhino.DocObjects;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

/// <summary>
/// Component: LayerBranchStyle
/// Version: 2025.09.25 (C# Port)
/// Author: rajeev pulari / Gemini
/// Creates layers (nested via "::") and applies colour + linetype, with
/// most-specific-path-wins cascading to child layers.
/// </summary>
public class Script_Instance : GH_ScriptInstance
{
    private void RunScript(
		bool Run,
		DataTree<string> RootNames,
		DataTree<Color> Colors,
		DataTree<string> LineTypes,
		ref object Result,
		ref object Info)
    {
        // --- Metadata + pin tooltips (once) ---
        if (this.Component != null && this.Component.Name != "Layer Branch Style")
        {
            this.Component.Name = "Layer Branch Style";
            this.Component.NickName = "LBS";
            this.Component.Message = "Layer Branch Style v2.0";
            this.Component.Description = "Creates layers (nested via '::') and sets colour + linetype. A style on a parent path cascades to its children; a more specific path wins.";

            SetTip(this.Component.Params.Input, 0, "Run",
                "Set True to apply the changes. False = do nothing.");
            SetTip(this.Component.Params.Input, 1, "RootNames",
                "Layer paths to create/style (tree). Use 'Parent::Child' for nested layers.");
            SetTip(this.Component.Params.Input, 2, "Colors",
                "Layer colours, matched to the names in each branch and cycled if shorter. Default black.");
            SetTip(this.Component.Params.Input, 3, "LineTypes",
                "Linetype names (Continuous/Hidden/Dashed/Dot/Center, or any in the document), cycled if shorter.");
            SetTip(this.Component.Params.Output, 0, "Result",
                "True for each layer that was created or modified.");
            SetTip(this.Component.Params.Output, 1, "Info",
                "Log of what was created and changed.");
        }

        if (!Run)
        {
            Info = "Waiting: Run=False.";
            Result = new List<bool>();
            return;
        }

        var doc = RhinoDoc.ActiveDoc;
        // Guard: the original dereferenced doc without checking, so it threw
        // when no Rhino document was active.
        if (doc == null)
        {
            Info = "Error: no active Rhino document.";
            Result = new List<bool>();
            this.Component?.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No active Rhino document.");
            return;
        }

        var assignments = new List<LayerAssignment>();
        var results = new List<bool>();
        var infoLogs = new List<string>();

        // 1. Flatten Tree Inputs & Zip Assignments
        foreach (GH_Path path in RootNames.Paths)
        {
            var names = RootNames.Branch(path);
            var colors = Colors.PathExists(path) ? Colors.Branch(path) : null;
            var lTypes = LineTypes.PathExists(path) ? LineTypes.Branch(path) : null;

            for (int i = 0; i < names.Count; i++)
            {
                string rawPath = NormalizePath(names[i]);
                if (string.IsNullOrEmpty(rawPath)) continue;

                // Cycle colour/linetype if the list is shorter (i % count)
                Color col = (colors != null && colors.Count > 0) ? colors[i % colors.Count] : Color.Black;
                string lt = (lTypes != null && lTypes.Count > 0) ? lTypes[i % lTypes.Count] : "Continuous";

                var res = ResolveLinetype(doc, lt);

                assignments.Add(new LayerAssignment
                {
                    Path = rawPath,
                    Color = col,
                    LinetypeIndex = res.Index,
                    LinetypeName = res.Name,
                    Depth = rawPath.Split(new[] { "::" }, StringSplitOptions.None).Length
                });
            }
        }

        // 2. Ensure Layers Exist (Recursive Creation)
        foreach (var a in assignments)
        {
            if (EnsureLayerExists(doc, a.Path, a.Color, out string msg))
            {
                if (!string.IsNullOrEmpty(msg)) infoLogs.Add(msg);
                results.Add(true);
            }
        }

        // 3. Apply styles based on Most-Specific-Wins (deterministic depth)
        var layerTable = doc.Layers;
        var plan = new Dictionary<int, LayerAssignment>();

        for (int i = 0; i < layerTable.Count; i++)
        {
            Layer lyr = layerTable[i];
            if (lyr.IsDeleted) continue;

            string fullPath = lyr.FullPath;

            foreach (var a in assignments)
            {
                // Match the exact path or any child of it
                bool isMatch = fullPath == a.Path || fullPath.StartsWith(a.Path + "::");
                if (isMatch)
                {
                    if (!plan.ContainsKey(i) || a.Depth > plan[i].Depth)
                        plan[i] = a;
                }
            }
        }

        // 4. Execute Modifications
        foreach (var entry in plan)
        {
            int index = entry.Key;
            LayerAssignment a = entry.Value;
            Layer lyr = layerTable[index];

            lyr.Color = a.Color;
            string status = "'" + lyr.FullPath + "' (from '" + a.Path + "') color updated.";

            if (a.LinetypeIndex == -1 && a.LinetypeName.Contains("Continuous"))
            {
                lyr.LinetypeIndex = -1;
                status += " Linetype=Default (Continuous).";
            }
            else if (a.LinetypeIndex >= 0)
            {
                lyr.LinetypeIndex = a.LinetypeIndex;
                status += " Linetype='" + a.LinetypeName + "'.";
            }
            else if (!string.IsNullOrEmpty(a.LinetypeName))
            {
                status += " Linetype '" + a.LinetypeName + "' not found; unchanged.";
            }

            lyr.CommitChanges();
            infoLogs.Add(status);
            results.Add(true);
        }

        // Refresh the Rhino UI so colour/linetype changes show immediately
        doc.Views.Redraw();

        Result = results;
        Info = infoLogs;
    }

    // --- Helper Methods & Classes ---

    private struct LayerAssignment
    {
        public string Path;
        public Color Color;
        public int LinetypeIndex;
        public string LinetypeName;
        public int Depth;
    }

    private string NormalizePath(string p)
    {
        if (string.IsNullOrWhiteSpace(p)) return string.Empty;
        string s = p.Trim();
        while (s.Contains("::::")) s = s.Replace("::::", "::");
        while (s.Contains(":::")) s = s.Replace(":::", "::");
        if (s.StartsWith("::")) s = s.Substring(2);
        if (s.EndsWith("::")) s = s.Substring(0, s.Length - 2);
        return s;
    }

    private (int Index, string Name) ResolveLinetype(RhinoDoc doc, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return (-1, "");
        string raw = name.Trim();
        string low = raw.ToLower();

        if (new[] { "continuous", "continous", "cont", "default" }.Contains(low))
            return (-1, "Continuous (Default)");

        var aliases = new Dictionary<string, string>{
            {"hidden", "Hidden"}, {"dashed", "Dashed"}, {"dot", "Dot"}, {"dotted", "Dot"}, {"center", "Center"}
        };

        string target = aliases.ContainsKey(low) ? aliases[low] : raw;
        int idx = doc.Linetypes.Find(target, true);

        if (idx >= 0) return (idx, target);

        // Fuzzy search fallback
        for (int i = 0; i < doc.Linetypes.Count; i++)
        {
            if (doc.Linetypes[i].Name.IndexOf(target, StringComparison.OrdinalIgnoreCase) >= 0)
                return (i, doc.Linetypes[i].Name);
        }

        return (-1, raw);
    }

    private bool EnsureLayerExists(RhinoDoc doc, string fullPath, Color leafColor, out string message)
    {
        message = string.Empty;
        int existingIdx = doc.Layers.FindByFullPath(fullPath, RhinoMath.UnsetIntIndex);
        if (existingIdx >= 0) return false;

        string[] parts = fullPath.Split(new[] { "::" }, StringSplitOptions.None);
        int parentIdx = RhinoMath.UnsetIntIndex;
        var created = new List<string>();

        for (int i = 0; i < parts.Length; i++)
        {
            string subPath = string.Join("::", parts.Take(i + 1));
            int currentIdx = doc.Layers.FindByFullPath(subPath, RhinoMath.UnsetIntIndex);

            if (currentIdx < 0)
            {
                Layer lyr = new Layer();
                lyr.Name = parts[i];
                if (parentIdx != RhinoMath.UnsetIntIndex)
                    lyr.ParentLayerId = doc.Layers[parentIdx].Id;

                // Only apply the specific colour to the leaf node
                lyr.Color = (i == parts.Length - 1) ? leafColor : Color.Black;

                currentIdx = doc.Layers.Add(lyr);
                created.Add(subPath); // log every created level, not just the last
            }
            parentIdx = currentIdx;
        }

        if (created.Count > 0) message = "Created: " + string.Join(", ", created) + ".";
        return true;
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
