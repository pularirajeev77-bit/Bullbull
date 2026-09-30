#region Metadata
/*
  Author      : Rajeev Pulari + ChatGPT
  Platform    : Rhino 8 | Grasshopper C#
  Version     : 2026.09.30
  Component   : Title Block Key Search
  NickName    : KeySearch
  Message     : KeySearch v2.1  (after a run: "matches / keys")
  Description : Flags which title-block attribute keys (from BlockAttExtract)
                contain any of the search keys. Whole-word, case-insensitive.
                Feed Keys straight from BlockAttExtract: it runs once per layout
                branch, so every output keeps the same {k} branches.

  Inputs:
    Keys        : List<string> (List) - Attribute keys, e.g. BlockAttExtract "Keys"
    SearchKeys  : List<string> (List) - Words or phrases to look for

  Outputs:
    Matches     : List<bool>   - True where the key contains a search key (one per key)
    MatchIndex  : List<int>    - Indices of the matching keys (use with List Item on Values)
    Summary     : string       - "n match(es) out of m keys"
*/
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

public class Script_Instance : GH_ScriptInstance
{
  // Keys, SearchKeys : list access
  private void RunScript(
    List<string> Keys,
    List<string> SearchKeys,
    ref object Matches,
    ref object MatchIndex,
    ref object Summary)
  {
    // Metadata + pin tooltips (once)
    if (Component != null && Component.Name != "Title Block Key Search")
    {
      Component.Name = "Title Block Key Search";
      Component.NickName = "KeySearch";
      Component.Message = "KeySearch v2.1";
      Component.Description = "Flags title-block keys (from BlockAttExtract) that contain any search key. Whole-word, case-insensitive.";

      var pi = Component.Params.Input;
      SetTip(pi, "Keys", "Attribute keys to search, e.g. the Keys output of BlockAttExtract. List access.");
      SetTip(pi, "SearchKeys", "Words or phrases to look for (whole words, not case-sensitive). List access.");
      var po = Component.Params.Output;
      SetTip(po, "Matches", "True where the key contains a search key (one per key).");
      SetTip(po, "MatchIndex", "Indices of matching keys - use with List Item on BlockAttExtract Values.");
      SetTip(po, "Summary", "How many keys matched.");
    }

    var results = new List<bool>();
    var matchedIndices = new List<int>();
    Matches = results;
    MatchIndex = matchedIndices;

    if (Keys == null || Keys.Count == 0)
    {
      Summary = "'Keys' input is empty.";
      Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, (string)Summary);
      return;
    }

    // Search keys normalised to single-spaced lower case, so "Drawing  No" == "drawing no"
    var searches = (SearchKeys ?? new List<string>())
      .Select(Normalise)
      .Where(w => w.Length > 0)
      .Distinct()
      .ToList();
    if (searches.Count == 0)
    {
      Summary = "'SearchKeys' input is empty.";
      Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, (string)Summary);
      return;
    }

    // Whole-word match: pad with spaces so "no" matches "drawing no" but not "note"
    var padded = searches.Select(w => " " + w + " ").ToList();
    for (int i = 0; i < Keys.Count; i++)
    {
      string text = " " + Normalise(Keys[i]) + " ";
      bool found = padded.Any(w => text.Contains(w));
      results.Add(found);
      if (found) matchedIndices.Add(i);
    }

    Summary = matchedIndices.Count + " match(es) out of " + Keys.Count + " keys.";
    Component.Message = "KeySearch v2.1 | " + matchedIndices.Count + "/" + Keys.Count;
  }

  // Lower case, any whitespace (space, tab, newline) collapsed to single spaces
  private static string Normalise(string s)
  {
    if (string.IsNullOrWhiteSpace(s)) return "";
    var parts = s.ToLowerInvariant().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
    return string.Join(" ", parts);
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
}
