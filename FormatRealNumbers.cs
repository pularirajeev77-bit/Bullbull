#region Metadata
/*
  Platform    : Rhino 8 | Grasshopper C#
  Component   : Format Real Numbers
  NickName    : Format-RealNumbers
  Message     : Format Real Numbers v1.1
  Description : Writes a number as text with exactly 14 decimals, showing the
                EXACT value stored in the double - no scientific notation, no
                rounding to 15-17 significant digits. C# port of the Python
                "{0:.14F}".format(Decimal(float(x))) (round half to even).
                e.g. 0.1 -> 0.10000000000000, 1e20 -> 100000000000000000000.00000000000000
                Text input is passed through unchanged.

  Inputs:
    Number : object (Item) - number (or integer / numeric goo); text passes through

  Outputs:
    Text   : string - the formatted number
*/
#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

public class Script_Instance : GH_ScriptInstance
{
  // Number : item access
  private void RunScript(object Number, ref object Text)
  {
    // Metadata + pin tooltips (once)
    if (Component != null && Component.Name != "Format Real Numbers")
    {
      Component.Name        = "Format Real Numbers";
      Component.NickName    = "Format-RealNumbers";
      Component.Message     = "Format Real Numbers v1.1";
      Component.Description = "Writes a number with exactly 14 decimals (exact stored value, no scientific notation). Text passes through.";

      SetTip(Component.Params.Input, "Number", "Number to format. Text is passed through unchanged. Item access.");
      SetTip(Component.Params.Output, "Text", "The number with exactly 14 decimals, e.g. 0.10000000000000.");
    }

    Text = "";
    if (Number == null) return;

    // Unwrap Grasshopper types (GH_Number, GH_Integer, GH_String ...)
    object raw = (Number is IGH_Goo) ? ((IGH_Goo)Number).ScriptVariable() : Number;
    if (raw == null) return;

    if (raw is string) { Text = raw; return; }

    double d;
    try
    {
      // Invariant culture: never depends on the PC's decimal separator
      d = Convert.ToDouble(raw, CultureInfo.InvariantCulture);
    }
    catch
    {
      Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
        "Input is not a number (" + raw.GetType().Name + ") - passed through as text.");
      Text = Convert.ToString(raw, CultureInfo.InvariantCulture);
      return;
    }

    Text = ExactF14(d);
  }

  // Exact equivalent of Python "{0:.14F}".format(Decimal(float(x)))
  private static string ExactF14(double d)
  {
    // Python gives "NaN" / "Infinity" / "-Infinity"; .NET's current culture may give "∞"
    if (double.IsNaN(d)) return "NaN";
    if (double.IsPositiveInfinity(d)) return "Infinity";
    if (double.IsNegativeInfinity(d)) return "-Infinity";

    const int digits = 14;
    long bits = BitConverter.DoubleToInt64Bits(d);
    bool neg = bits < 0;
    int exp = (int)((bits >> 52) & 0x7FF);
    long man = bits & 0xFFFFFFFFFFFFFL;
    if (exp == 0) exp++; else man |= 1L << 52;   // subnormal / normal
    exp -= 1075;                                  // value = man * 2^exp

    BigInteger pow10 = BigInteger.Pow(10, digits);
    BigInteger scaled;                            // value * 10^14, rounded half-even

    if (exp >= 0)
    {
      scaled = (new BigInteger(man) << exp) * pow10;
    }
    else
    {
      int sh = -exp;
      BigInteger numer = new BigInteger(man) * pow10;
      BigInteger q = numer >> sh;
      BigInteger rem = numer - (q << sh);
      BigInteger half = BigInteger.One << (sh - 1);
      int c = rem.CompareTo(half);
      if (c > 0 || (c == 0 && !q.IsEven)) q += 1;
      scaled = q;
    }

    string s = scaled.ToString(CultureInfo.InvariantCulture).PadLeft(digits + 1, '0');
    string res = s.Substring(0, s.Length - digits) + "." + s.Substring(s.Length - digits);
    return neg ? "-" + res : res;
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
