/*
  Author: Rajeev Pulari + Gemini
  Rhino 8 | Grasshopper C#
  Version: 2025.08.12
  Component: Current Layer Setter
  Purpose: Sets the current Rhino layer by name, creating it if it doesn't exist.
           Supports nested layers via "Parent::Child".
*/

using System;
using System.Collections;
using System.Collections.Generic;

using Rhino;
using Rhino.DocObjects;
using Rhino.Commands;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

public class Script_Instance : GH_ScriptInstance
{
  private void RunScript(string LayerName, bool Run, ref object Msg)
  {
    // --- Metadata + pin tooltips (once) ---
    if (this.Component != null && this.Component.Name != "Current Layer Setter")
    {
      this.Component.Name = "Current Layer Setter";
      this.Component.NickName = "CurrLyr";
      this.Component.Message = "Current Layer v1.0";
      this.Component.Description = "Sets the current Rhino layer by name, creating it (and any parents) if needed. Runs only when Run is True.";

      SetTip(this.Component.Params.Input, 0, "LayerName",
        "Name of the layer to make current. Use 'Parent::Child' for a sub-layer; missing layers are created.");
      SetTip(this.Component.Params.Input, 1, "Run",
        "Set True to apply. False = do nothing.");
      SetTip(this.Component.Params.Output, 0, "Msg",
        "What happened: which layer was set, whether it was created, or an error.");
    }

    string message = "";

    // --- Safety checks ---
    if (!Run)
    {
      Msg = "Set 'Run' = True to execute.";
      return;
    }

    if (string.IsNullOrWhiteSpace(LayerName))
    {
      Msg = "Error: 'LayerName' is empty. Please provide a valid layer name.";
      this.Component?.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "LayerName is empty.");
      return;
    }

    try
    {
      var doc = RhinoDoc.ActiveDoc;
      if (doc == null)
      {
        Msg = "Error: No active Rhino document found.";
        this.Component?.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No active Rhino document.");
        return;
      }

      // Find or create the layer (handles nested "Parent::Child" paths).
      // The original used doc.Layers.Find(name, true) + Add(name, color),
      // which (a) is a deprecated lookup and (b) for "A::B" created one flat
      // layer literally named "A::B" instead of a real sub-layer.
      bool created;
      int layerIndex = GetOrCreateLayer(doc, LayerName.Trim(), out created);

      if (layerIndex < 0)
      {
        Msg = "Error: could not find or create layer '" + LayerName + "'.";
        this.Component?.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Could not create the layer.");
        return;
      }

      if (created) message += "Created new layer: " + LayerName + ". ";

      // --- Set as current ---
      bool success = doc.Layers.SetCurrentLayerIndex(layerIndex, true);
      doc.Views.Redraw();

      var current = doc.Layers.CurrentLayer;
      if (success)
        message += "Current layer set to: " + current.FullPath;
      else
      {
        message += "Failed to set layer. Current layer remains: " + current.FullPath;
        this.Component?.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "SetCurrentLayerIndex failed.");
      }
    }
    catch (Exception ex)
    {
      message = "Error setting layer: " + ex.Message;
      this.Component?.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, message);
    }

    Msg = message;
  }

  // Finds a layer by full path, creating each missing level as a real
  // (nested) layer. Returns the layer index, or -1 on failure.
  private int GetOrCreateLayer(RhinoDoc doc, string fullPath, out bool createdAny)
  {
    createdAny = false;

    string[] parts = fullPath.Split(new string[] { "::" }, StringSplitOptions.None);
    Guid parentId = Guid.Empty;
    int idx = -1;
    string cumulative = "";

    for (int i = 0; i < parts.Length; i++)
    {
      string name = parts[i].Trim();
      if (name.Length == 0) return -1; // empty segment, e.g. "A::" or "::B"

      cumulative = (i == 0) ? name : cumulative + "::" + name;

      int found = doc.Layers.FindByFullPath(cumulative, -1);
      if (found >= 0)
      {
        idx = found;
        parentId = doc.Layers[found].Id;
        continue;
      }

      var layer = new Layer();
      layer.Name = name;
      layer.Color = System.Drawing.Color.White;
      if (parentId != Guid.Empty) layer.ParentLayerId = parentId;

      idx = doc.Layers.Add(layer);
      if (idx < 0) return -1;

      createdAny = true;
      parentId = doc.Layers[idx].Id;
    }

    return idx;
  }

  private void SetTip(System.Collections.Generic.IList<IGH_Param> ps, int i, string name, string tip)
  {
    // Pins are matched by NAME (the script variable), not by position 'i':
    // Rhino 8 script components can have an extra "out" pin first, which
    // shifted position-based tooltips onto the wrong pins. Name is never
    // changed (it is the script variable) - only NickName and Description.
    if (ps == null) return;
    IGH_Param hit = null;
    foreach (IGH_Param p in ps)
      if (string.Equals(p.Name, name, System.StringComparison.OrdinalIgnoreCase)) { hit = p; break; }
    if (hit == null)
      foreach (IGH_Param p in ps)
        if (string.Equals(p.NickName, name, System.StringComparison.OrdinalIgnoreCase)) { hit = p; break; }
    if (hit == null) return;
    hit.NickName = name;
    hit.Description = tip;
  }
}
