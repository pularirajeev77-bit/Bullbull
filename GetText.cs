using System;
using System.Collections.Generic;
using System.Text;

public class Script_Instance : GH_ScriptInstance
{
    // String: item access -- Grasshopper runs this once per input string and
    // assembles the outputs to match the input list/tree structure.
    // (lowercase "string" is a C# keyword and can't be a parameter name.)
    private void RunScript(string String, ref object TextOnly)
    {
    SetPinTips();   // pin tooltips (set once, matched by name)

        // Set Component Metadata for Rhino 8
        Component.Message = "Get text only";
        Component.NickName = "GetText";

        // Null/empty input: output an empty string rather than a message,
        // so "No input provided" never flows downstream as if it were
        // extracted text
        if (string.IsNullOrEmpty(String))
        {
            TextOnly = string.Empty;
            return;
        }

        TextOnly = ExtractAlphabets(String);
    }

    // Core Logic: keep letters, collapse whitespace, keep line breaks
    public string ExtractAlphabets(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        StringBuilder sb = new StringBuilder();
        bool pendingSpace = false;

        foreach (char c in text)
        {
            if (char.IsLetter(c))
            {
                // Emit at most one space between words. Removing digits and
                // symbols used to leave gaps like "Beam 12 Column" ->
                // "Beam  Column" (double space); now -> "Beam Column".
                // No space at the start of a line.
                if (pendingSpace && sb.Length > 0 && sb[sb.Length - 1] != '\n')
                    sb.Append(' ');
                pendingSpace = false;
                sb.Append(c);
            }
            else if (c == '\n')
            {
                // Keep line breaks so multi-line text stays multi-line
                sb.Append('\n');
                pendingSpace = false;
            }
            else if (char.IsWhiteSpace(c))
            {
                // spaces, tabs, and the '\r' of "\r\n"
                pendingSpace = true;
            }
            // digits, punctuation, symbols: dropped
        }

        // Remove leading/trailing blank lines
        return sb.ToString().Trim();
    }

  // ---------------------------------------------------------------- pin tooltips
  private bool _pinTipsSet = false;

  private void SetPinTips()
  {
    if (_pinTipsSet || Component == null) return;
    _pinTipsSet = true;
    TipPin(Component.Params.Input, "String", "The text (a list works; output keeps the same shape)");
    TipPin(Component.Params.Output, "TextOnly", "The letters, with single spaces between words.");
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
