using System;
using System.Collections.Generic;

public class Script_Instance : GH_ScriptInstance
{
  private void RunScript(
		List<object> Pr,
		object Sn,
		object Ct,
		object Pn,
		List<object> Sf,
		ref object T) // Output list
  {
    SetPinTips();   // pin tooltips (set once, matched by name)

    // Set Component Metadata (Rhino 8 feature)
        Component.Message = "Name-Format v2.0";
        Component.NickName = "Name-Format";
    // --- Safe converters ---
    Func<object, string> S = o => o == null ? "" : o.ToString();
    Func<object, int> I = o =>
    {
      if (o == null) return 0;
      if (o is int) return (int)o;
      if (o is double) return (int)(double)o;
      int vi;
      if (int.TryParse(o.ToString(), out vi)) return vi;
      double vd;
      if (double.TryParse(o.ToString(), out vd)) return (int)vd;
      return 0;
    };

    // --- Convert inputs to lists ---
    List<string> prefixes = new List<string>();
    List<string> suffixes = new List<string>();

    // handle prefix input
    if (Pr is List<object>)
      foreach (var p in (List<object>)Pr) prefixes.Add(S(p));
    else
      prefixes.Add(S(Pr));

    // handle suffix input
    if (Sf is List<object>)
      foreach (var s in (List<object>)Sf) suffixes.Add(S(s));
    else
      suffixes.Add(S(Sf));

    // guard against an explicitly empty list (as opposed to unconnected/null,
    // which already falls into the else-branch above) — without this,
    // prefixes[Math.Min(i, prefixes.Count - 1)] indexes at -1 and throws
    if (prefixes.Count == 0) prefixes.Add("");
    if (suffixes.Count == 0) suffixes.Add("");

    int start = I(Sn);
    int count = Math.Max(0, I(Ct));
    int padDigits = Math.Max(0, I(Pn)); // PadLeft throws on a negative length

    // --- Generate names ---
    List<string> result = new List<string>();

    for (int i = 0; i < count; i++)
    {
      string prefix = prefixes[Math.Min(i, prefixes.Count - 1)];
      string suffix = suffixes[Math.Min(i, suffixes.Count - 1)];
      int num = start + i;
      string padded = num.ToString().PadLeft(padDigits, '0');
      result.Add(prefix + padded + suffix);
    }

    // --- Output ---
    T = result;
  }

  // ---------------------------------------------------------------- pin tooltips
  private bool _pinTipsSet = false;

  private void SetPinTips()
  {
    if (_pinTipsSet || Component == null) return;
    _pinTipsSet = true;
    TipPin(Component.Params.Input, "Pr", "Prefix text. Name *i* uses prefix *i*; if the list is shorter, the last prefix is reused.");
    TipPin(Component.Params.Input, "Sn", "Start number.");
    TipPin(Component.Params.Input, "Ct", "How many names to make.");
    TipPin(Component.Params.Input, "Pn", "Pad digits — total width of the number, filled with zeros.");
    TipPin(Component.Params.Input, "Sf", "Suffix text, matched to names like Pr.");
    TipPin(Component.Params.Output, "T", "The list of names.");
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
