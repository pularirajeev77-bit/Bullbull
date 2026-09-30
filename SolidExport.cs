/*
  Author: Rajeev Pulari + Gemini
  Rhino 8 | Grasshopper C#
  Version: 2026.09.30
  Component: DWG/DXF Solid Exporter v3.2
  Description: One DWG/DXF file per tree branch. Temporarily bakes solids, points
               and annotations, exports them with the "2018 Solid" export scheme
               (so breps go out as ACIS solids, not wireframe), then removes them.
               Exports ONCE per press.
*/

#region Usings
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Rhino;
using Rhino.Geometry;
using Rhino.DocObjects;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

using Color = System.Drawing.Color;
#endregion

public class Script_Instance : GH_ScriptInstance
{
    // Export, FileType, Folder : item | FileNames, Solids, Points, Annotations : tree
    // AnnotationLayers, AnnotationColors, LayerNames, LayerColors : list (item i = file i, cycles)
    private void RunScript(
        bool Export,
        string FileType,
        DataTree<string> FileNames,
        string Folder,
        DataTree<Brep> Solids,
        DataTree<Point3d> Points,
        DataTree<GeometryBase> Annotations,
        List<string> AnnotationLayers,
        List<Color> AnnotationColors,
        List<string> LayerNames,
        List<Color> LayerColors,
        ref object ExportedFiles)
    {
        // Metadata + pin tooltips (once)
        if (Component != null && Component.Name != "DWG/DXF Solid Exporter")
        {
            Component.Name = "DWG/DXF Solid Exporter";
            Component.NickName = "SolidExport";
            Component.Message = "Solid Scheme v3.2";
            Component.Description = "Exports one DWG/DXF per branch using the '" + SCHEME + "' scheme, so breps stay solids.";

            var pi = Component.Params.Input;
            SetTip(pi, "Export", "Exports once each time this goes from False to True. Use a Button.");
            SetTip(pi, "FileType", "DWG or DXF (empty = DWG).");
            SetTip(pi, "FileNames", "One file name per branch (first item of each branch, no extension). Existing files are overwritten.");
            SetTip(pi, "Folder", "Existing folder to save into.");
            SetTip(pi, "Solids", "Breps per file (branch i -> file i). Open planar holes are capped.");
            SetTip(pi, "Points", "Points per file (branch i -> file i), on the same layer as the solids.");
            SetTip(pi, "Annotations", "Text, leaders, dimensions or curves per file (branch i -> file i).");
            SetTip(pi, "AnnotationLayers", "Annotation layer per file (cycles). Empty = Annotations.");
            SetTip(pi, "AnnotationColors", "Annotation colour per file (cycles). Empty = black.");
            SetTip(pi, "LayerNames", "Solid/point layer per file (cycles), nested as Parent::Child OK. Empty = Solid_Geometry.");
            SetTip(pi, "LayerColors", "Solid/point colour per file (cycles). Empty = black.");
            SetTip(Component.Params.Output, "ExportedFiles", "Full paths of the files written by the last press.");
        }

        ExportedFiles = _lastFiles;

        // Export only on the rising edge (a Toggle left on would re-export on every recompute)
        bool trigger = Export && !_wasPressed;
        _wasPressed = Export;
        if (!trigger)
        {
            if (!Export) Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Idle. Set 'Export' to True.");
            return;
        }

        // 1. Checks
        string ext = string.IsNullOrWhiteSpace(FileType) ? "dwg" : FileType.Trim().TrimStart('.').ToLowerInvariant();
        if (ext != "dwg" && ext != "dxf") { Warn(GH_RuntimeMessageLevel.Error, "FileType must be DWG or DXF."); return; }
        if (string.IsNullOrWhiteSpace(Folder) || !Directory.Exists(Folder)) { Warn(GH_RuntimeMessageLevel.Error, "Folder does not exist."); return; }
        if (FileNames == null || FileNames.BranchCount == 0) { Warn(GH_RuntimeMessageLevel.Warning, "No FileNames supplied."); return; }

        RhinoDoc doc = RhinoDocument ?? RhinoDoc.ActiveDoc;
        if (doc == null) { Warn(GH_RuntimeMessageLevel.Error, "No active Rhino document."); return; }

        var results = new List<string>();
        var previousSelection = doc.Objects.GetSelectedObjects(false, false).Select(o => o.Id).ToList();

        // 2. One file per branch
        for (int i = 0; i < FileNames.BranchCount; i++)
        {
            var nameBranch = FileNames.Branch(i);
            if (nameBranch == null || nameBranch.Count == 0 || string.IsNullOrWhiteSpace(nameBranch[0])) continue;

            string fileName = nameBranch[0].Trim();
            if (fileName.EndsWith("." + ext, StringComparison.OrdinalIgnoreCase))
                fileName = fileName.Substring(0, fileName.Length - ext.Length - 1);
            if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                Warn(GH_RuntimeMessageLevel.Warning, "Skipped '" + fileName + "': invalid characters in file name.");
                continue;
            }

            var bakedIds = new List<Guid>();
            try
            {
                string layer = GetValue(LayerNames, i, "Solid_Geometry");
                Color color = GetValue(LayerColors, i, Color.Black);

                if (Solids != null && i < Solids.BranchCount)
                    bakedIds.AddRange(BakeAndRepairBreps(Solids.Branch(i), layer, color, doc, fileName));
                if (Points != null && i < Points.BranchCount)
                    bakedIds.AddRange(BakePoints(Points.Branch(i), layer, color, doc));
                if (Annotations != null && i < Annotations.BranchCount)
                    bakedIds.AddRange(BakeAnnotations(Annotations.Branch(i),
                        GetValue(AnnotationLayers, i, "Annotations"), GetValue(AnnotationColors, i, Color.Black), doc));

                if (bakedIds.Count == 0)
                {
                    Warn(GH_RuntimeMessageLevel.Remark, "'" + fileName + "': nothing to export.");
                    continue;
                }

                // 3. Export only the temporary objects, with the solid scheme
                doc.Objects.UnselectAll();
                doc.Objects.Select(bakedIds, true);

                string savePath = Path.Combine(Folder, fileName + "." + ext);
                // Rhino asks "replace?" for an existing file, which would break the scripted command
                if (File.Exists(savePath)) File.Delete(savePath);

                // -Export <path>, set the Scheme option, then Enter to write the file
                string cmd = string.Format("-_Export \"{0}\" _Scheme \"{1}\" _Enter _Enter", savePath, SCHEME);
                bool ran = RhinoApp.RunScript(cmd, false);

                if (ran && File.Exists(savePath)) results.Add(savePath);
                else Warn(GH_RuntimeMessageLevel.Error,
                    "Failed: " + fileName + ". Check that the '" + SCHEME + "' export scheme exists in Rhino.");
            }
            catch (Exception ex)
            {
                Warn(GH_RuntimeMessageLevel.Error, "'" + fileName + "': " + ex.Message);
            }
            finally
            {
                // Always remove the temporary objects, even if the export failed
                foreach (Guid id in bakedIds) doc.Objects.Delete(id, true);
            }
        }

