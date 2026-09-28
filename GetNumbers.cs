using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

public class Script_Instance : GH_ScriptInstance
{
    // String: item access -- Grasshopper runs this once per input string and
    // builds the output tree itself (one branch of numbers per input item),
    // so the output always matches the input structure.
    // (lowercase "string" is a C# keyword and can't be a parameter name.)
    private void RunScript(string String, ref object NumbersOnly)
    {
        // Set Component Metadata (Rhino 8 feature)
        Component.Message = "get numbers only";
        Component.NickName = "GetNumbers";

        // Null/empty input: output an empty list rather than a text message,
        // so downstream Number params never receive a non-numeric value
        if (string.IsNullOrEmpty(String))
        {
            NumbersOnly = new List<double>();
            return;
        }

        NumbersOnly = ExtractNumbers(String);
    }

    // Core Logic: Extract numbers without Regex
    public List<double> ExtractNumbers(string text)
    {
        var numbers = new List<double>();
        var current = new StringBuilder();
        bool decimalFound = false;
        bool digitFound = false;

        // ASCII digits only: char.IsDigit also accepts Unicode digits
        // (e.g. Arabic-Indic), which InvariantCulture can't parse, so
        // those numbers would be silently dropped
        bool IsAsciiDigit(char ch) => ch >= '0' && ch <= '9';

        void Flush()
        {
            // digitFound guards against a lone "-" or "." being parsed
            if (digitFound &&
                double.TryParse(current.ToString(), NumberStyles.Float,
                                CultureInfo.InvariantCulture, out double value))
            {
                numbers.Add(value);
            }
            current.Clear();
            decimalFound = false;
            digitFound = false;
        }

        for (int i = 0; i < text.Length; i++)
        {
            char c    = text[i];
            char prev = i > 0 ? text[i - 1] : '\0';
            char next = i + 1 < text.Length ? text[i + 1] : '\0';

            if (IsAsciiDigit(c))
            {
                current.Append(c);
                digitFound = true;
            }
            // Decimal point: accept after digits ("12.5") or directly before
            // one (".5", "-.5")
            else if (c == '.' && !decimalFound && (digitFound || IsAsciiDigit(next)))
            {
                current.Append(c);
                decimalFound = true;
            }
            // Minus sign: only when it starts a number AND isn't glued to a
            // preceding letter/digit -- so "B-12" gives 12 (not -12) and
            // "100-200" gives 100, 200, while "x = -5" still gives -5
            else if (c == '-' && current.Length == 0 && !char.IsLetterOrDigit(prev)
                     && (IsAsciiDigit(next) || next == '.'))
            {
                current.Append(c);
            }
            else
            {
                Flush();
            }
        }

        // Catch the last number if the string ends with digits
        Flush();

        return numbers;
    }
}
