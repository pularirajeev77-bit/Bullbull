#region Metadata
/*
  Author      : rajeev pulari / Claude
  Platform    : Rhino 8 | Grasshopper C#
  Version     : 2.0  (2026-10-29)
  Component   : Layout From Template
  NickName    : LFT
  Description : Creates new layouts by copying a template layout: page size, detail
                views (position, view, scale, projection lock, display mode, layer)
                and all page-space objects (title block, text, lines ...). One new
                layout per name. The template can be a layout in this file, or a
                layout in ANOTHER .3dm file (TemplateFile) - it is imported once.

  Inputs:
    Run          : bool         (Item) - toggle; layouts are created while it is True
    Template     : string       (Item) - template layout name (or its number, e.g. "2")
    Names        : List<string> (List) - names of the new layouts
    TemplateFile : string       (Item) - optional .3dm file to import the template from

  Output:
    Report       : string - template source, then created / skipped / failed per name

  v2.0
    - NEW TemplateFile: import the template layout from another Rhino file (Rhino's
      ImportLayout), then copy it as before. Imported once; re-runs reuse it.
    - Template also accepts the layout number.
    - Details: the template detail's whole view (camera, projection, zoom) is copied,
      so the scale matches exactly. Before, SetScale got PageToModelRatio as the MODEL
      length, which inverts the scale (1:100 -> 100:1).
    - Layouts already in the file are not created again; report lists every name.
*/
#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
        string TemplateFile,
        ref object Report)
    {
        SetMetadata();

        if (!Run)
        {
            Report = _lastReport ?? "Toggle Run to create the layouts.";
            return;
        }

        RhinoDoc doc = RhinoDocument ?? RhinoDoc.ActiveDoc;
        if (doc == null) { Fail(ref Report, "No active Rhino document."); return; }
        if (string.IsNullOrWhiteSpace(Template)) { Warn(ref Report, "No template layout name (or number)."); return; }
        if (Names == null || Names.Count == 0) { Warn(ref Report, "No names provided."); return; }

        string key = Template.Trim();
        var report = new StringBuilder();

        // 1. Find the template: in this file, or imported from TemplateFile
        RhinoPageView src = FindPage(doc, key);
        bool fromFile = !string.IsNullOrWhiteSpace(TemplateFile);

        if (fromFile && src == null)
        {
            string path = TemplateFile.Trim().Trim('"');
            if (!File.Exists(path)) { Fail(ref Report, "Template file not found: " + path); return; }
            if (!path.EndsWith(".3dm", StringComparison.OrdinalIgnoreCase)) { Fail(ref Report, "Template file must be a .3dm file: " + path); return; }

            src = ImportTemplate(doc, path, key, report);
            if (src == null)
            {
                Fail(ref Report, report + "Could not import layout '" + key + "' from " + Path.GetFileName(path) +
                     ". Check the layout name, or give its number in Template (1 = first layout in that file).");
                return;
            }
        }
        else if (fromFile)
        {
            report.AppendLine("Template '" + src.PageName + "' is already in this file - using it (not imported again).");
        }

        if (src == null) { Fail(ref Report, "Template layout not found: " + key); return; }

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
        int made = 0;
        uint undo = doc.BeginUndoRecord("Layouts from template");
        try
        {
            foreach (string raw in Names)
            {
                string name = raw == null ? "" : raw.Trim();
                if (name.Length == 0) continue;

                if (FindPageByName(doc, name) != null) { report.AppendLine("Skipped (already exists): " + name); continue; }

                try
                {
                    RhinoPageView page = doc.Views.AddPageView(name, src.PageWidth, src.PageHeight);
                    if (page == null) { report.AppendLine("Failed to create: " + name); continue; }

                    foreach (DetailViewObject sd in details)
                        CopyDetail(doc, sd, page);

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
        report.AppendLine("Template        : " + src.PageName + (fromFile ? "  (from " + Path.GetFileName(TemplateFile.Trim().Trim('"')) + ")" : ""));
        report.AppendLine("Details copied  : " + details.Count + " per layout");
        report.AppendLine("Layouts created : " + made);

        Component.Message = "Layout From Template v2.0 | " + made + " new";
        _lastReport = report.ToString();
        Report = _lastReport;
    }

    // <Custom additional code>
    private string _lastReport = null;
    private bool _metaSet = false;

    // ---- template lookup -------------------------------------------------------

    private static RhinoPageView FindPageByName(RhinoDoc doc, string name)
    {
        return doc.Views.GetPageViews().FirstOrDefault(v =>
            string.Equals(v.PageName, name, StringComparison.OrdinalIgnoreCase));
    }

    // By name; else, if key is a number, by layout number (1 = first layout)
    private static RhinoPageView FindPage(RhinoDoc doc, string key)
    {
        RhinoPageView byName = FindPageByName(doc, key);
        if (byName != null) return byName;
        int n;
        if (int.TryParse(key, out n))
            return doc.Views.GetPageViews().FirstOrDefault(v => v.PageNumber + 1 == n);   // PageNumber is 0-based
        return null;
    }

    // Import one layout from another .3dm with Rhino's ImportLayout command.
    // Brings the layout's page objects and details. NOTE: Rhino also imports all
    // layers of that file (known Rhino behaviour).
    private RhinoPageView ImportTemplate(RhinoDoc doc, string path, string key, StringBuilder report)
    {
        var before = new HashSet<Guid>(doc.Views.GetPageViews().Select(v => v.MainViewport.Id));

        string cmd = "_-ImportLayout \"" + path + "\" \"" + key + "\" _Enter";
        bool ok = RhinoApp.RunScript(cmd, false);

        List<RhinoPageView> added = doc.Views.GetPageViews().Where(v => !before.Contains(v.MainViewport.Id)).ToList();
        if (added.Count == 0)
        {
            report.AppendLine("ImportLayout found nothing for '" + key + "'" + (ok ? "" : " (command cancelled)") + ".");
            return null;
        }

        // the imported layout: same name, else (renamed by Rhino or imported by number) the first new one
        RhinoPageView page = added.FirstOrDefault(v => string.Equals(v.PageName, key, StringComparison.OrdinalIgnoreCase))
                          ?? added[0];
        report.AppendLine("Imported template layout '" + page.PageName + "' from " + Path.GetFileName(path) + ".");
        if (added.Count > 1) report.AppendLine("Note: " + added.Count + " layouts came in; using '" + page.PageName + "'.");
        return page;
    }

    // ---- detail copy ------------------------------------------------------------

    private static void CopyDetail(RhinoDoc doc, DetailViewObject sd, RhinoPageView page)
    {
        BoundingBox bb = sd.Geometry.GetBoundingBox(true);
        DefinedViewportProjection proj = sd.Viewport.IsPerspectiveProjection
            ? DefinedViewportProjection.Perspective
            : DefinedViewportProjection.Top;

        DetailViewObject nd = page.AddDetailView(sd.Attributes.Name ?? "",
            new Point2d(bb.Min.X, bb.Min.Y), new Point2d(bb.Max.X, bb.Max.Y), proj);
        if (nd == null) return;

        RhinoViewport sv = sd.Viewport, nv = nd.Viewport;

        // Whole view (camera, projection, frustum = zoom). Same detail size -> same scale.
        bool copied = nv.SetViewProjection(new ViewportInfo(sv), true);
        if (!copied)
        {
            nv.SetCameraLocations(sv.CameraTarget, sv.CameraLocation);
            nv.CameraUp = sv.CameraUp;
            // page length per model length -> SetScale(model length 1, page length ratio)
            nd.DetailGeometry.SetScale(1.0, doc.ModelUnitSystem, sd.DetailGeometry.PageToModelRatio, doc.PageUnitSystem);
        }
        nv.DisplayMode = sv.DisplayMode;

        nd.DetailGeometry.IsProjectionLocked = sd.DetailGeometry.IsProjectionLocked;
        nd.CommitViewportChanges();
        nd.Attributes.LayerIndex = sd.Attributes.LayerIndex;
        nd.CommitChanges();
    }

    // ---- messages + metadata ------------------------------------------------------

    private void Fail(ref object report, string msg)
    {
        report = msg; _lastReport = msg;
        Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, msg);
    }

    private void Warn(ref object report, string msg)
    {
        report = msg;
        Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, msg);
    }

    private void SetMetadata()
    {
        if (_metaSet || Component == null) return;
        _metaSet = true;

        Component.Name = "Layout From Template";
        Component.NickName = "LFT";
        Component.Message = "Layout From Template v2.0";
        Component.Description =
            "Creates new layouts by copying a template layout (page size, details, page objects). " +
            "The template can be in this file or imported from another .3dm (TemplateFile). Rajeev Pulari, v2.0.";

        SetTip(Component.Params.Input, "Run", "Toggle. Layouts are created while True; existing names are skipped, so re-running is safe. Set back to False when done.");
        SetTip(Component.Params.Input, "Template", "Template layout name (case-insensitive) - or its number (1 = first layout). With TemplateFile, the layout in THAT file.");
        SetTip(Component.Params.Input, "Names", "Names for the new layouts. List access. Blank or already-used names are skipped.");
        SetTip(Component.Params.Input, "TemplateFile", "Optional. Full path of another .3dm file to take the template layout from. It is imported once (Rhino ImportLayout - also brings that file's layers). Empty = use a layout in this file.");
        SetTip(Component.Params.Output, "Report", "Template source, then created / skipped / failed per name.");
    }

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
