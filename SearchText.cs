// ✅ Search Text – Rhino 8 Grasshopper C#
// Author: Rajeev Pulari + ChatGPT
// Version: 2025.06.03
// Inputs: texts (List<string>), words (List<string>)
// Output: result (List<bool>) -- one bool per entry in texts, same order/length

using System;
using System.Collections;
using System.Collections.Generic;

public class Script_Instance : GH_ScriptInstance
{
  private void RunScript(
		List<object> texts,
		List<object> words,
		ref object result)
  {
    SetPinTips();   // pin tooltips (set once, matched by name)

    // Set Component Metadata (Rhino 8 feature)
        Component.Message = "Search Text v1.0";
        Component.NickName = "Search Text";

    // --- Convert inputs ---
    List<string> textList = ToStringList(texts);
    List<string> wordList = ToStringList(words);

    // --- Validate inputs ---
    // Keep the output length matched to textList so downstream components
    // that expect one bool per text still line up correctly.
    if (textList.Count == 0)
    {
      result = new List<bool>();
      return;
    }
    if (wordList.Count == 0)
    {
      result = textList.ConvertAll(_ => false);
      return;
    }

    // --- Prepare match set (case-insensitive) ---
    // Trim + drop blanks so a stray empty entry in `words` (e.g. a trailing
    // comma in a panel) can't sit in the set and false-match empty tokens
    // produced by double spaces below.
    HashSet<string> matchWords = new HashSet<string>();
    foreach (string w in wordList)
    {
      string cleaned = (w ?? "").Trim().ToLower();
      if (cleaned.Length > 0) matchWords.Add(cleaned);
    }

    // --- Core logic ---
    List<bool> output = new List<bool>();

    foreach (string txt in textList)
    {
      bool matchFound = false;
      if (!string.IsNullOrEmpty(txt))
      {
        // RemoveEmptyEntries so double/leading/trailing spaces don't
        // produce an empty "" token that could match a blank word entry
        string[] tokens = txt.ToLower().Split(
          new char[] { ' ', '\t', '\n', '\r' },
          StringSplitOptions.RemoveEmptyEntries);

        foreach (string w in tokens)
        {
          if (matchWords.Contains(w))
          {
            matchFound = true;
            break;
          }
        }
      }
      output.Add(matchFound);
    }

    // --- Output ---
    result = output;
  }

  // <Custom additional code>

  // Converts any GH input to List<string>. Handles item access (a single
  // object), list access (an IEnumerable), and null/unconnected inputs.
  // string implements IEnumerable<char>, so it's checked explicitly before
  // the generic IEnumerable branch — otherwise a single item-input string
  // would get exploded into individual characters.
  List<string> ToStringList(object input)
  {
    var list = new List<string>();
    if (input == null) return list;

    if (input is string s)
    {
      list.Add(s);
      return list;
    }

    if (input is IEnumerable enumerable)
    {
      foreach (var item in enumerable)
        if (item != null) list.Add(item.ToString());
    }
    else
    {
      list.Add(input.ToString());
    }
    return list;
  }

  // </Custom additional code>

  // ---------------------------------------------------------------- pin tooltips
  private bool _pinTipsSet = false;

  private void SetPinTips()
  {
    if (_pinTipsSet || Component == null) return;
    _pinTipsSet = true;
    TipPin(Component.Params.Input, "texts", "Texts to search in.");
    TipPin(Component.Params.Input, "words", "Words to look for.");
    TipPin(Component.Params.Output, "result", "One True/False per text, same order as texts.");
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
