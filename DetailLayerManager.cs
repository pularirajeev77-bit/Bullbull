#region Metadata
/*
  Platform    : Rhino 8 | Grasshopper C#
  Component   : Detail Layer Manager
  NickName    : DLM
  Message     : Detail Visibility v2.1
  Description : Shows / hides layers inside layout DETAIL views (per-detail layer
                visibility, like "Hide in Detail" in Rhino).
                  Run + Mode 0  -> show ONLY the listed layers in the details
                  Run + Mode 1+ -> hide the listed layers in the details
                  Reset         -> show the listed layers again

  Inputs:
    Run        : bool         (Item) - apply Mode
    Reset      : bool         (Item) - show the listed layers (undo isolate / hide)
    Mode       : int          (Item) - 0 = show only listed, other = hide listed
    Layouts    : List<string> (List) - layout page names
    Details    : List<string> (List) - detail names inside those layouts
    LayerNames : List<string> (List) - layer names or full paths (Parent::Child)

  Output:
    status     : string - what happened, with the number of layers changed
*/
#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Rhino;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;

public class Script_Instance : GH_ScriptInstance
{
    private void RunScript(
        bool Run,
        bool Reset,
        int Mode,
        List<string> Layouts,
        List<string> Details,
        List<string> LayerNames,
        ref object status)
    {
        // --- Metadata + pin tooltips (once) ---
        if (Component != null && Component.Name != "Detail Layer Manager")
        {
            Component.Name = "Detail Layer Manager";
            Component.NickName = "DLM";
            Component.Message = "Detail Visibility v2.1";
            Component.Description = "Shows/hides layers inside layout detail views. Mode 0 isolates the listed layers; Mode 1 hides them; Reset shows them.";

            var pi = Component.Params.Input;
            SetTip(pi, "Run", "True to apply the visibility change (use with Mode). Cannot be True at the same time as Reset.");
            SetTip(pi, "Reset", "True to SHOW the listed layers in the details (undo an isolate/hide).");
            SetTip(pi, "Mode", "0 = Show Only (show listed layers, hide all others). Any other value = Hide the listed layers.");
            SetTip(pi, "Layouts", "Layout page names to act on. List access.");
            SetTip(pi, "Details", "Detail view names within those layouts. List access.");
            SetTip(pi, "LayerNames", "Layer names or full paths (Parent::Child) to show/hide. List access.");
            SetTip(Component.Params.Output, "status", "What happened, with a count of changes, or an error.");
        }

        if (!Run && !Reset)
        {
            status = "Waiting for action... (Toggle 'Run' or 'Reset')";
            return;
        }
        if (Run && Reset)
        {
            string msg = "Error: 'Run' and 'Reset' cannot both be true.";
            status = msg;
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, msg);
            return;
        }
        if (Layouts == null || Details == null || LayerNames == null || Layouts.Count == 0 || Details.Count == 0 || LayerNames.Count == 0)
        {
            string msg = "Error: Layouts, Details and LayerNames must all be provided.";
            status = msg;
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, msg);
            return;
        }

        RhinoDoc doc = RhinoDocument ?? RhinoDoc.ActiveDoc;
        if (doc == null)
        {
            string msg = "Error: no active Rhino document.";
            status = msg;
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, msg);
            return;
        }

        // Layers to act on: matched by name OR full path
        var targets = new HashSet<string>(LayerNames.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()),
                                          StringComparer.OrdinalIgnoreCase);
        var targetLayers = doc.Layers.Where(l => !l.IsDeleted && (targets.Contains(l.Name) || targets.Contains(l.FullPath))).ToList();
        if (targetLayers.Count == 0)
        {
            string msg = "Error: none of the LayerNames exist in this document.";
            status = msg;
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, msg);
            return;
        }

        // For "show only": the PARENTS of listed layers must stay visible too,
        // otherwise a listed child layer is still hidden by its hidden parent.
        var keepVisible = new HashSet<Guid>();
        foreach (Layer l in targetLayers)
        {
            keepVisible.Add(l.Id);
            Guid parent = l.ParentLayerId;
            while (parent != Guid.Empty)
            {
                keepVisible.Add(parent);
                Layer p = doc.Layers.FindId(parent);
                parent = (p != null) ? p.ParentLayerId : Guid.Empty;
            }
        }

        int totalChanges = 0, detailsDone = 0;
        uint undo = doc.BeginUndoRecord("Detail Layer Manager");
        try
        {
            var pages = doc.Views.GetPageViews();
            foreach (string layoutName in Layouts)
            {
                if (string.IsNullOrWhiteSpace(layoutName)) continue;

                RhinoPageView layout = pages.FirstOrDefault(v => string.Equals(v.PageName, layoutName.Trim(), StringComparison.OrdinalIgnoreCase));
                if (layout == null)
                {
                    Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Layout not found: " + layoutName);
                    continue;
                }

                DetailViewObject[] details = layout.GetDetailViews();
                foreach (string detailName in Details)
                {
                    if (string.IsNullOrWhiteSpace(detailName)) continue;

                    // Unnamed details have a null name - string.Equals handles that (d.Name.Equals crashed)
                    DetailViewObject detail = details.FirstOrDefault(d =>
                        string.Equals(d.Attributes.Name, detailName.Trim(), StringComparison.OrdinalIgnoreCase));
                    if (detail == null)
                    {
                        Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Detail not found: " + detailName + " in " + layoutName);
                        continue;
                    }

                    // Per-detail layer visibility is keyed by the detail's VIEWPORT id,
                    // not by the detail object's id (that was why nothing changed).
                    Guid viewportId = detail.Viewport.Id;

                    if (Reset)
                        totalChanges += SetVisible(doc, viewportId, l => keepVisible.Contains(l.Id) ? (bool?)true : null);
                    else if (Mode == 0)
                        totalChanges += SetVisible(doc, viewportId, l => keepVisible.Contains(l.Id));
                    else
                        totalChanges += SetVisible(doc, viewportId, l => targetLayers.Any(t => t.Id == l.Id) ? (bool?)false : null);

                    detail.CommitViewportChanges();
                    detailsDone++;
                }
                layout.Redraw();
            }
        }
        catch (Exception ex)
        {
            string msg = "Error: " + ex.Message;
            status = msg;
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, msg);
            return;
        }
        finally
        {
            doc.EndUndoRecord(undo);
        }

        doc.Views.Redraw();
        status = (Reset ? "Reset: " : "Success: ") + totalChanges + " layer change(s) in " + detailsDone + " detail(s).";
        if (detailsDone == 0)
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No matching detail found - nothing changed.");
    }

    // decide(layer): true = show, false = hide, null = leave as is. Returns layers actually changed.
    private static int SetVisible(RhinoDoc doc, Guid viewportId, Func<Layer, bool?> decide)
    {
        int count = 0;
        foreach (Layer layer in doc.Layers)
        {
            if (layer.IsDeleted) continue;
            bool? want = decide(layer);
            if (!want.HasValue) continue;
            if (layer.PerViewportIsVisible(viewportId) == want.Value) continue;   // already right

            layer.SetPerViewportVisible(viewportId, want.Value);
            layer.CommitChanges();
            count++;
        }
        return count;
    }

    // Find the pin by its variable name (not index), so an extra "out" pin can't shift names.
    // Only NickName/Description are changed - changing Name (the script variable) can break the script.
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
