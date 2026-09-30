/*
  Rhino 8 | Grasshopper C#
  Component: CNCExport v2.1
  Description: One DWG/DXF per panel. Temporarily bakes curves, points and
               annotations with per-layer colour and linetype, exports them,
               then removes them. Exports ONCE per press.

  Tree layout:  FileNames {panel}            -> one file per panel
                Curves / Points / Annotations {panel; layer} -> sub-branch k of a
                panel goes on LayerNames[k] / LayerColors[k] / Linetypes[k]
*/

using System;
using System.IO;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

public class Script_Instance : GH_ScriptInstance
{
    // Export, FileType, Folder : item | FileNames, Curves, Points, Annotations : tree
    // AnnotationLayers, AnnotationColors, LayerNames, LayerColors, Linetypes : list (cycle)
    private void RunScript(
        bool Export,
        string FileType,
        DataTree<string> FileNames,
        string Folder,
        DataTree<Curve> Curves,
        DataTree<Point3d> Points,
        DataTree<object> Annotations,
        List<string> AnnotationLayers,
        List<Color> AnnotationColors,
        List<string> LayerNames,
        List<Color> LayerColors,
        List<string> Linetypes,
        ref object ExportedFiles)
    {
        // Metadata + pin tooltips (once)
        if (Component != null && Component.Name != "CNCExport")
        {
            Component.Name = "CNCExport";
            Component.NickName = "CNCExport";
            Component.Message = "CNC Export v2.1";
            Component.Description = "One DWG/DXF per panel, with layers, colours and linetypes.";

            var pi = Component.Params.Input;
            SetTip(pi, "Export", "Exports once each time this goes from False to True. Use a Button.");
            SetTip(pi, "FileType", "DWG or DXF (empty = DWG).");
            SetTip(pi, "FileNames", "One name per panel branch {panel} (first item, no extension). Existing files are overwritten.");
            SetTip(pi, "Folder", "Existing folder to save into.");
            SetTip(pi, "Curves", "Curves as {panel; layer}: sub-branch k goes on LayerNames[k].");
            SetTip(pi, "Points", "Points as {panel; layer}, same layer rules as Curves.");
            SetTip(pi, "Annotations", "Text, leaders, dimensions, curves as {panel; layer}.");
            SetTip(pi, "AnnotationLayers", "Annotation layer per sub-branch (cycles). Empty = Annotations.");
            SetTip(pi, "AnnotationColors", "Annotation colour per sub-branch (cycles). Empty = black.");
            SetTip(pi, "LayerNames", "Layer per sub-branch (cycles), Parent::Child OK. Empty = Layer_k.");
            SetTip(pi, "LayerColors", "Colour per sub-branch (cycles). Empty = black.");
            SetTip(pi, "Linetypes", "Linetype per sub-branch (cycles), e.g. Continuous, Dashed, Hidden, Center.");
            SetTip(Component.Params.Output, "ExportedFiles", "Full paths of the files written by the last press.");
        }

        ExportedFiles = _lastFiles;

        // Export only on the rising edge (a Toggle left on would re-export on every recompute)
        bool trigger = Export && !_wasPressed;
        _wasPressed = Export;
        if (!trigger)
        {
            if (!Export) Msg(GH_RuntimeMessageLevel.Remark, "Idle. Set 'Export' to True.");
            return;
        }

        // 1. Checks
        string ext = string.IsNullOrWhiteSpace(FileType) ? "dwg" : FileType.Trim().TrimStart('.').ToLowerInvariant();
        if (ext != "dwg" && ext != "dxf") { Msg(GH_RuntimeMessageLevel.Error, "FileType must be DWG or DXF."); return; }
        if (string.IsNullOrWhiteSpace(Folder) || !Directory.Exists(Folder)) { Msg(GH_RuntimeMessageLevel.Error, "Folder does not exist."); return; }
        if (FileNames == null || FileNames.BranchCount == 0) { Msg(GH_RuntimeMessageLevel.Warning, "No FileNames supplied."); return; }

        RhinoDoc doc = RhinoDocument ?? RhinoDoc.ActiveDoc;
        if (doc == null) { Msg(GH_RuntimeMessageLevel.Error, "No active Rhino document."); return; }

        var exported = new List<string>();
        var previousSelection = doc.Objects.GetSelectedObjects(false, false).Select(o => o.Id).ToList();

        // 2. One file per FileNames branch; its panel number is the first index of that branch's path
        for (int i = 0; i < FileNames.BranchCount; i++)
        {
            var names = FileNames.Branch(i);
            if (names == null || names.Count == 0 || string.IsNullOrWhiteSpace(names[0])) continue;

            int panel = FileNames.Paths[i].Indices[0];
            string fileName = names[0].Trim();
            if (fileName.EndsWith("." + ext, StringComparison.OrdinalIgnoreCase))
                fileName = fileName.Substring(0, fileName.Length - ext.Length - 1);
            if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                Msg(GH_RuntimeMessageLevel.Warning, "Skipped '" + fileName + "': invalid characters in file name.");
                continue;
            }

            string result = ExportPanel(doc, panel, ext, fileName, Folder, Curves, Points, Annotations,
                AnnotationLayers, AnnotationColors, LayerNames, LayerColors, Linetypes);
            if (result != null) exported.Add(result);
        }

