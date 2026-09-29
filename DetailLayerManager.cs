using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;

/// <summary>
/// Component: DetailLayerManager
/// Controls per-detail layer visibility on layout pages.
/// </summary>
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
        if (this.Component != null && this.Component.Name != "Detail Layer Manager")
        {
            this.Component.Name = "Detail Layer Manager";
            this.Component.NickName = "DLM";
            this.Component.Message = "Detail Visibility v2.0";
            this.Component.Description = "Shows/hides layers inside layout detail views. Mode 0 isolates the listed layers; Mode 1 hides them; Reset shows them.";

            SetTip(this.Component.Params.Input, 0, "Run",
                "True to apply the visibility change (use with Mode). Cannot be True at the same time as Reset.");
            SetTip(this.Component.Params.Input, 1, "Reset",
                "True to SHOW the listed layers in the details (undo an isolate/hide).");
            SetTip(this.Component.Params.Input, 2, "Mode",
                "0 = Show Only (show listed layers, hide all others). Any other value = Hide the listed layers.");
            SetTip(this.Component.Params.Input, 3, "Layouts",
                "Layout page names to act on.");
            SetTip(this.Component.Params.Input, 4, "Details",
                "Detail view names within those layouts to act on.");
            SetTip(this.Component.Params.Input, 5, "LayerNames",
                "Layer names to show/hide. Matched by layer name (not full path).");
            SetTip(this.Component.Params.Output, 0, "status",
                "What happened, with a count of changes, or an error.");
        }

        // Early exits
        if (!Run && !Reset)
        {
            status = "Waiting for action... (Toggle 'Run' or 'Reset')";
            return;
        }

        if (Run && Reset)
        {
            status = "Error: 'Run' and 'Reset' cannot both be true.";
            this.Component?.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, status);
            return;
        }

        if (Layouts == null || Details == null || LayerNames == null)
        {
            status = "Error: Layouts, Details and LayerNames must all be provided.";
            this.Component?.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, status);
            return;
        }

        RhinoDoc doc = RhinoDoc.ActiveDoc;
        // Guard: the original used doc without a null check.
        if (doc == null)
        {
            status = "Error: no active Rhino document.";
            this.Component?.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, status);
            return;
        }

        int totalChanges = 0;

        try
        {
            foreach (string layoutName in Layouts)
            {
                if (string.IsNullOrWhiteSpace(layoutName)) continue;

                var layout = doc.Views.GetPageViews()
                    .FirstOrDefault(v => v.PageName.Equals(layoutName, StringComparison.OrdinalIgnoreCase));

                if (layout == null)
                {
                    Print("Layout '" + layoutName + "' not found.");
                    this.Component?.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Layout not found: " + layoutName);
                    continue;
                }

                var details = layout.GetDetailViews();
                foreach (string detailName in Details)
                {
                    if (string.IsNullOrWhiteSpace(detailName)) continue;

                    var targetDetail = details.FirstOrDefault(d => d.Name.Equals(detailName, StringComparison.OrdinalIgnoreCase));

                    if (targetDetail == null)
                    {
                        Print("Detail '" + detailName + "' not found in layout '" + layoutName + "'.");
                        this.Component?.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Detail not found: " + detailName + " in " + layoutName);
                        continue;
                    }

                    Guid detailId = targetDetail.Id;

                    if (Reset)
                        totalChanges += ApplyVisibility(doc, detailId, LayerNames, true, false);   // show listed
                    else if (Mode == 0)
                        totalChanges += ApplyVisibility(doc, detailId, LayerNames, true, true);    // show only listed
                    else
                        totalChanges += ApplyVisibility(doc, detailId, LayerNames, false, false);  // hide listed
                }
                layout.Redraw();
            }

            doc.Views.Redraw();
            status = (Reset ? "Reset: " : "Success: ") + totalChanges + " change(s).";
        }
        catch (Exception ex)
        {
            status = "Error: " + ex.Message;
            this.Component?.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, status);
        }
    }

    // --- Helper Method ---
    private int ApplyVisibility(RhinoDoc doc, Guid detailId, List<string> targetLayers, bool state, bool isolate)
    {
        int count = 0;
        var targetSet = new HashSet<string>(targetLayers, StringComparer.OrdinalIgnoreCase);

        foreach (Layer layer in doc.Layers)
        {
            if (layer.IsDeleted) continue;

            bool inList = targetSet.Contains(layer.Name);

            if (isolate)
            {
                // Isolate: listed -> state, everything else -> !state
                bool finalVisible = inList ? state : !state;
                layer.SetPerViewportVisible(detailId, finalVisible);
                count++;
            }
            else if (inList)
            {
                layer.SetPerViewportVisible(detailId, state);
                count++;
            }
        }
        return count;
    }

    private void SetTip(System.Collections.Generic.IList<IGH_Param> ps, int i, string name, string tip)
    {
        if (ps == null || i < 0 || i >= ps.Count) return;
        ps[i].Name = name;
        ps[i].NickName = name;
        ps[i].Description = tip;
    }
}