        doc.Objects.UnselectAll();
        if (previousSelection.Count > 0) doc.Objects.Select(previousSelection, true);
        doc.Views.Redraw();

        _lastFiles = results;
        ExportedFiles = results;
        Warn(GH_RuntimeMessageLevel.Remark, "Exported " + results.Count + " file(s).");
    }

    // <Custom additional code>
    private const string SCHEME = "2018 Solid";   // Rhino DWG/DXF export scheme name
    private bool _wasPressed = false;
    private List<string> _lastFiles = new List<string>();

    private void Warn(GH_RuntimeMessageLevel level, string msg)
    {
        if (Component != null) Component.AddRuntimeMessage(level, msg);
    }

    private ObjectAttributes Attr(RhinoDoc doc, int layerIdx, Color color)
    {
        var a = doc.CreateDefaultAttributes();
        if (layerIdx >= 0) a.LayerIndex = layerIdx;
        a.ColorSource = ObjectColorSource.ColorFromObject;
        a.ObjectColor = color;
        return a;
    }

    private List<Guid> BakeAndRepairBreps(List<Brep> input, string layerName, Color color, RhinoDoc doc, string fileName)
    {
        var ids = new List<Guid>();
        var attr = Attr(doc, EnsureLayer(layerName, color, doc), color);
        double tol = doc.ModelAbsoluteTolerance;
        int open = 0;

        foreach (Brep b in input)
        {
            if (b == null) continue;
            Brep copy = b.DuplicateBrep();

            // Cap planar holes so it exports as a closed ACIS solid
            if (!copy.IsSolid)
            {
                Brep capped = copy.CapPlanarHoles(tol);
                if (capped != null) copy = capped;
            }
            copy.Repair(tol);

            Guid id = doc.Objects.AddBrep(copy, attr);
            if (id != Guid.Empty)
            {
                ids.Add(id);
                if (!copy.IsSolid) open++;
            }
        }
        if (open > 0)
            Warn(GH_RuntimeMessageLevel.Warning, "'" + fileName + "': " + open + " brep(s) are not closed solids (exported as surfaces).");
        return ids;
    }

    private List<Guid> BakePoints(List<Point3d> input, string layerName, Color color, RhinoDoc doc)
    {
        var ids = new List<Guid>();
        var attr = Attr(doc, EnsureLayer(layerName, color, doc), color);
        foreach (Point3d p in input)
        {
            if (!p.IsValid) continue;
            Guid id = doc.Objects.AddPoint(p, attr);
            if (id != Guid.Empty) ids.Add(id);
        }
        return ids;
    }

    private List<Guid> BakeAnnotations(List<GeometryBase> input, string layerName, Color color, RhinoDoc doc)
    {
        var ids = new List<Guid>();
        var attr = Attr(doc, EnsureLayer(layerName, color, doc), color);
        foreach (var geo in input)
        {
            if (geo == null) continue;
            // Text, leaders, dimensions, curves ... (Add handles every geometry type)
            Guid id = doc.Objects.Add(geo, attr);
            if (id != Guid.Empty) ids.Add(id);
        }
        return ids;
    }

    // Finds or creates a layer ("Parent::Child" OK) and makes it visible + unlocked,
    // otherwise its objects cannot be selected and nothing is exported.
    private int EnsureLayer(string fullPath, Color color, RhinoDoc doc)
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
                    var layer = new Layer { Name = part, Color = color };
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

    private T GetValue<T>(List<T> list, int index, T fallback)
    {
        if (list == null || list.Count == 0) return fallback;
        T v = list[index % list.Count];
        if (v == null || (v is string && string.IsNullOrWhiteSpace(v as string))) return fallback;
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
