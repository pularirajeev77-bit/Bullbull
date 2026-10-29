#region Metadata
/*
  Author      : rajeev pulari / Claude
  Platform    : Rhino 8 | Grasshopper C#
  Version     : 2.1  (2026-10-29)
  Component   : Layout From Template
  NickName    : LFT
  Description : Creates new layouts by copying ONE template layout: page size, detail
                views (position, view, scale, projection lock, display mode, layer)
                and all page-space objects (title block, text, lines ...). One new
                layout per name in Names - nothing else is added.
                The template is a layout of THIS file, or picked from ANOTHER .3dm
                file (TemplateFile): there it is imported only for the copy and
                removed again, so only the layouts in Names remain.

  Inputs:
    Run          : bool         (Item) - toggle; layouts are created while it is True
    Template     : string       (Item) - the ONE layout to use as template (name, or number "2")
    Names        : List<string> (List) - names of the new layouts to create in this file
    TemplateFile : string       (Item) - optional .3dm file that holds the template layout

  Output:
    Report       : string - template source, then created / skipped / failed per name

  v2.1
    - TemplateFile: the template layout is imported only as a temporary source and
      deleted after copying (with its page objects and any imported layers left
      empty). Only the layouts named in Names are added to this file.
    - With TemplateFile, the template is always taken from that file - a layout of
      the same name in this file is never used or changed.
    - No import at all when every name already exists (Run can stay on).
  v2.0
    - TemplateFile input; Template accepts a layout number; detail views copied as a
      whole (camera, projection, zoom) - the old SetScale call inverted the scale.
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
        if (string.IsNullOrWhiteSpace(Template)) { Warn(ref Report, "Template: give the name (or number) of the layout to use."); return; }
        if (Names == null || Names.Count == 0) { Warn(ref Report, "Names: give the names of the new layouts."); return; }

        string key = Template.Trim();
        bool fromFile = !string.IsNullOrWhiteSpace(TemplateFile);
        string path = fromFile ? TemplateFile.Trim().Trim('"') : null;
        var report = new StringBuilder();

        // 0. Which names still have to be made? (nothing to do -> no import)
        var todo = new List<string>();
        foreach (string raw in Names)
        {
            string name = raw == null ? "" : raw.Trim();
            if (name.Length == 0) continue;
            if (todo.Any(t => string.Equals(t, name, StringComparison.OrdinalIgnoreCase))) continue;   // duplicate in Names
            if (FindPageByName(doc, name) != null) { report.AppendLine("Skipped (already exists): " + name); continue; }
            todo.Add(name);
        }
        if (todo.Count == 0)
        {
            report.AppendLine("Nothing to create - all names already exist.");
            Finish(ref Report, report, 0);
            return;
        }

        // 1. The template: imported from TemplateFile (temporary), or a layout of this file
        RhinoPageView src;
        RhinoPageView imported = null;
        HashSet<Guid> layersBefore = null;

        if (fromFile)
        {
            if (!File.Exists(path)) { Fail(ref Report, "Template file not found: " + path); return; }
            if (!path.EndsWith(".3dm", StringComparison.OrdinalIgnoreCase)) { Fail(ref Report, "TemplateFile must be a .3dm file: " + path); return; }

            layersBefore = new HashSet<Guid>(doc.Layers.Where(l => l != null && !l.IsDeleted).Select(l => l.Id));
            imported = ImportTemplate(doc, path, key, report);
            if (imported == null)
            {
                Fail(ref Report, report + "Could not import layout '" + key + "' from " + Path.GetFileName(path) +
                     ". Check the layout name, or give its number in Template (1 = first layout in that file).");
                return;
            }
            src = imported;
        }
        else
        {
            src = FindPage(doc, key);
            if (src == null) { Fail(ref Report, "Template layout not found in this file: " + key); return; }
            report.AppendLine("Template: layout '" + src.PageName + "' of this file.");
        }

        int made = 0, detailCount = 0;
        try
        {
            // 2. Template contents
            var details = new List<DetailViewObject>();
            var others = new List<RhinoObject>();
            foreach (RhinoObject o in doc.Objects)
            {
                if (o.Attributes.ViewportId != src.MainViewport.Id) continue;
                if (o is DetailViewObject d) details.Add(d);
                else others.Add(o);
            }
            detailCount = details.Count;

            // 3. One new layout per name
            uint undo = doc.BeginUndoRecord("Layouts from template");
            try
            {
                foreach (string name in todo)
                {
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
        }
        finally
        {
            // 4. The imported template was only the source - remove it again
            if (imported != null) RemoveImported(doc, imported, layersBefore, report);
        }

        report.AppendLine("------------------------------------");
        report.AppendLine("Template        : " + key + (fromFile ? "  (from " + Path.GetFileName(path) + ", not kept)" : ""));
        report.AppendLine("Details copied  : " + detailCount + " per layout");
        Finish(ref Report, report, made);
    }

    // <Custom additional code>
    private string _lastReport = null;
    private bool _metaSet = false;

    private void Finish(ref object Report, StringBuilder report, int made)
    {
        report.AppendLine("Layouts created : " + made);
        if (made > 0) RhinoDoc.ActiveDoc?.Views.Redraw();
        Component.Message = "Layout From Template v2.1 | " + made + " new";
        _lastReport = report.ToString();
        Report = _lastReport;
    }

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

    // Import ONE layout from another .3dm with Rhino's ImportLayout command.
    // Brings the layout's page objects and details (and, as Rhino does, that file's layers).
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

        // The one we asked for: same name (Rhino may add a suffix if the name is taken), else the first new one
        RhinoPageView page = added.FirstOrDefault(v => string.Equals(v.PageName, key, StringComparison.OrdinalIgnoreCase))
                          ?? added.FirstOrDefault(v => v.PageName.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                          ?? added[0];

        // Anything else that came in is not wanted - remove it straight away
        foreach (RhinoPageView extra in added.Where(v => v.MainViewport.Id != page.MainViewport.Id).ToList())
            RemovePage(doc, extra);

        report.AppendLine("Template: layout '" + key + "' taken from " + Path.GetFileName(path) + ".");
        return page;
    }

    // ---- clean-up of the imported template ---------------------------------------

    private void RemoveImported(RhinoDoc doc, RhinoPageView page, HashSet<Guid> layersBefore, StringBuilder report)
    {
        string name = page.PageName;
        bool removed = RemovePage(doc, page);
        if (!removed)
            report.AppendLine("Note: the imported template layout '" + name + "' could not be removed - delete it by hand.");

        // Layers that only came in with the import and are now empty
        int purged = 0;
        if (layersBefore != null)
        {
            for (int pass = 0; pass < 10; pass++)   // children first, then their parents
            {
                bool any = false;
                foreach (Layer l in doc.Layers.Where(x => x != null && !x.IsDeleted && !layersBefore.Contains(x.Id)).ToList())
                {
                    if (l.Index == doc.Layers.CurrentLayerIndex) continue;
                    var kids = l.GetChildren();
                    if (kids != null && kids.Any(k => !k.IsDeleted)) continue;
                    if (doc.Objects.FindByLayer(l).Length > 0) continue;
                    if (doc.Layers.Delete(l.Index, true)) { purged++; any = true; }
                }
                if (!any) break;
            }
        }
        if (purged > 0) report.AppendLine("Removed " + purged + " empty layer(s) that came with the import.");
    }

    // Delete a layout and everything drawn on it
    private static bool RemovePage(RhinoDoc doc, RhinoPageView page)
    {
        Guid vpId = page.MainViewport.Id;
        var ids = new List<Guid>();
        foreach (RhinoObject o in doc.Objects)
            if (o.Attributes.ViewportId == vpId) ids.Add(o.Id);
        if (ids.Count > 0) doc.Objects.Delete(ids, true);

        page.Close();   // closing a page view deletes the layout
        return !doc.Views.GetPageViews().Any(v => v.MainViewport.Id == vpId);
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
        Component.Message = "Layout From Template v2.1";
        Component.Description =
            "Creates new layouts (Names) by copying ONE template layout - from this file, or picked from another .3dm " +
            "(TemplateFile), where the template is only borrowed and removed again. Rajeev Pulari, v2.1.";

        SetTip(Component.Params.Input, "Run", "Toggle. Layouts are created while True; names that already exist are skipped (and nothing is imported), so re-running is safe.");
        SetTip(Component.Params.Input, "Template", "The ONE layout to use as template - its name (case-insensitive) or number (1 = first layout). With TemplateFile: a layout of THAT file, e.g. one of its 5 layouts.");
        SetTip(Component.Params.Input, "Names", "Names of the new layouts to create in this file (list). Only these are added. Blank, repeated or existing names are skipped.");
        SetTip(Component.Params.Input, "TemplateFile", "Optional. Full path of the .3dm file holding the template layout. The template is imported only to copy it, then removed. Empty = use a layout of this file.");
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
