// ✅ Find & Replace Text – Rhino 8 Grasshopper C#
// Author: Rajeev Pulari + ChatGPT
// Inputs: T (list of strings), F (list of find strings), R (list of replace strings)
// Output: Txt (list of replaced strings)

using System;
using System.Collections;
using System.Collections.Generic;

List<string> ToStringList(object input)
{

    // Converts any GH input to List<string>
    var list = new List<string>();
    if (input == null) return list;

    // string implements IEnumerable<char>, so it would otherwise match the
    // IEnumerable branch below and get exploded into individual characters
    // (e.g. a single "Beam" item-input becomes "B","e","a","m") — check it
    // explicitly first and treat it as one item.
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

// --- Set Component Metadata (Rhino 8 feature) ---
Component.Message = "Find&Replace v1.0";
Component.NickName = "Find&Replace";

// --- Convert all inputs ---
List<string> textList = ToStringList(T);
List<string> findList = ToStringList(F);
List<string> replList = ToStringList(R);

// --- Safety check ---
if (textList.Count == 0 || findList.Count == 0)
{
    Txt = new List<string>() { "⚠ Empty input list(s)" };
    return;
}

// --- Extend replacement list to match find list ---
string last = (replList.Count > 0) ? replList[replList.Count - 1] : "";
while (replList.Count < findList.Count)
    replList.Add(last);

// --- Perform find & replace ---
List<string> result = new List<string>();

foreach (string s in textList)
{
    string modified = s;
    for (int i = 0; i < findList.Count; i++)
    {
        string find = findList[i];
        string replace = replList[i];
        if (!string.IsNullOrEmpty(find))
            modified = modified.Replace(find, replace);
    }
    result.Add(modified);
}

// --- Output ---
Txt = result;
