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
}
