#region Metadata
/*
  Author      : Rajeev Pulari + ChatGPT
  Platform    : Rhino 8 | Grasshopper C#
  Version     : 2026.09.30
  Component   : Block Attribute Editor
  NickName    : BlockAttEditor
  Message     : BlockAttEditor v2.1  (after a run: "changed values | layouts")
  Description : Writes attribute user text (key / value) onto the title-block
                instances on each named layout. The write-back partner of
                BlockAttExtract: branch {k} of Values goes to LayoutNames[k].
                One Values branch = same values on every layout.
                Only values that actually differ are written; one Undo step.

  Inputs:
    Run          : bool             (Item) - True = write
    BlockName    : string           (Item) - Title-block definition name (not case-sensitive)
    Keys         : List<string>     (List) - Attribute keys to set
    Values       : DataTree<object> (Tree) - One branch per layout, values in Keys order
    LayoutNames  : List<string>     (List) - Layouts to edit (e.g. BlockAttExtract FoundLayouts)

  Outputs:
    Success      : bool   - True if the block was found on at least one layout
    Summary      : string - What happened
    ChangedCount : int    - Number of attribute values that were changed
*/
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using Rhino;
using Rhino.Display;
using Rhino.DocObjects;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

public class Script_Instance : GH_ScriptInstance
{
  // Run, BlockName : item | Keys, LayoutNames : list | Values : tree
  private void RunScript(
    bool Run,
    string BlockName,
    List<string> Keys,
    DataTree<object> Values,
    List<string> LayoutNames,
    ref object Success,
    ref object Summary,
    ref object ChangedCount)
  {
    // Metadata + pin tooltips (once)
    if (Component != null && Component.Name != "Block Attribute Editor")
    {
      Component.Name = "Block Attribute Editor";
      Component.NickName = "BlockAttEditor";
      Component.Message = "BlockAttEditor v2.1";
      Component.Description = "Writes attribute user text onto the title block on each layout (partner of BlockAttExtract).";

      var pi = Component.Params.Input;
      SetTip(pi, "Run", "True = write the values.");
      SetTip(pi, "BlockName", "Title-block definition name (not case-sensitive).");
      SetTip(pi, "Keys", "Attribute keys to set. List access.");
      SetTip(pi, "Values", "Tree access: branch {k} = values for LayoutNames[k], in Keys order. One branch = same values on all layouts. Null = leave that key unchanged.");
      SetTip(pi, "LayoutNames", "Layouts to edit, e.g. FoundLayouts from BlockAttExtract. List access.");
      var po = Component.Params.Output;
      SetTip(po, "Success", "True if the block was found on at least one layout.");
      SetTip(po, "Summary", "What happened.");
      SetTip(po, "ChangedCount", "Number of attribute values changed.");
    }

    Success = false;
    ChangedCount = 0;

    // 1. Input checks
    if (!Run) { Summary = "Set Run = True to write."; return; }
    if (string.IsNullOrWhiteSpace(BlockName)) { Fail("BlockName is empty.", out Summary); return; }
    if (Keys == null || Keys.Count(k => !string.IsNullOrWhiteSpace(k)) == 0) { Fail("Keys is empty.", out Summary); return; }
    if (LayoutNames == null || LayoutNames.Count == 0) { Fail("LayoutNames is empty.", out Summary); return; }
    if (Values == null || Values.BranchCount == 0) { Fail("Values is empty.", out Summary); return; }

    // One branch per layout, or a single branch shared by all layouts
    bool shared = Values.BranchCount == 1;
    if (!shared && Values.BranchCount != LayoutNames.Count)
    {
      Fail("Values has " + Values.BranchCount + " branches but there are " + LayoutNames.Count +
           " LayoutNames. Use one branch per layout, or a single branch for all.", out Summary);
      return;
    }

    RhinoDoc doc = RhinoDocument ?? RhinoDoc.ActiveDoc;
    if (doc == null) { Fail("No active Rhino document.", out Summary); return; }

    var pages = new Dictionary<string, RhinoPageView>(StringComparer.OrdinalIgnoreCase);
    foreach (RhinoPageView pv in doc.Views.GetPageViews())
      if (!pages.ContainsKey(pv.PageName)) pages[pv.PageName] = pv;
    if (pages.Count == 0) { Fail("No layouts in the document.", out Summary); return; }

    // 2. Write
    int total = 0;
    var edited = new List<string>();
    var missingLayouts = new List<string>();
    var noBlock = new List<string>();
    string blockName = BlockName.Trim();

    uint undo = doc.BeginUndoRecord("Block Attribute Editor");
    try
    {
      for (int i = 0; i < LayoutNames.Count; i++)
      {
        string layoutName = (LayoutNames[i] ?? "").Trim();
        if (layoutName.Length == 0) continue;

        RhinoPageView page;
        if (!pages.TryGetValue(layoutName, out page)) { missingLayouts.Add(layoutName); continue; }

        List<object> vals = Values.Branch(shared ? 0 : i);
        if (vals.Count != Keys.Count)
          Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
            "'" + page.PageName + "': " + vals.Count + " values for " + Keys.Count + " keys; extra keys/values ignored.");

        // Pair keys and values by position; null = leave that key alone (never shifts the pairing)
        var kv = new List<KeyValuePair<string, string>>();
        for (int k = 0; k < Keys.Count && k < vals.Count; k++)
        {
          if (string.IsNullOrWhiteSpace(Keys[k]) || vals[k] == null) continue;
          object raw = (vals[k] is IGH_Goo) ? ((IGH_Goo)vals[k]).ScriptVariable() : vals[k];
          if (raw == null) continue;
          kv.Add(new KeyValuePair<string, string>(Keys[k].Trim(), Convert.ToString(raw)));
        }

        int blocksHere = 0;
        foreach (InstanceObject inst in GetBlocksOnLayout(doc, page, blockName))
        {
          blocksHere++;
          var newAttr = inst.Attributes.Duplicate();
          int changedHere = 0;
          foreach (var pair in kv)
          {
            if (newAttr.GetUserString(pair.Key) != pair.Value)
            {
              newAttr.SetUserString(pair.Key, pair.Value);
              changedHere++;
            }
          }
          if (changedHere > 0)
          {
            if (doc.Objects.ModifyAttributes(inst.Id, newAttr, true)) total += changedHere;
            else Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
              "Could not modify the block on '" + page.PageName + "'.");
          }
        }

        if (blocksHere == 0) noBlock.Add(page.PageName);
        else edited.Add(page.PageName);
      }
    }
    finally
    {
      doc.EndUndoRecord(undo);
    }

    if (total > 0) doc.Views.Redraw();

    // 3. Report
    if (missingLayouts.Count > 0)
      Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Layout(s) not found: " + string.Join(", ", missingLayouts));
    if (noBlock.Count > 0)
      Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No '" + blockName + "' block on: " + string.Join(", ", noBlock));

    Success = edited.Count > 0;
    ChangedCount = total;
    Component.Message = "BlockAttEditor v2.1 | " + total + " changed | " + edited.Count + " layouts";

    if (edited.Count == 0)
      Summary = (missingLayouts.Count == LayoutNames.Count)
        ? "None of the layouts were found."
        : "Block '" + blockName + "' not found on the layouts.";
    else if (total == 0)
      Summary = "Everything already up to date on " + edited.Count + " layout(s): " + string.Join(", ", edited) + ".";
    else
      Summary = "Changed " + total + " value(s) on block '" + blockName + "' across " + edited.Count +
                " layout(s): " + string.Join(", ", edited) + ".";
  }

  // <Custom additional code>
  private void Fail(string msg, out object summary)
  {
    Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, msg);
    summary = msg;
  }

  // All instances of the block on a layout's page space (locked and hidden included)
  private List<InstanceObject> GetBlocksOnLayout(RhinoDoc doc, RhinoPageView page, string blockName)
  {
    var settings = new ObjectEnumeratorSettings
    {
      ViewportFilter = page.MainViewport,
      ObjectTypeFilter = ObjectType.InstanceReference,
      NormalObjects = true,
      LockedObjects = true,
      HiddenObjects = true,
      IncludeGrips = false,
      IncludeLights = false
    };

    var result = new List<InstanceObject>();
    foreach (RhinoObject obj in doc.Objects.GetObjectList(settings))
    {
      var inst = obj as InstanceObject;
      if (inst == null || inst.InstanceDefinition == null) continue;
      if (string.Equals(inst.InstanceDefinition.Name, blockName, StringComparison.OrdinalIgnoreCase))
        result.Add(inst);
    }
    return result;
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
  // </Custom additional code>
}
