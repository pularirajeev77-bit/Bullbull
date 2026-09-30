// Rhino 8 runs scripts on .NET 7+, where OleDb is not built in: pull it from NuGet.
// (Remove this line if you run the script in Rhino 7 / .NET Framework.)
#r "nuget: System.Data.OleDb, 8.0.0"

/*
  Author: Rajeev Pulari
  Rhino 8 | Grasshopper C#
  Component: Parametric Hardware Lookup v1.1
  Description: Looks up node hardware (bolt, sleeve, cone, thread) for a pipe
               diameter from an Excel table (.xlsx) via OLEDB - no Excel process.
  Needs: Microsoft Access Database Engine (ACE.OLEDB.12.0), 64-bit, installed.
*/

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.Globalization;
using System.IO;
using Grasshopper.Kernel;

public class Script_Instance : GH_ScriptInstance
{
    // ExcelPath: item (text) | PipeDiameter: item (number)
    private void RunScript(
        string ExcelPath,
        object PipeDiameter,
        ref object BoltDiameter,
        ref object SleeveDiameter,
        ref object SleeveLength,
        ref object ConeDepth,
        ref object ThreadLength)
    {
        // Metadata + pin tooltips (once)
        if (Component != null && Component.Name != "Parametric Hardware Lookup")
        {
            Component.Name = "Parametric Hardware Lookup";
            Component.NickName = "HardLook";
            Component.Message = "HardLook v1.1";
            Component.Description = "Looks up bolt / sleeve / cone / thread sizes for a pipe diameter from an Excel table.";

            var pi = Component.Params.Input;
            SetTip(pi, "ExcelPath", "Full path to the .xlsx table (first row = column headers).");
            SetTip(pi, "PipeDiameter", "Pipe diameter to look up. Rounded to a whole number before matching.");
            var po = Component.Params.Output;
            SetTip(po, "BoltDiameter", "Bolt diameter from the table (10 if not found).");
            SetTip(po, "SleeveDiameter", "Sleeve diameter from the table (10 if not found).");
            SetTip(po, "SleeveLength", "Sleeve length from the table (10 if not found).");
            SetTip(po, "ConeDepth", "Cone depth from the table (10 if not found).");
            SetTip(po, "ThreadLength", "Thread length from the table (10 if not found).");
        }

        // 1. Fallback values
        const double DEFAULT_VALUE = 10.0;
        BoltDiameter = DEFAULT_VALUE;
        SleeveDiameter = DEFAULT_VALUE;
        SleeveLength = DEFAULT_VALUE;
        ConeDepth = DEFAULT_VALUE;
        ThreadLength = DEFAULT_VALUE;

        // 2. Pipe diameter (accepts number, integer or numeric text)
        double targetDiameter;
        if (PipeDiameter == null || !GH_Convert.ToDouble(PipeDiameter, out targetDiameter, GH_Conversion.Both))
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                "Input 'PipeDiameter' is missing or not a number. Default values used.");
            return;
        }

        // 3. Excel path
        if (string.IsNullOrWhiteSpace(ExcelPath) || !File.Exists(ExcelPath))
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                "Excel file not found. Default values used.");
            return;
        }

        string connectionString =
            "Provider=Microsoft.ACE.OLEDB.12.0;Data Source=\"" + ExcelPath + "\";" +
            "Extended Properties=\"Excel 12.0 Xml;HDR=YES;IMEX=1;\"";

        try
        {
            using (var connection = new OleDbConnection(connectionString))
            {
                connection.Open();

                // Sheet1 if present, otherwise the first sheet in the workbook
                string sheet = FindSheet(connection);
                if (sheet == null)
                {
                    Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "The workbook has no sheets.");
                    return;
                }

                var table = new DataTable();
                using (var command = new OleDbCommand("SELECT * FROM [" + sheet + "]", connection))
                using (var adapter = new OleDbDataAdapter(command))
                    adapter.Fill(table);

                string[] needed = { "PipeDiameter", "BoltDiameter", "SleeveDiameter", "SleeveLength", "ConeDepth", "ThreadLength" };
                var missing = new List<string>();
                foreach (string col in needed)
                    if (!table.Columns.Contains(col)) missing.Add(col);
                if (missing.Count > 0)
                {
                    Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                        "Sheet '" + sheet.TrimEnd('$') + "' is missing column(s): " + string.Join(", ", missing));
                    return;
                }

                // IMEX=1 reads cells as text, so parse numbers ourselves (culture-safe)
                long targetKey = (long)Math.Round(targetDiameter, MidpointRounding.AwayFromZero);
                foreach (DataRow row in table.Rows)
                {
                    double rowDia;
                    if (!TryNumber(row["PipeDiameter"], out rowDia)) continue;
                    if ((long)Math.Round(rowDia, MidpointRounding.AwayFromZero) != targetKey) continue;

                    double v;
                    var bad = new List<string>();
                    if (TryNumber(row["BoltDiameter"], out v)) BoltDiameter = v; else bad.Add("BoltDiameter");
                    if (TryNumber(row["SleeveDiameter"], out v)) SleeveDiameter = v; else bad.Add("SleeveDiameter");
                    if (TryNumber(row["SleeveLength"], out v)) SleeveLength = v; else bad.Add("SleeveLength");
                    if (TryNumber(row["ConeDepth"], out v)) ConeDepth = v; else bad.Add("ConeDepth");
                    if (TryNumber(row["ThreadLength"], out v)) ThreadLength = v; else bad.Add("ThreadLength");

                    if (bad.Count > 0)
                        Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                            "Row for " + targetKey + " has empty/non-numeric " + string.Join(", ", bad) + "; default used there.");
                    return;
                }

                Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                    "Pipe diameter " + targetKey + " not found in the table. Default values used.");
            }
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("ACE.OLEDB"))
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                "Microsoft Access Database Engine (64-bit) is not installed: " + ex.Message);
        }
        catch (Exception ex)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                "Excel read error (is the file open / locked?): " + ex.Message);
        }
    }

    private string FindSheet(OleDbConnection connection)
    {
        DataTable schema = connection.GetOleDbSchemaTable(OleDbSchemaGuid.Tables, null);
        if (schema == null) return null;
        string first = null;
        foreach (DataRow r in schema.Rows)
        {
            string name = Convert.ToString(r["TABLE_NAME"]).Trim('\'');
            if (!name.EndsWith("$")) continue;          // skip named ranges
            if (first == null) first = name;
            if (name.Equals("Sheet1$", StringComparison.OrdinalIgnoreCase)) return name;
        }
        return first;
    }

    private bool TryNumber(object cell, out double value)
    {
        value = 0;
        if (cell == null || cell == DBNull.Value) return false;
        if (cell is double) { value = (double)cell; return true; }
        string s = Convert.ToString(cell, CultureInfo.InvariantCulture).Trim();
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            || double.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
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
