#region Metadata
/*
  Author      : rajeev pulari / Claude
  Platform    : Rhino 8 | Grasshopper C#
  Version     : 2.2  (2026-10-29)
  Component   : Layout From Template
  NickName    : LFT
  Description : Creates new layouts by copying template layouts: page size, detail
                views (position, view, scale, projection lock, display mode, layer)
                and all page-space objects (title block, text, lines ...).
                One template or a LIST of templates; names as a list or a TREE:
                  1 template            -> every name from it
                  templates + flat list -> template i -> name i
                  templates + tree      -> branch i -> template i
                (a shorter list reuses its last item, Grasshopper-style)
                Templates come from THIS file, or are picked from ANOTHER .3dm
                (TemplateFile): there they are only borrowed - imported once each,
                copied, then removed - so only the layouts in Names are added.

  Inputs:
    Run          : bool             (Item) - toggle; layouts are created while it is True
    Template     : List<string>     (List) - template layout name(s) (or numbers, "2")
    Names        : DataTree<string> (Tree) - names of the new layouts (list or tree)
    TemplateFile : string           (Item) - optional .3dm file holding the templates

  Output:
    Report       : string - templates used, then created / skipped / failed per name

  v2.2
    - Template is a list and Names a tree: copy several templates to several names
      (pairing rules above). Each template is imported once; all are removed after.
  v2.1
    - From TemplateFile the template is only borrowed (imported, copied, deleted with
      its page objects and imported layers left empty).
  v2.0
    - TemplateFile input; layout numbers; details copied as a whole view (scale fix).
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
using Grasshopper.Kernel.Data;

public class Script_Instance : GH_ScriptInstance
{
    private void RunScript(
        bool Run,
        List<string> Template,
        DataTree<string> Names,
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

        var templates = (Template ?? new List<string>())
            .Select(t => t == null ? "" : t.Trim()).Where(t => t.Length > 0).ToList();
        if (templates.Count == 0) { Warn(ref Report, "Template: give the name(s) (or numbers) of the template layout(s)."); return; }
        if (Names == null || Names.DataCount == 0) { Warn(ref Report, "Names: give the names of the new layouts."); return; }

        bool fromFile = !string.IsNullOrWhiteSpace(TemplateFile);
        string path = fromFile ? TemplateFile.Trim().Trim('"') : null;
        var report = new StringBuilder();

        // 0. Pair names with templates -> jobs (template, name); skip existing / repeated names
        var jobs = new List<KeyValuePair<string, string>>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool flat = Names.BranchCount == 1;
        int flatIndex = 0;

        for (int b = 0; b < Names.BranchCount; b++)
        {
            List<string> branch = Names.Branch(b);
            for (int j = 0; j < branch.Count; j++)
            {
                string name = branch[j] == null ? "" : branch[j].Trim();
                if (name.Length == 0) continue;

                // flat list: template i -> name i;  tree: template i -> branch i  (last one reused)
                int ti = flat ? flatIndex++ : b;
                string tpl = templates[Math.Min(ti, templates.Count - 1)];

                if (!seen.Add(name)) { report.AppendLine("Skipped (repeated in Names): " + name); continue; }
                if (FindPageByName(doc, name) != null) { report.AppendLine("Skipped (already exists): " + name); continue; }
                jobs.Add(new KeyValuePair<string, string>(tpl, name));
            }
        }

        int used = flat ? Math.Min(templates.Count, flatIndex) : Math.Min(templates.Count, Names.BranchCount);
        if (templates.Count > used)
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                (templates.Count - used) + " template(s) not used - more templates than " + (flat ? "names" : "branches") + ".");

        if (jobs.Count == 0)
        {
            report.AppendLine("Nothing to create - all names already exist.");
            Finish(ref Report, report, 0);
            return;
        }

        // 1. Template pages: imported once each from TemplateFile (temporary), or layouts of this file
        var srcPages = new Dictionary<string, RhinoPageView>(StringComparer.OrdinalIgnoreCase);
        var imported = new List<RhinoPageView>();
        HashSet<Guid> layersBefore = null;
        int made = 0;

        try
        {
            List<string> needed = jobs.Select(j => j.Key).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            if (fromFile)
            {
                if (!File.Exists(path)) { Fail(ref Report, "Template file not found: " + path); return; }
                if (!path.EndsWith(".3dm", StringComparison.OrdinalIgnoreCase)) { Fail(ref Report, "TemplateFile must be a .3dm file: " + path); return; }
                layersBefore = new HashSet<Guid>(doc.Layers.Where(l => l != null && !l.IsDeleted).Select(l => l.Id));
            }

            foreach (string key in needed)
            {
                RhinoPageView page;
                if (fromFile)
                {
                    page = ImportTemplate(doc, path, key, report);
                    if (page != null) imported.Add(page);
                    else report.AppendLine("Could not import template '" + key + "' from " + Path.GetFileName(path) +
                                           " - check the name, or give its number (1 = first layout in that file).");
                }
                else
                {
                    page = FindPage(doc, key);
                    if (page != null) report.AppendLine("Template: layout '" + page.PageName + "' of this file.");
                    else report.AppendLine("Template layout not found in this file: " + key);
                }
                if (page != null) srcPages[key] = page;
            }

            if (srcPages.Count == 0)
            {
                Fail(ref Report, report + "No template layout available - nothing created.");
                return;
            }

            // 2. Contents of each template, collected once
            var contents = new Dictionary<string, TemplateContent>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in srcPages)
                contents[kv.Key] = Collect(doc, kv.Value);

            // 3. One new layout per job
            uint undo = doc.BeginUndoRecord("Layouts from template");
            try
            {
                foreach (var job in jobs)
                {
                    string name = job.Value;
                    TemplateContent tc;
                    if (!contents.TryGetValue(job.Key, out tc)) { report.AppendLine("Failed (template '" + job.Key + "' missing): " + name); continue; }

                    try
                    {
                        RhinoPageView page = doc.Views.AddPageView(name, tc.Width, tc.Height);
                        if (page == null) { report.AppendLine("Failed to create: " + name); continue; }

                        foreach (DetailViewObject sd in tc.Details)
                            CopyDetail(doc, sd, page);

                        foreach (RhinoObject so in tc.Others)
                        {
                            ObjectAttributes attr = so.Attributes.Duplicate();
                            attr.ViewportId = page.MainViewport.Id;
                            attr.Space = ActiveSpace.PageSpace;
                            doc.Objects.Add(so.Geometry.Duplicate(), attr);
                        }

                        made++;
                        report.AppendLine("Created: " + name + "   (from " + job.Key + ")");
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
            // 4. Imported templates were only sources - remove them again
            if (imported.Count > 0) RemoveImported(doc, imported, layersBefore, report);
        }

        report.AppendLine("------------------------------------");
        report.AppendLine("Templates used  : " + string.Join(", ", srcPages.Keys) +
                          (fromFile ? "  (from " + Path.GetFileName(path) + ", not kept)" : ""));
        Finish(ref Report, report, made);
    }

    // <Custom additional code>
    private string _lastReport = null;
    private bool _metaSet = false;

    private class TemplateContent
    {
        public double Width, Height;
        public List<DetailViewObject> Details = new List<DetailViewObject>();
        public List<RhinoObject> Others = new List<RhinoObject>();
    }

    private static TemplateContent Collect(RhinoDoc doc, RhinoPageView src)
    {
        var tc = new TemplateContent { Width = src.PageWidth, Height = src.PageHeight };
        foreach (RhinoObject o in doc.Objects)
        {
            if (o.Attributes.ViewportId != src.MainViewport.Id) continue;
            if (o is DetailViewObject d) tc.Details.Add(d);
            else tc.Others.Add(o);
        }
        return tc;
    }

    private void Finish(ref object Report, StringBuilder report, int made)
    {
        report.AppendLine("Layouts created : " + made);
        if (made > 0) RhinoDoc.ActiveDoc?.Views.Redraw();
        Component.Message = "Layout From Template v2.2 | " + made + " new";
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

    // ---- clean-up of the imported templates --------------------------------------

    private void RemoveImported(RhinoDoc doc, List<RhinoPageView> pages, HashSet<Guid> layersBefore, StringBuilder report)
    {
        foreach (RhinoPageView page in pages)
        {
            string name = page.PageName;
            if (!RemovePage(doc, page))
                report.AppendLine("Note: the imported template layout '" + name + "' could not be removed - delete it by hand.");
        }

        // Layers that only came in with the imports and are now empty
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
        Component.Message = "Layout From Template v2.2";
        Component.Description =
            "Creates new layouts (Names, list or tree) from one or more template layouts - from this file, or picked " +
            "from another .3dm (TemplateFile), where templates are only borrowed and removed again. Rajeev Pulari, v2.2.";

        SetTip(Component.Params.Input, "Run", "Toggle. Layouts are created while True; names that already exist are skipped (and nothing is imported), so re-running is safe.");
        SetTip(Component.Params.Input, "Template", "Template layout(s) - names (case-insensitive) or numbers (1 = first layout). One template: all names use it. Several: template i -> name i (flat list) or -> branch i (tree); a shorter list reuses its last item. With TemplateFile: layouts of THAT file.");
        SetTip(Component.Params.Input, "Names", "Names of the new layouts to create in this file - a list or a tree (tree access). Tree: branch i uses template i. Only these layouts are added; blank, repeated or existing names are skipped.");
        SetTip(Component.Params.Input, "TemplateFile", "Optional. Full path of the .3dm file holding the template layouts. Each template is imported once, copied, then removed. Empty = use layouts of this file.");
        SetTip(Component.Params.Output, "Report", "Templates used, then created (with its template) / skipped / failed per name.");
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
