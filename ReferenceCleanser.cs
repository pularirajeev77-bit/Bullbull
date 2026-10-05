/*
  Component : Global Reference Cleanser  (nickname: Cleanse)
  Author    : Rajeev Pulari
  Version   : 2.1  (2026-10-05)
  Platform  : Rhino 8 | Grasshopper C# Script

  Purpose
  -------
  Removes every Rhino-REFERENCED geometry ("Set one / Set multiple ...") from the
  parameters of this Grasshopper file in one click - e.g. before handing the file
  over, or to re-reference on a new Rhino model. Wired inputs (live data) and
  internalised geometry are left alone. One Ctrl+Z restores what was cleared.

  Inputs
  ------
  Run     (item) Connect a Button. Cleans once per press.

  Outputs
  -------
  Status            Summary of the last clean.
  ClearedParameters "Component > Parameter" for every parameter that was cleared.
  SkippedMixed      Parameters holding BOTH referenced and internalised data -
                    left untouched so internalised data is never lost.

  v2.1 changes
  ------------
  - Parameters were expired DURING this component's solve, which Grasshopper
    does not allow (solution already running) - the cleared inputs often did not
    update. The clean now runs in a ScheduleSolution callback, where expiring is
    allowed, and this component refreshes itself afterwards.
  - Ran on every solve while Run was True (Toggle) -> once per press.
  - PersistentData was read with reflection + IEnumerable, which a GH_Structure is
    not reliably; now read through IGH_Structure (AllData / Clear).
  - A parameter with BOTH referenced and internalised items was wiped entirely,
    losing the internalised data; now skipped and listed in SkippedMixed.
  - No undo: an undo record is now made per parameter (Ctrl+Z restores).
  - Component identity set once; tooltips; new ClearedParameters / SkippedMixed.

  Note: parameters inside clusters are not searched.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

public class Script_Instance : GH_ScriptInstance
{
    private bool _metaSet = false;
    private bool _wasPressed = false;
    private bool _pending = false;

    private string _status = "Ready - press Run (Button) to clear referenced geometry.";
    private List<string> _cleared = new List<string>();
    private List<string> _mixed = new List<string>();

    private void RunScript(
		bool Run,
		ref object Status,
		ref object ClearedParameters,
		ref object SkippedMixed)
    {
        SetMetadata();

        bool pressed = Run && !_wasPressed;
        _wasPressed = Run;

        if (pressed && !_pending)
        {
            _pending = true;
            _status = "Clearing...";
            GH_Document ghDoc = GrasshopperDocument;
            IGH_Component self = Component;

            // Expiring other objects is only allowed outside a running solution,
            // so the clean happens in the callback before the next solution.
            ghDoc.ScheduleSolution(5, doc =>
            {
                Cleanse(doc);
                _pending = false;
                self.ExpireSolution(false);   // refresh our own outputs
            });
        }

        Component.Message = _pending ? "Clearing..." : "Cleared: " + _cleared.Count;
        Status = _status;
        ClearedParameters = _cleared;
        SkippedMixed = _mixed;
    }

    // ------------------------------------------------------------------ core

    private void Cleanse(GH_Document doc)
    {
        _cleared = new List<string>();
        _mixed = new List<string>();
        int kept = 0;

        foreach (IGH_DocumentObject obj in doc.Objects)
        {
            if (obj is IGH_Param param)
            {
                ProcessParam(param, param.NickName, ref kept);
            }
            else if (obj is IGH_Component comp)
            {
                if (comp.InstanceGuid == Component.InstanceGuid) continue;   // not ourselves
                foreach (IGH_Param input in comp.Params.Input)
                    ProcessParam(input, comp.NickName + " > " + input.NickName, ref kept);
            }
        }

        _status = "Cleared referenced geometry from " + _cleared.Count + " parameter(s). " +
                  "Kept " + kept + " internalised/non-geometry parameter(s)" +
                  (_mixed.Count > 0 ? ", skipped " + _mixed.Count + " mixed parameter(s) (see SkippedMixed)." : ".") +
                  (_cleared.Count > 0 ? " Ctrl+Z to undo." : "");
    }

    private void ProcessParam(IGH_Param p, string label, ref int kept)
    {
        if (p == null || p.SourceCount > 0) return;     // wired: live data, leave it

        try
        {
            var prop = p.GetType().GetProperty("PersistentData");
            if (prop == null) return;
            IGH_Structure data = prop.GetValue(p, null) as IGH_Structure;
            if (data == null || data.DataCount == 0) return;

            int referenced = 0, other = 0;
            foreach (IGH_Goo goo in data.AllData(true))
            {
                IGH_GeometricGoo geo = goo as IGH_GeometricGoo;
                if (geo != null && geo.IsReferencedGeometry) referenced++;
                else other++;
            }

            if (referenced == 0) { kept++; return; }
            if (other > 0) { _mixed.Add(label + " (" + referenced + " ref, " + other + " other)"); return; }

            p.RecordUndoEvent("Cleanse references");
            data.Clear();
            p.ExpireSolution(false);
            _cleared.Add(label + " (" + referenced + ")");
        }
        catch
        {
            // locked / unusual parameter types: leave them alone
        }
    }

    // -------------------------------------------------------------- metadata

    private void SetMetadata()
    {
        if (_metaSet) return;
        _metaSet = true;

        Component.Name = "Global Reference Cleanser";
        Component.NickName = "Cleanse";
        Component.Description =
            "Clears all Rhino-referenced geometry from unwired parameters in this Grasshopper file " +
            "(wired and internalised data are kept). Ctrl+Z restores. Rajeev Pulari, v2.1.";

        SetTip(Component.Params.Input, "Run",
            "Connect a Button. Clears referenced geometry once per press.");
        SetTip(Component.Params.Output, "Status",
            "Summary of the last clean.");
        SetTip(Component.Params.Output, "ClearedParameters",
            "'Component > Parameter (count)' for every parameter that was cleared.");
        SetTip(Component.Params.Output, "SkippedMixed",
            "Parameters holding both referenced and internalised data - left untouched.");
    }

    // Match pins by Name (the script variable), fall back to NickName.
    // Only NickName/Description are changed - never Name.
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
}
