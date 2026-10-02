#region Metadata
/*
  Author      : rajeev pulari / Gemini
  Platform    : Rhino 8 | Grasshopper C#
  Version     : 2026.10.02
  Component   : ToggleDetailLocks
  NickName    : TDL
  Message     : Detail View Lock v2.1
  Description : Locks / unlocks the projection of every detail view on the named
                layouts with one button. Each press toggles: if any listed detail
                is unlocked, all get LOCKED; if all are locked, all get UNLOCKED.

  Inputs:
    Toggle  : bool         (Item) - connect a Button; acts once per press
    Layouts : List<string> (List) - layout page names

  Output:
    Report  : string - per-layout counts and the new state
*/
#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Display;
using Grasshopper;
using Grasshopper.Kernel;

public class Script_Instance : GH_ScriptInstance
{
    private void RunScript(
        bool Toggle,
        List<string> Layouts,
        ref object Report)
    {
        // Metadata + pin tooltips (once)
        if (Component != null && Component.Name != "ToggleDetailLocks")
        {
            Component.Name = "ToggleDetailLocks";
            Component.NickName = "TDL";
            Component.Message = "Detail View Lock v2.1";
            Component.Description = "Locks/unlocks the projection of all detail views on the given layouts, one press at a time.";

            SetTip(Component.Params.Input, "Toggle", "Connect a Button. Each press: lock all (if any is unlocked) or unlock all.");
            SetTip(Component.Params.Input, "Layouts", "Layout page names. List access.");
            SetTip(Component.Params.Output, "Report", "Per-layout counts and the new lock state.");
        }

        // Act once per press (a Toggle left on would otherwise flip on every recompute)
        bool pressed = Toggle && !_wasPressed;
        _wasPressed = Toggle;
        if (!pressed)
        {
            Report = _lastReport ?? "Press the button to toggle detail lock state.";
            return;
        }

        if (Layouts == null || Layouts.Count == 0)
        {
            Report = "No layout names provided.";
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No layout names provided.");
            return;
        }

        RhinoDoc doc = RhinoDocument ?? RhinoDoc.ActiveDoc;
        if (doc == null)
        {
            Report = "No active Rhino document.";
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No active Rhino document.");
            return;
        }

        // 1. Collect the details of all listed layouts
        var report = new StringBuilder();
        var pages = doc.Views.GetPageViews();
        var found = new List<Tuple<RhinoPageView, DetailViewObject[]>>();
        foreach (string raw in Layouts)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            string name = raw.Trim();
            RhinoPageView page = pages.FirstOrDefault(v => string.Equals(v.PageName, name, StringComparison.OrdinalIgnoreCase));
            if (page == null) { report.AppendLine("Layout '" + name + "' not found."); continue; }
            DetailViewObject[] details = page.GetDetailViews() ?? new DetailViewObject[0];
            found.Add(Tuple.Create(page, details));
            report.AppendLine("Layout '" + page.PageName + "' - " + details.Length + " detail(s)");
        }

        // 2. Decide from the REAL state of the details (not a remembered flag that
        //    resets when the file is reopened): any unlocked -> lock all, else unlock all
        bool anyUnlocked = found.SelectMany(f => f.Item2)
                                .Any(d => d.DetailGeometry != null && !d.DetailGeometry.IsProjectionLocked);
        bool lockIt = anyUnlocked;

        // 3. Apply
        int total = 0, changed = 0;
        uint undo = doc.BeginUndoRecord(lockIt ? "Lock details" : "Unlock details");
        try
        {
            foreach (var f in found)
            {
                foreach (DetailViewObject dvo in f.Item2)
                {
                    total++;
                    try
                    {
                        DetailView geo = dvo.DetailGeometry;
                        if (geo != null && geo.IsProjectionLocked != lockIt)
                        {
                            geo.IsProjectionLocked = lockIt;
                            if (dvo.CommitChanges()) changed++;
                        }
                    }
                    catch (Exception ex)
                    {
                        report.AppendLine("  Failed on " + dvo.Id + ": " + ex.Message);
                    }
                }
                f.Item1.Redraw();
            }
        }
        finally
        {
            doc.EndUndoRecord(undo);
        }

        if (changed > 0) doc.Views.Redraw();

        report.AppendLine("------------------------------------");
        report.AppendLine("Details scanned : " + total);
        report.AppendLine("Details changed : " + changed);
        report.AppendLine("State now       : " + (lockIt ? "LOCKED" : "UNLOCKED"));

        Component.Message = "Detail View Lock v2.1 | " + (lockIt ? "LOCKED" : "UNLOCKED");
        _lastReport = report.ToString();
        Report = _lastReport;
    }

    // <Custom additional code>
    private bool _wasPressed = false;
    private string _lastReport = null;

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
