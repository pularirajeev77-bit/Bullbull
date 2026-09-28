// ✅ Find & Replace Text – Rhino 8 Grasshopper C#
// Author: Rajeev Pulari + ChatGPT
// Inputs: T (item access, one string per run), F (list of find strings), R (list of replace strings)
// Output: Txt (one processed string per run — Grasshopper matches it to T's list shape automatically)

using System;
using System.Collections;
using System.Collections.Generic;

public class Script_Instance : GH_ScriptInstance
{
  private void RunScript(
		object T,
		List<object> F,
		List<object> R,
		ref object Txt)
  {
    // Set Component Metadata (Rhino 8 feature)
        Component.Message = "Find&Replace v1.0";
        Component.NickName = "Find&Replace";

    // T is item access: Grasshopper calls RunScript once per entry in the
    // input list and reassembles the outputs into a matching list on its
    // own, as long as Txt stays a single value per call (not a list) --
    // that's what keeps the output list shaped like the input instead of
    // nesting each result into its own one-item branch.
    string text = T == null ? "" : T.ToString();

    List<string> findList = ToStringList(F);
    List<string> replList = ToStringList(R);

    if (findList.Count == 0)
    {
      Txt = text; // nothing to find/replace: pass the text through unchanged
      return;
    }

    // --- Extend replacement list to match find list ---
    string last = (replList.Count > 0) ? replList[replList.Count - 1] : "";
    while (replList.Count < findList.Count)
      replList.Add(last);

    // --- Perform find & replace ---
    string modified = text;
    for (int i = 0; i < findList.Count; i++)
    {
      string find = findList[i];
      string replace = replList[i];
      if (!string.IsNullOrEmpty(find))
        modified = modified.Replace(find, replace);
    }

    // --- Output ---
    Txt = modified;
  }

  // <Custom additional code>

  // Converts any GH input to List<string>. Handles item access (a single
  // object), list access (an IEnumerable), and null/unconnected inputs.
  // string implements IEnumerable<char>, so it's checked explicitly before
  // the generic IEnumerable branch — otherwise a single item-input string
  // like "Beam" would get exploded into "B","e","a","m".
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
}
