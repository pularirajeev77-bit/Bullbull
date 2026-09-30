/*
  Rhino 8 | Grasshopper C#
  Component: Bake_Pro v2.1
  Description: Bakes geometry to the Rhino document with per-object name, layer
               (nested "Parent::Child" supported), colour, print width and
               isocurve density, optionally grouped. Bakes ONCE per press.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

using Rhino;
using Rhino.Geometry;
using Rhino.DocObjects;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

public class Script_Instance : GH_ScriptInstance
{
    // Geometry, Names, Layers, Colors, PrintWidths, WireDensity : list access
    // Bake, Group : item access
    // Per-object lists cycle (item i uses list[i % count]), so one value applies to all.
    private void RunScript(
        List<object> Geometry,
        List<string> Names,
        List<string> Layers,
        List<Color> Colors,
        List<double> PrintWidths,
        List<int> WireDensity,
        bool Bake,
        bool Group,
        ref object ObjectIds)
    {
        // Metadata + pin tooltips (once)
        if (Component != null && Component.Name != "Bake_Pro")
        {
            Component.Name = "Bake_Pro";
            Component.NickName = "Bake";
            Component.Message = "Bake_Pro v2.1";
            Component.Description = "Bakes geometry with name, layer, colour, print width and isocurve density; optional grouping.";

            var pi = Component.Params.Input;
            SetTip(pi, "Geometry", "Geometry to bake (curves, breps, meshes, points, lines, circles, boxes...).");
            SetTip(pi, "Names", "Object names. Cycles if shorter than Geometry. Optional.");
            SetTip(pi, "Layers", "Layer per object; nested as Parent::Child. Missing layers are created. Optional.");
            SetTip(pi, "Colors", "Object colour (sets colour 'by object'). Optional.");
            SetTip(pi, "PrintWidths", "Print width in mm (sets print width 'by object'). 0 = default, -1 = no print. Optional.");
            SetTip(pi, "WireDensity", "Isocurve density for surfaces/breps (-1 hides, 0 = edges only, 1 = default). Optional.");
            SetTip(pi, "Bake", "Bakes once each time this goes from False to True. Use a Button.");
            SetTip(pi, "Group", "True = put everything baked in this press into one group.");
            SetTip(Component.Params.Output, "ObjectIds", "IDs of the objects baked by the last press.");
        }

        // Keep showing the last result between presses
        ObjectIds = _lastIds;

        if (Geometry == null || Geometry.Count == 0)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Input 'Geometry' is empty. Provide geometry to bake.");
            _wasPressed = Bake;
            return;
        }

        // Bake only on the rising edge: a Toggle left on would otherwise bake a new copy on every recompute
        bool trigger = Bake && !_wasPressed;
        _wasPressed = Bake;
        if (!trigger)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                Bake ? "Baked. Set Bake to False and back to True to bake again." : "Waiting for Bake = True.");
            return;
        }

        RhinoDoc doc = RhinoDocument ?? RhinoDoc.ActiveDoc;
        if (doc == null)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No active Rhino document.");
            return;
        }

        var ids = new List<Guid>();
        uint undo = doc.BeginUndoRecord("Bake_Pro");
        try
        {
            for (int i = 0; i < Geometry.Count; i++)
            {
                object obj = Geometry[i];
                if (obj == null) continue;

                var att = doc.CreateDefaultAttributes();

                if (Names != null && Names.Count > 0 && !string.IsNullOrEmpty(Names[i % Names.Count]))
                    att.Name = Names[i % Names.Count];

                if (Colors != null && Colors.Count > 0)
                {
                    att.ColorSource = ObjectColorSource.ColorFromObject;
                    att.ObjectColor = Colors[i % Colors.Count];
                }

                if (Layers != null && Layers.Count > 0)
                {
                    int lIdx = EnsureLayer(doc, Layers[i % Layers.Count]);
                    if (lIdx >= 0) att.LayerIndex = lIdx;
                    else Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        "Invalid layer name '" + Layers[i % Layers.Count] + "'; current layer used.");
                }

                if (PrintWidths != null && PrintWidths.Count > 0)
                {
                    att.PlotWeightSource = ObjectPlotWeightSource.PlotWeightFromObject;
                    att.PlotWeight = PrintWidths[i % PrintWidths.Count];
                }

                if (WireDensity != null && WireDensity.Count > 0)
                    att.WireDensity = WireDensity[i % WireDensity.Count];

                BakeItem(doc, obj, att, ids);
            }

            if (Group && ids.Count > 0)
            {
                int g = doc.Groups.Add();
                doc.Groups.AddToGroup(g, ids);
            }
        }
        finally
        {
            doc.EndUndoRecord(undo);
        }

        doc.Views.Redraw();
        _lastIds = ids;
        ObjectIds = ids;
        Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
            "Baked " + ids.Count + " object(s)" + (Group && ids.Count > 0 ? " into one group." : "."));
    }

    // <Custom additional code>
    private bool _wasPressed = false;
    private List<Guid> _lastIds = new List<Guid>();

    private void BakeItem(RhinoDoc doc, object obj, ObjectAttributes att, List<Guid> ids)
    {
        if (obj == null) return;

        // Unwrap Grasshopper wrappers (GH_Curve, GH_Point, ...)
        var goo = obj as IGH_Goo;
        if (goo != null) obj = goo.ScriptVariable();
        if (obj == null) return;

        Guid id = Guid.Empty;
        if (obj is GeometryBase)
            id = doc.Objects.Add((GeometryBase)obj, att);
        else if (obj is Point3d) id = doc.Objects.AddPoint((Point3d)obj, att);
        else if (obj is Line) id = doc.Objects.AddLine((Line)obj, att);
        else if (obj is Circle) id = doc.Objects.AddCircle((Circle)obj, att);
        else if (obj is Arc) id = doc.Objects.AddArc((Arc)obj, att);
        else if (obj is Polyline) id = doc.Objects.AddPolyline((Polyline)obj, att);
        else if (obj is Rectangle3d) id = doc.Objects.AddCurve(((Rectangle3d)obj).ToNurbsCurve(), att);
        else if (obj is Box) id = doc.Objects.AddBrep(((Box)obj).ToBrep(), att);
        else if (obj is Sphere) id = doc.Objects.AddBrep(((Sphere)obj).ToBrep(), att);
        else if (obj is IEnumerable && !(obj is string))
        {
            foreach (var item in (IEnumerable)obj) BakeItem(doc, item, att, ids);
            return;
        }
        else
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                "Skipped unsupported type: " + obj.GetType().Name);
            return;
        }

        if (id != Guid.Empty) ids.Add(id);
    }

    // Finds or creates a layer; supports "Parent::Child::Grandchild". Returns -1 if a name is invalid.
    private int EnsureLayer(RhinoDoc doc, string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath)) return -1;
        int existing = doc.Layers.FindByFullPath(fullPath.Trim(), -1);
        if (existing >= 0) return existing;

        string[] parts = fullPath.Split(new[] { "::" }, StringSplitOptions.None);
        Guid parentId = Guid.Empty;
        string path = "";
        int idx = -1;
        foreach (string raw in parts)
        {
            string part = raw.Trim();
            if (!Layer.IsValidName(part)) return -1;
            path = (path.Length == 0) ? part : path + "::" + part;
            idx = doc.Layers.FindByFullPath(path, -1);
            if (idx < 0)
            {
                var layer = new Layer { Name = part, Color = Color.Black };
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
