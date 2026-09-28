using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Rhino;
using Rhino.Geometry;
using Grasshopper;
using Grasshopper.Kernel;

public class Script_Instance : GH_ScriptInstance
{
  private void RunScript(
		List<Plane> Pln,
		List<string> Txt,
		ref object Text,
		ref object Plane)
  {
    // Set Component Metadata for Rhino 8
        Component.Message = "Plane><Text";
        Component.NickName = "Plane><Text";

    /*
      PlaneText Converter v3.0
      ✅ Both conversions run independently
      ✅ No warnings/orange balloons
      ✅ Handles single or list inputs
      ✅ Silent when inputs are empty
      Author: Rajeev Pulari + ChatGPT
      Rhino 8 | Grasshopper C#
      Version: 2025.11.04
    */

    var textOut  = new List<string>();
    var planeOut = new List<Plane>();

    // --- 1. Plane → Text ---
    if (Pln != null && Pln.Count > 0)
    {
      foreach (Plane pl in Pln)
      {
        try
        {
          string o_str = string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "{0:F6},{1:F6},{2:F6}", pl.OriginX, pl.OriginY, pl.OriginZ);
          string x_str = string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "{0:F6},{1:F6},{2:F6}", pl.XAxis.X, pl.XAxis.Y, pl.XAxis.Z);
          string y_str = string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "{0:F6},{1:F6},{2:F6}", pl.YAxis.X, pl.YAxis.Y, pl.YAxis.Z);

          textOut.Add(string.Format("O{{{0}}}&X{{{1}}}&Y{{{2}}}", o_str, x_str, y_str));
        }
        catch { /* skip malformed plane silently */ }
      }
    }

    // --- 2. Text → Plane ---
    if (Txt != null && Txt.Count > 0)
    {
      foreach (string tx in Txt)
      {
        try
        {
          if (string.IsNullOrWhiteSpace(tx)) continue;

          string[] parts = tx.Trim().Split('&');
          if (parts.Length != 3) continue; // skip silently

          // trims each segment (handles stray whitespace/newlines from
          // copy-paste or a CSV round-trip) and checks length before
          // slicing off the "O{"/"X{"/"Y{" prefix and trailing "}", so a
          // malformed segment fails the length check instead of relying
          // on Substring to throw
          Func<string, double[]> parse = (s) =>
          {
            string trimmed = s.Trim();
            if (trimmed.Length < 3) throw new FormatException("segment too short: " + s);
            string clean = trimmed.Substring(2, trimmed.Length - 3);
            return clean.Split(',')
                        .Select(v => double.Parse(v.Trim(), System.Globalization.CultureInfo.InvariantCulture))
                        .ToArray();
          };

          double[] o = parse(parts[0]);
          double[] x = parse(parts[1]);
          double[] y = parse(parts[2]);

          planeOut.Add(new Plane(
            new Point3d(o[0], o[1], o[2]),
            new Vector3d(x[0], x[1], x[2]),
            new Vector3d(y[0], y[1], y[2])
          ));
        }
        catch { /* skip malformed text silently */ }
      }
    }

    // --- 3. Outputs ---
    Text  = textOut;
    Plane = planeOut;
  }
}
