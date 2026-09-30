#region Metadata
/*
  Author      : Rajeev Pulari + ChatGPT
  Platform    : Rhino 8 | Grasshopper C#
  Version     : 2026.09.30
  Component   : Block Attribute Extract
  NickName    : BlockAttExtract
  Message     : BlockAttExtract v2.1  (after a run: "found / requested layouts")
  Description : Reads the attribute user text (key / value pairs) of a title-block
                instance placed on each named layout (page view).
                Branch {k} of Keys / Values belongs to FoundLayouts[k].

  Inputs:
    Run          : bool          (Item) - True = read the layouts
    BlockName    : string        (Item) - Title-block definition name (not case-sensitive)
    LayoutNames  : List<string>  (List) - Layout (page) names to read

  Outputs:
    Keys         : DataTree<string> - Attribute keys, sorted A-Z, one branch per found layout
    Values       : DataTree<string> - Matching attribute values
    FoundLayouts : List<string>     - Layouts that exist, in input order
*/
#endregion

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Rhino;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

public class Script_Instance : GH_ScriptInstance
{
  // Run, BlockName : item access | LayoutNames : list access
  private void RunScript(
    bool Run,
    string BlockName,
    List<string> LayoutNames,
    ref object Keys,
    ref object Values,
    ref object FoundLayouts)
  {
    // Metadata + pin tooltips (once)
    if (Component != null && Component.Name != "Block Attribute Extract")
    {
      Component.Name = "Block Attribute Extract";
      Component.NickName = "BlockAttExtract";
      Component.Message = "BlockAttExtract v2.1";
      Component.Description = "Reads the attribute user text of a title block on each layout.";

      var pi = Component.Params.Input;
      SetTip(pi, "Run", "True = read the layouts.");
      SetTip(pi, "BlockName", "Name of the title-block definition (not case-sensitive).");
      SetTip(pi, "LayoutNames", "Layout (page) names to read. List access.");
      var po = Component.Params.Output;
      SetTip(po, "Keys", "Attribute keys, sorted; branch {k} = FoundLayouts[k].");
      SetTip(po, "Values", "Attribute values matching Keys.");
      SetTip(po, "FoundLayouts", "Layouts that exist, in input order; one branch of Keys/Values each.");
    }

    var keyTree = new DataTree<string>();
    var valTree = new DataTree<string>();
    var found = new List<string>();
    Keys = keyTree;
    Values = valTree;
    FoundLayouts = found;

    if (!Run)
    {
      Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Set Run to True.");
      return;
    }
    if (string.IsNullOrWhiteSpace(BlockName))
    {
      Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "BlockName is empty.");
      return;
    }
    if (LayoutNames == null || LayoutNames.Count == 0)
    {
      Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "LayoutNames is empty.");
      return;
    }

    RhinoDoc doc = RhinoDocument ?? RhinoDoc.ActiveDoc;
    if (doc == null)
    {
      Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No active Rhino document.");
      return;
    }

    // Layouts by page name (case-insensitive); only page views, never model views
    var pages = new Dictionary<string, RhinoPageView>(StringComparer.OrdinalIgnoreCase);
    foreach (RhinoPageView pv in doc.Views.GetPageViews())
      if (!pages.ContainsKey(pv.PageName)) pages[pv.PageName] = pv;

    var missingLayouts = new List<string>();
    var noBlock = new List<string>();

    foreach (string raw in LayoutNames)
    {
      if (string.IsNullOrWhiteSpace(raw)) continue;
      string layoutName = raw.Trim();

      RhinoPageView page;
      if (!pages.TryGetValue(layoutName, out page)) { missingLayouts.Add(layoutName); continue; }

      // Branch index = position in FoundLayouts, so the two always line up
      var path = new GH_Path(found.Count);
      found.Add(page.PageName);
      keyTree.EnsurePath(path);
      valTree.EnsurePath(path);

      List<string> ks, vs;
      int matches;
      GetBlockUserText(doc, page, BlockName.Trim(), out ks, out vs, out matches);

      if (matches == 0) noBlock.Add(page.PageName);
      else if (matches > 1)
        Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
          "'" + page.PageName + "' has " + matches + " '" + BlockName + "' blocks; the first was read.");

      keyTree.AddRange(ks, path);
      valTree.AddRange(vs, path);
    }

    Component.Message = "BlockAttExtract v2.1 | " + found.Count + "/" + LayoutNames.Count(n => !string.IsNullOrWhiteSpace(n)) + " layouts";

    if (missingLayouts.Count > 0)
      Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Layout(s) not found: " + string.Join(", ", missingLayouts));
    if (noBlock.Count > 0)
      Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
        "No '" + BlockName + "' block on: " + string.Join(", ", noBlock) + " (empty branch).");
  }

  // Attribute user text of the first matching block instance on a layout
  private void GetBlockUserText(RhinoDoc doc, RhinoPageView page, string blockName,
                                out List<string> keys, out List<string> values, out int matches)
  {
    keys = new List<string>();
    values = new List<string>();
    matches = 0;

    var settings = new ObjectEnumeratorSettings
    {
      ViewportFilter = page.MainViewport,                 // objects in this layout's page space
      ObjectTypeFilter = ObjectType.InstanceReference,     // blocks only
      LockedObjects = true,                                // title blocks are often locked
      HiddenObjects = true
    };

    InstanceObject first = null;
    foreach (RhinoObject obj in doc.Objects.GetObjectList(settings))
    {
      var inst = obj as InstanceObject;
      if (inst == null || inst.InstanceDefinition == null) continue;
      if (!string.Equals(inst.InstanceDefinition.Name, blockName, StringComparison.OrdinalIgnoreCase)) continue;
      matches++;
      if (first == null) first = inst;
    }
    if (first == null) return;

    var ud = first.Attributes.GetUserStrings();
    if (ud == null || ud.Count == 0) return;

    var sortedKeys = ud.AllKeys.Where(k => k != null).ToList();
    sortedKeys.Sort(StringComparer.OrdinalIgnoreCase);
    foreach (string k in sortedKeys)
    {
      keys.Add(k);
      values.Add(ud[k] ?? "");
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
