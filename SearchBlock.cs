#region Metadata
/*
  Author      : Rajeev Pulari + Gemini   (original: Jestomaguio)
  Platform    : Rhino 8 | Grasshopper C#
  Version     : 2026.09.30
  Component   : Search Block
  NickName    : SrchBlk
  Message     : Search Block v2.1  (after a run: "matches / names")
  Description : Finds the block names that contain a search text (case-insensitive
                substring), e.g. to pick the title-block name to feed into
                BlockAttExtract / BlockAttEditor. Trees are handled per branch
                by Grasshopper, so output branches match the input.

  Inputs:
    BlockNames : List<string> (List) - Block names (or any text) to search
    Search     : string       (Item) - Text to look for, e.g. "title"

  Outputs:
    Matches    : List<string> - Names containing the search text (original case)
    MatchIndex : List<int>    - Their indices in BlockNames
*/
#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Rhino;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

public class Script_Instance : GH_ScriptInstance
{
  // BlockNames : list access | Search : item access
  private void RunScript(
    List<string> BlockNames,
    string Search,
    ref object Matches,
    ref object MatchIndex)
  {
    // Metadata + pin tooltips (once)
    if (Component != null && Component.Name != "Search Block")
    {
      Component.Name = "Search Block";
      Component.NickName = "SrchBlk";
      Component.Message = "Search Block v2.1";
      Component.Description = "Finds block names containing a search text (case-insensitive).";

      SetTip(Component.Params.Input, "BlockNames", "Block names (or any text) to search. List access.");
      SetTip(Component.Params.Input, "Search", "Text to look for, e.g. 'title'. Not case-sensitive. Item access.");
      SetTip(Component.Params.Output, "Matches", "Names that contain the search text (original spelling).");
      SetTip(Component.Params.Output, "MatchIndex", "Indices of the matches in BlockNames.");
    }

    var results = new List<string>();
    var indices = new List<int>();
    Matches = results;
    MatchIndex = indices;

    if (BlockNames == null || BlockNames.Count == 0)
    {
      Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Input 'BlockNames' is empty. Please provide text data.");
      return;
    }
    if (string.IsNullOrWhiteSpace(Search))
    {
      Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Input 'Search' is empty. Please provide a search term.");
      return;
    }

    string term = Search.Trim();
    for (int i = 0; i < BlockNames.Count; i++)
    {
      string name = BlockNames[i];
      if (string.IsNullOrEmpty(name)) continue;
      if (name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
      {
        results.Add(name);
        indices.Add(i);
      }
    }

    Component.Message = "Search Block v2.1 | " + results.Count + "/" + BlockNames.Count;
    if (results.Count == 0)
      Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "No name contains '" + term + "'.");
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