        doc.Objects.UnselectAll();
        if (previousSelection.Count > 0) doc.Objects.Select(previousSelection, true);
        doc.Views.Redraw();

        _lastFiles = exported;
        ExportedFiles = exported;
        Msg(GH_RuntimeMessageLevel.Remark, "Exported " + exported.Count + " file(s).");
    }

    // <Custom additional code>
    private bool _wasPressed = false;
    private List<string> _lastFiles = new List<string>();
    private readonly HashSet<string> _warnedLinetypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private void Msg(GH_RuntimeMessageLevel level, string msg)
    {
        if (Component != null) Component.AddRuntimeMessage(level, msg);
    }

    private string ExportPanel(RhinoDoc doc, int panel, string ext, string fileName, string folder,
        DataTree<Curve> curves, DataTree<Point3d> points, DataTree<object> annotations,
        List<string> annoLayers, List<Color> annoColors,
        List<string> layerNames, List<Color> layerColors, List<string> linetypes)
    {
        var objIds = new List<Guid>();
        try
        {
            // Sub-branches {panel; *} of each input, in tree order
            var curvePaths = PathsOf(curves == null ? null : curves.Paths, panel);
            var pointPaths = PathsOf(points == null ? null : points.Paths, panel);
            var annoPaths = PathsOf(annotations == null ? null : annotations.Paths, panel);

            int branchLimit = Math.Max(curvePaths.Count, Math.Max(pointPaths.Count, annoPaths.Count));
            for (int k = 0; k < branchLimit; k++)
            {
                string layerName = SafeIndex(layerNames, k, "Layer_" + k);
                Color col = SafeIndex(layerColors, k, Color.Black);
                string lt = SafeIndex(linetypes, k, "Continuous");
                var attr = MakeAttributes(doc, layerName, col, lt);

                if (k < curvePaths.Count)
                    foreach (Curve c in curves.Branch(curvePaths[k]))
                        if (c != null) Keep(objIds, doc.Objects.AddCurve(c, attr));

                if (k < pointPaths.Count)
                    foreach (Point3d p in points.Branch(pointPaths[k]))
                        if (p.IsValid) Keep(objIds, doc.Objects.AddPoint(p, attr));

                if (k < annoPaths.Count)
                {
                    var aAttr = MakeAttributes(doc, SafeIndex(annoLayers, k, "Annotations"),
                        SafeIndex(annoColors, k, Color.Black), null);
                    foreach (object a in annotations.Branch(annoPaths[k]))
                    {
                        object raw = (a is IGH_Goo) ? ((IGH_Goo)a).ScriptVariable() : a;
                        var geo = raw as GeometryBase;          // text, leader, dimension, curve, hatch ...
                        if (geo != null) Keep(objIds, doc.Objects.Add(geo, aAttr));
                        else if (raw is Line) Keep(objIds, doc.Objects.AddLine((Line)raw, aAttr));
                        else if (raw is Polyline) Keep(objIds, doc.Objects.AddPolyline((Polyline)raw, aAttr));
                    }
                }
            }

            if (objIds.Count == 0)
            {
                Msg(GH_RuntimeMessageLevel.Remark, "'" + fileName + "' (panel " + panel + "): nothing to export.");
                return null;
            }

            // Export only the temporary objects
            doc.Objects.UnselectAll();
            doc.Objects.Select(objIds, true);

            string fullPath = Path.Combine(folder, fileName + "." + ext);
            // Rhino asks "replace?" for an existing file, which would break the scripted command
            if (File.Exists(fullPath)) File.Delete(fullPath);

            bool ran = RhinoApp.RunScript("-_Export \"" + fullPath + "\" _Enter", false);
            if (ran && File.Exists(fullPath)) return fullPath;

            Msg(GH_RuntimeMessageLevel.Error, "Export failed: " + fileName + "." + ext);
            return null;
        }
        catch (Exception ex)
        {
            Msg(GH_RuntimeMessageLevel.Error, "'" + fileName + "': " + ex.Message);
            return null;
        }
        finally
        {
            // Always remove the temporary objects, even if the export failed
            foreach (Guid id in objIds) doc.Objects.Delete(id, true);
        }
    }

    private static List<GH_Path> PathsOf(IList<GH_Path> paths, int panel)
    {
        if (paths == null) return new List<GH_Path>();
        return paths.Where(p => p.Length > 0 && p.Indices[0] == panel).ToList();
    }

    private static void Keep(List<Guid> ids, Guid id)
    {
        if (id != Guid.Empty) ids.Add(id);
    }

    // linetype == null -> by layer (used for annotations)
    private ObjectAttributes MakeAttributes(RhinoDoc doc, string layerName, Color col, string linetype)
    {
        var attr = doc.CreateDefaultAttributes();
        int layerIdx = GetOrCreateLayer(doc, layerName, col);
        if (layerIdx >= 0) attr.LayerIndex = layerIdx;
        attr.ObjectColor = col;
        attr.ColorSource = ObjectColorSource.ColorFromObject;

        if (string.IsNullOrWhiteSpace(linetype) || linetype.Trim().Equals("Continuous", StringComparison.OrdinalIgnoreCase))
        {
            // "Continuous" is Rhino's built-in default: set it by object (index -1)
            attr.LinetypeIndex = -1;
            attr.LinetypeSource = linetype == null ? ObjectLinetypeSource.LinetypeFromLayer : ObjectLinetypeSource.LinetypeFromObject;
        }
        else
        {
            int ltIdx = doc.Linetypes.Find(linetype.Trim());
            if (ltIdx >= 0)
            {
                attr.LinetypeIndex = ltIdx;
                attr.LinetypeSource = ObjectLinetypeSource.LinetypeFromObject;
            }
            else
            {
                attr.LinetypeSource = ObjectLinetypeSource.LinetypeFromLayer;
                if (_warnedLinetypes.Add(linetype))
                    Msg(GH_RuntimeMessageLevel.Warning, "Linetype '" + linetype + "' not found in this model; layer linetype used.");
            }
        }
        return attr;
    }

    // Finds or creates a layer ("Parent::Child" OK) and makes it visible + unlocked,
    // otherwise its objects cannot be selected and nothing is exported.
    private int GetOrCreateLayer(RhinoDoc doc, string fullPath, Color col)
    {
        if (string.IsNullOrWhiteSpace(fullPath)) return -1;
        fullPath = fullPath.Trim();
        int idx = doc.Layers.FindByFullPath(fullPath, -1);
        if (idx < 0)
        {
            Guid parentId = Guid.Empty;
            string path = "";
            foreach (string raw in fullPath.Split(new[] { "::" }, StringSplitOptions.None))
            {
                string part = raw.Trim();
                if (!Layer.IsValidName(part)) return -1;
                path = (path.Length == 0) ? part : path + "::" + part;
                idx = doc.Layers.FindByFullPath(path, -1);
                if (idx < 0)
                {
                    var layer = new Layer { Name = part, Color = col };
                    if (parentId != Guid.Empty) layer.ParentLayerId = parentId;
                    idx = doc.Layers.Add(layer);
                    if (idx < 0) return -1;
                }
                parentId = doc.Layers[idx].Id;
            }
        }
        Layer l = doc.Layers[idx];
        if (!l.IsVisible || l.IsLocked)
        {
            l.IsVisible = true;
            l.IsLocked = false;
            l.CommitChanges();
        }
        return idx;
    }

    private T SafeIndex<T>(List<T> list, int i, T def)
    {
        if (list == null || list.Count == 0) return def;
        T v = list[i % list.Count];
        if (v == null || (v is string && string.IsNullOrWhiteSpace(v as string))) return def;
        return v;
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
