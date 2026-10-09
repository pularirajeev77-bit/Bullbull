using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using Rhino;
using Rhino.Geometry;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

public class Script_Instance : GH_ScriptInstance
{
  /*
    Members:
      RhinoDoc RhinoDocument
      GH_Document GrasshopperDocument
      IGH_Component Component
      int Iteration

    Methods (Virtual & overridable):
      Print(string text)
      Print(string format, params object[] args)
      Reflect(object obj)
      Reflect(object obj, string method_name)
  */

  private void RunScript(List<string> Stext, ref object Mtext)
  {
    SetPinTips();   // pin tooltips (set once, matched by name)

    // Set Component Metadata (Rhino 8 feature)
        Component.Message = "Single > Multi-Line-Text";
        Component.NickName = "Single > Multi-Line-Text";

    // Stext is declared List<string>, so Grasshopper always hands us a
    // list here (an unconnected list input defaults to empty, not a bare
    // string or null) -- the original "is IList" / "single string" /
    // "null" three-way split was dead code, since only the list branch
    // could ever actually run. Kept a defensive null-check on Stext
    // itself as cheap insurance, but dropped the unreachable branches.
    List<string> output = new List<string>();

    if (Stext != null)
    {
      foreach (string item in Stext)
      {
        if (item == null) continue;
        // If a list element is itself multi-line text, split it
        string[] parts = item.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None);
        output.AddRange(parts);
      }
    }

    Mtext = output;
  }

  // ---------------------------------------------------------------- pin tooltips
  private bool _pinTipsSet = false;

  private void SetPinTips()
  {
    if (_pinTipsSet || Component == null) return;
    _pinTipsSet = true;
    TipPin(Component.Params.Input, "Stext", "One or more texts that may contain line breaks.");
    TipPin(Component.Params.Output, "Mtext", "One item per line.");
  }

  // Match pins by Name (the script variable), fall back to NickName.
  // Only NickName/Description are changed - never Name.
  private void TipPin(System.Collections.Generic.IList<Grasshopper.Kernel.IGH_Param> ps, string name, string tip)
  {
    if (ps == null) return;
    Grasshopper.Kernel.IGH_Param hit = null;
    foreach (Grasshopper.Kernel.IGH_Param p in ps)
      if (string.Equals(p.Name, name, System.StringComparison.OrdinalIgnoreCase)) { hit = p; break; }
    if (hit == null)
      foreach (Grasshopper.Kernel.IGH_Param p in ps)
        if (string.Equals(p.NickName, name, System.StringComparison.OrdinalIgnoreCase)) { hit = p; break; }
    if (hit == null) return;
    hit.NickName = name;
    hit.Description = tip;
  }
}
