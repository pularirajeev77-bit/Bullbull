using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Rhino;
using Rhino.DocObjects;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

/// <summary>
/// Component: ChangeLayer
/// Version: 2025.08.12 (C# Port)
/// Author: rajeev pulari / Gemini
/// Moves all objects from a base layer onto a target layer, creating the
/// target (and any parents) if needed. Runs once on the rising edge of Run.
/// </summary>
public class Script_Instance : GH_ScriptInstance
{
    // Persistent per-component state: only fire on the rising edge of Run,
    // so re-solves caused by editing the document don't loop.
    private bool _lastRun = false;

    private void RunScript(
		bool Run,
		DataTree<string> BaseLayer,
		DataTree<string> TargetLayer,
		ref object status)
    {
        // --- Metadata + pin tooltips (once) ---
        if (this.Component != null && this.Component.Name != "Change Layer")
        {
            this.Component.Name = "Change Layer";
            this.Component.NickName = "CHL";
            this.Component.Message = "Change Layer v2.0";
            this.Component.Description = "Moves every object from a base layer onto a target layer, creating the target if needed. Fires once when Run turns True.";

            SetTip(this.Component.Params.Input, 0, "Run",
                "Toggle True to move objects. It fires once on the True edge; toggle back to False to arm it again.");
            SetTip(this.Component.Params.Input, 1, "BaseLayer",
                "Full path(s) of the layer(s) to move objects FROM (tree). Use 'Parent::Child' for sub-layers.");
            SetTip(this.Component.Params.Input, 2, "TargetLayer",
                "Full path(s) to move objects TO, matched to BaseLayer per branch. One target reuses it for the whole branch. Created if missing.");
            SetTip(this.Component.Params.Output, 0, "status",
                "What happened: working, done, idle, or an error/mismatch.");
        }

        // Only run on the rising edge of Run
        if (Run && !_lastRun)
        {
            status = "Working...";
            RhinoDoc doc = RhinoDoc.ActiveDoc;

            // Guard: the original used doc without a null check.
            if (doc == null)
            {
                status = "Error: no active Rhino document.";
                this.Component?.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No active Rhino document.");
                _lastRun = Run;
                return;
            }

            try
            {
                if (BaseLayer.BranchCount != TargetLayer.BranchCount)
                {
                    status = "Mismatched branch count.";
                    this.Component?.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, status);
                    _lastRun = Run;
                    return;
                }

                int moved = 0;

                for (int i = 0; i < BaseLayer.BranchCount; i++)
                {
                    GH_Path path = BaseLayer.Path(i);
                    var baseBranch = BaseLayer.Branch(path);

                    if (!TargetLayer.PathExists(path))
                    {
                        status = "Path " + path + " missing in TargetLayer.";
                        this.Component?.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, status);
                        _lastRun = Run;
                        return;
                    }
                    var targetBranch = TargetLayer.Branch(path);

                    for (int j = 0; j < baseBranch.Count; j++)
                    {
                        string bLayerName = baseBranch[j];

                        // One target -> reuse for the whole branch; else match by index
                        string tLayerName = (targetBranch.Count == 1)
                            ? targetBranch[0]
                            : (j < targetBranch.Count ? targetBranch[j] : null);

                        if (string.IsNullOrWhiteSpace(bLayerName) || tLayerName == null)
                        {
                            status = "Mismatched or empty item in branch " + path;
                            this.Component?.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, status);
                            _lastRun = Run;
                            return;
                        }

                        // 1. Ensure the target layer exists (nested-safe)
                        string fullTarget = EnsureLayer(doc, tLayerName);
                        int targetIdx = doc.Layers.FindByFullPath(fullTarget, RhinoMath.UnsetIntIndex);
                        if (targetIdx == RhinoMath.UnsetIntIndex) continue;

                        // 2. Objects on the base layer
                        int baseIdx = doc.Layers.FindByFullPath(bLayerName, RhinoMath.UnsetIntIndex);
                        if (baseIdx == RhinoMath.UnsetIntIndex)
                        {
                            // Base layer not found - note it but keep going
                            this.Component?.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Base layer not found: " + bLayerName);
                            continue;
                        }

                        RhinoObject[] objs = doc.Objects.FindByLayer(doc.Layers[baseIdx]);
                        if (objs == null) continue;

                        // 3. Move each object to the target layer
                        foreach (RhinoObject obj in objs)
                        {
                            obj.Attributes.LayerIndex = targetIdx;
                            obj.CommitChanges();
                            moved++;
                        }
                    }
                }

                doc.Views.Redraw();
                status = "Done! Moved " + moved + " object(s).";
            }
            catch (Exception ex)
            {
                status = "Error: " + ex.Message;
                this.Component?.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, status);
            }
        }
        else if (!Run)
        {
            status = "Idle. Toggle 'Run' to start.";
        }
        else
        {
            // Run is still True from a previous fire - already done
            status = "Already run. Toggle 'Run' off then on to repeat.";
        }

        // Update state
        _lastRun = Run;
    }

    // --- Helper Methods ---

    // Finds a layer by full path, creating each missing level as a real
    // (nested) layer. Returns the full path.
    private string EnsureLayer(RhinoDoc doc, string layerPath)
    {
        if (string.IsNullOrWhiteSpace(layerPath)) return string.Empty;

        string[] parts = layerPath.Split(new[] { "::" }, StringSplitOptions.None);
        string currentPath = "";
        int parentIdx = RhinoMath.UnsetIntIndex;

        for (int i = 0; i < parts.Length; i++)
        {
            string name = parts[i].Trim();
            if (name.Length == 0) return currentPath; // stop on an empty segment

            currentPath = (i == 0) ? name : currentPath + "::" + name;

            int idx = doc.Layers.FindByFullPath(currentPath, RhinoMath.UnsetIntIndex);

            if (idx == RhinoMath.UnsetIntIndex)
            {
                Layer newLayer = new Layer { Name = name };
                if (parentIdx != RhinoMath.UnsetIntIndex)
                    newLayer.ParentLayerId = doc.Layers[parentIdx].Id;
                parentIdx = doc.Layers.Add(newLayer);
            }
            else
            {
                parentIdx = idx;
            }
        }
        return currentPath;
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
