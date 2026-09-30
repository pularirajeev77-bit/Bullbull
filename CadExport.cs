/*
  Rhino 8 | Grasshopper C#
  Component: CadExport v2.1
  Description: Temporarily bakes points, curves and text onto one layer, exports
               them to a DWG or DXF file, then removes the temporary objects.
               Exports ONCE per press.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

public class Script_Instance : GH_ScriptInstance
{
    // Export, FileType, Folder, FileName, LayerName, LayerColor, TextSize : item access
    // Points, Curves, TextPoints, Texts : list access
    private void RunScript(
        bool Export,
        string FileType,
        string Folder,
        string FileName,
        string LayerName,
        object LayerColor,
        List<object> Points,
        List<object> Curves,
        List<object> TextPoints,
        List<string> Texts,
        double TextSize,
        ref object Result)
    {
        // Metadata + pin tooltips (once)
        if (Component != null && Component.Name != "CadExport")
        {
            Component.Name = "CadExport";
            Component.NickName = "CadExport";
            Component.Message = "CadExport v2.1";
            Component.Description = "Exports points, curves and text to a DWG/DXF file on one layer, without leaving objects in the model.";

            var pi = Component.Params.Input;
            SetTip(pi, "Export", "Exports once each time this goes from False to True. Use a Button.");
            SetTip(pi, "FileType", "DWG or DXF.");
            SetTip(pi, "Folder", "Existing folder to save into.");
            SetTip(pi, "FileName", "File name without extension (an existing file is overwritten).");
            SetTip(pi, "LayerName", "Layer for the exported objects (nested as Parent::Child). Empty = CadExport.");
            SetTip(pi, "LayerColor", "Layer colour. Empty = black.");
            SetTip(pi, "Points", "Points to export.");
            SetTip(pi, "Curves", "Curves / lines / polylines to export.");
            SetTip(pi, "TextPoints", "Insertion point for each text (centred).");
            SetTip(pi, "Texts", "Text strings, paired with TextPoints by index.");
            SetTip(pi, "TextSize", "Text height. 0 or less = no text exported.");
            SetTip(Component.Params.Output, "Result", "Status of the last export.");
        }

        // Keep showing the last status between presses
        Result = _lastResult;

        // Export only on the rising edge (a Toggle left on would re-export on every recompute)
        bool trigger = Export && !_wasPressed;
        _wasPressed = Export;
        if (!trigger)
        {
            if (!Export) Result = _lastResult ?? "Idle. Set 'Export' to True.";
            return;
        }

        _lastResult = DoExport(FileType, Folder, FileName, LayerName, LayerColor, Points, Curves, TextPoints, Texts, TextSize);
        Result = _lastResult;
    }

    // <Custom additional code>
    private bool _wasPressed = false;
    private string _lastResult = null;

    private string DoExport(string fileType, string folder, string fileName, string layerName, object layerColor,
        List<object> points, List<object> curves, List<object> textPoints, List<string> texts, double textSize)
    {
        // 1. Checks
        string ext = (fileType ?? "").Trim().TrimStart('.').ToUpperInvariant();
        if (ext != "DWG" && ext != "DXF") return Fail("FileType must be DWG or DXF.");
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return Fail("Folder does not exist.");

        string name = (fileName ?? "").Trim();
        if (name.EndsWith("." + ext, StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - ext.Length - 1);
        if (name.Length == 0) return Fail("FileName is empty.");
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return Fail("FileName contains invalid characters.");
        string fullPath = Path.Combine(folder, name + "." + ext.ToLowerInvariant());

        RhinoDoc doc = RhinoDocument ?? RhinoDoc.ActiveDoc;
        if (doc == null) return Fail("No active Rhino document.");

        var tempIds = new List<Guid>();
        var previousSelection = doc.Objects.GetSelectedObjects(false, false).Select(o => o.Id).ToList();
        try
        {
            // 2. Layer (created if missing; must be visible and unlocked to be selectable)
            Color color = Color.Black;
            if (layerColor != null) GH_Convert.ToColor(layerColor, out color, GH_Conversion.Both);

            int layerIndex = EnsureLayer(doc, string.IsNullOrWhiteSpace(layerName) ? "CadExport" : layerName.Trim(), color);
            if (layerIndex < 0) return Fail("Invalid LayerName.");
            Layer lyr = doc.Layers[layerIndex];
            lyr.Color = color;
            lyr.IsVisible = true;
            lyr.IsLocked = false;
            lyr.CommitChanges();

            var attr = doc.CreateDefaultAttributes();
            attr.LayerIndex = layerIndex;

            // 3. Points
            int skipped = 0;
            if (points != null)
                foreach (var item in points)
                {
                    Point3d pt;
                    if (item != null && GH_Convert.ToPoint3d(Unwrap(item), ref pt, GH_Conversion.Both))
                        tempIds.Add(doc.Objects.AddPoint(pt, attr));
                    else if (item != null) skipped++;
                }

            // 4. Curves (Curve, Line, Polyline, Arc, Circle, Rectangle ...)
            if (curves != null)
                foreach (var item in curves)
                {
                    Curve crv = null;
                    if (item != null && GH_Convert.ToCurve(Unwrap(item), ref crv, GH_Conversion.Both) && crv != null)
                        tempIds.Add(doc.Objects.AddCurve(crv, attr));
                    else if (item != null) skipped++;
                }

            // 5. Text
            if (texts != null && textPoints != null && textSize > 0)
            {
                if (texts.Count != textPoints.Count)
                    Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        "Texts (" + texts.Count + ") and TextPoints (" + textPoints.Count + ") differ; extra items ignored.");

                int count = Math.Min(texts.Count, textPoints.Count);
                for (int i = 0; i < count; i++)
                {
                    Point3d loc = Point3d.Unset;
                    if (textPoints[i] == null || string.IsNullOrEmpty(texts[i])) continue;
                    if (!GH_Convert.ToPoint3d(Unwrap(textPoints[i]), ref loc, GH_Conversion.Both)) { skipped++; continue; }

                    var te = TextEntity.Create(texts[i], new Plane(loc, Vector3d.ZAxis),
                        doc.DimStyles.Current, false, 0, 0);
                    te.TextHeight = textSize;
                    te.TextHorizontalAlignment = TextHorizontalAlignment.Center;
                    te.TextVerticalAlignment = TextVerticalAlignment.Middle;
                    tempIds.Add(doc.Objects.AddText(te, attr));
                }
            }

            tempIds.RemoveAll(id => id == Guid.Empty);
            if (skipped > 0)
                Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, skipped + " item(s) could not be converted and were skipped.");
            if (tempIds.Count == 0) return Fail("No geometry to export.");

            // 6. Export only the temporary objects
            doc.Objects.UnselectAll();
            doc.Objects.Select(tempIds, true);

            // Rhino asks "replace?" for an existing file, which would break the scripted command
            if (File.Exists(fullPath)) File.Delete(fullPath);

            string cmd = "-_Export \"" + fullPath + "\" _Enter";
            bool ran = RhinoApp.RunScript(cmd, false);

            if (ran && File.Exists(fullPath))
            {
                Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Exported " + tempIds.Count + " object(s).");
                return "Exported " + tempIds.Count + " object(s): " + fullPath;
            }
            return Fail("Export failed (file was not written).");
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
        }
        finally
        {
            // 7. Always remove the temporary objects and restore the user's selection
            foreach (Guid id in tempIds) doc.Objects.Delete(id, true);
            doc.Objects.UnselectAll();
            if (previousSelection.Count > 0) doc.Objects.Select(previousSelection, true);
            doc.Views.Redraw();
        }
    }

    private string Fail(string message)
    {
        Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, message);
        return "Error: " + message;
    }

    private object Unwrap(object o)
    {
        var goo = o as IGH_Goo;
        return goo != null ? goo.ScriptVariable() : o;
    }

    // Finds or creates a layer; supports "Parent::Child". Returns -1 if a name is invalid.
    private int EnsureLayer(RhinoDoc doc, string fullPath, Color color)
    {
        int existing = doc.Layers.FindByFullPath(fullPath, -1);
        if (existing >= 0) return existing;

        Guid parentId = Guid.Empty;
        string path = "";
        int idx = -1;
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
        return idx;
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
