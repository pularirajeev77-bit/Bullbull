#region Metadata
/*
  Author      : rajeev pulari / Claude
  Platform    : Rhino 8 | Grasshopper C#
  Version     : 2026.10.12
  Component   : LayoutFromTemplate
  NickName    : LFT
  Message     : Layout From Template v1.0
  Description : Creates new layouts by copying an existing layout (the template):
                page size, detail views (position, camera, scale, projection lock,
                display mode, layer) and all page-space objects (title block, text,
                lines ...). One new layout is made per name in the list.

  Inputs:
    Run      : bool         (Item) - toggle; layouts are created while it is True
    Template : string       (Item) - name of the reference layout to copy
    Names    : List<string> (List) - names of the new layouts

  Output:
    Report   : string - created / skipped / failed per name
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
        bool Run,
        string Template,
        List<string> Names,
        ref object Report)
    {
        // Metadata + pin tooltips (once)
        if (Component != null && Component.Name != "LayoutFromTemplate")
        {
            Component.Name = "LayoutFromTemplate";
            Component.NickName = "LFT";
            Component.Message = "Layout From Template v1.0";
            Component.Description = "Creates new layouts by copying a template layout: page size, detail views and page-space objects. One layout per name.";

            SetTip(Component.Params.Input, "Run", "Toggle. Layouts are created while True; existing names are skipped, so re-running is safe. Set back to False when done.");
            SetTip(Component.Params.Input, "Template", "Name of the reference layout to copy (case-insensitive).");
            SetTip(Component.Params.Input, "Names", "Names for the new layouts. List access. Blank or already-used names are skipped.");
            SetTip(Component.Params.Output, "Report", "Created / skipped / failed message per name.");
        }

        if (!Run)
        {
            Report = _lastReport ?? "Toggle Run to create the layouts.";
            return;
        }

        RhinoDoc doc = RhinoDocument ?? RhinoDoc.ActiveDoc;
        if (doc == null)
        {
            Report = "No active Rhino document.";
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No active Rhino document.");
            return;
        }
        if (string.IsNullOrWhiteSpace(Template))
        {
            Report = "No template layout name.";
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No template layout name.");
            return;
        }
        if (Names == null || Names.Count == 0)
        {
            Report = "No names provided.";
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No names provided.");
            return;
        }

        // 1. Find the template
        RhinoPageView src = doc.Views.GetPageViews().FirstOrDefault(v =>
            string.Equals(v.PageName, Template.Trim(), StringComparison.OrdinalIgnoreCase));
        if (src == null)
        {
            Report = "Template layout not found: " + Template;
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Template layout not found: " + Template);
            return;
        }

        // 2. Collect the template contents once
        var details = new List<DetailViewObject>();
        var others = new List<RhinoObject>();
        foreach (RhinoObject o in doc.Objects)
        {
            if (o.Attributes.ViewportId != src.MainViewport.Id) continue;
            if (o is DetailViewObject d) details.Add(d);
            else others.Add(o);
        }

        // 3. Build one layout per name
        var report = new StringBuilder();
        int made = 0;
        uint undo = doc.BeginUndoRecord("Layouts from template");
        try
        {
            foreach (string raw in Names)
            {
                string name = raw == null ? "" : raw.Trim();
                if (name.Length == 0) continue;

                bool exists = doc.Views.GetPageViews().Any(v =>
                    string.Equals(v.PageName, name, StringComparison.OrdinalIgnoreCase));
                if (exists) { report.AppendLine("Skipped (already exists): " + name); continue; }

                try
                {
                    RhinoPageView page = doc.Views.AddPageView(name, src.PageWidth, src.PageHeight);
                    if (page == null) { report.AppendLine("Failed to create: " + name); continue; }

                    foreach (DetailViewObject sd in details)
                    {
                        BoundingBox bb = sd.Geometry.GetBoundingBox(true);
                        DefinedViewportProjection proj = sd.Viewport.IsPerspectiveProjection
                            ? DefinedViewportProjection.Perspective
                            : DefinedViewportProjection.Top;

                        DetailViewObject nd = page.AddDetailView(sd.Attributes.Name ?? "",
                            new Point2d(bb.Min.X, bb.Min.Y), new Point2d(bb.Max.X, bb.Max.Y), proj);
                        if (nd == null) continue;

                        RhinoViewport sv = sd.Viewport, nv = nd.Viewport;
                        nv.SetCameraLocations(sv.CameraTarget, sv.CameraLocation);
                        nv.CameraUp = sv.CameraUp;
                        nv.DisplayMode = sv.DisplayMode;

                        nd.DetailGeometry.IsProjectionLocked = sd.DetailGeometry.IsProjectionLocked;
                        nd.DetailGeometry.SetScale(sd.DetailGeometry.PageToModelRatio, doc.ModelUnitSystem, 1.0, doc.PageUnitSystem);
                        nd.CommitViewportChanges();
                        nd.Attributes.LayerIndex = sd.Attributes.LayerIndex;
                        nd.CommitChanges();
                    }

                    foreach (RhinoObject so in others)
                    {
                        ObjectAttributes attr = so.Attributes.Duplicate();
                        attr.ViewportId = page.MainViewport.Id;
                        attr.Space = ActiveSpace.PageSpace;
                        doc.Objects.Add(so.Geometry.Duplicate(), attr);
                    }

                    made++;
                    report.AppendLine("Created: " + name);
                }
                catch (Exception ex)
                {
                    report.AppendLine("Failed on '" + name + "': " + ex.Message);
                }
            }
        }
        finally
        {
            doc.EndUndoRecord(undo);
        }

        if (made > 0) doc.Views.Redraw();

        report.AppendLine("------------------------------------");
        report.AppendLine("Template        : " + src.PageName);
        report.AppendLine("Layouts created  : " + made);

        Component.Message = "Layout From Template v1.0 | " + made + " new";
        _lastReport = report.ToString();
        Report = _lastReport;
    }

    // <Custom additional code>
    private string _lastReport = null;

    // Find the pin by its variable name (not index), so an extra "out" pin cannot shift names
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
