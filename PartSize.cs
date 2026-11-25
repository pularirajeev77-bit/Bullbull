/*
  Author: Rajeev Pulari + Claude
  Rhino 8 | Grasshopper C#
  Version: 2026.11.25
  Component: partSize
  Description:
    Calculates length, width, and height of a Box (in millimeters).
    Sizes are measured along the box's own axes, so oriented boxes work.
    Auto-adaptive for Item, List, or Tree input.
    Round: None (default, 2 decimals) | Nearest | Floor | Ceiling
           accepts the name or 0 / 1 / 2 / 3.
*/

using System;
using System.Collections.Generic;
using Rhino.Geometry;
using Grasshopper;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel;

public class Script_Instance : GH_ScriptInstance
{
    // Professional Signature: Use out object for modern Rhino 8 output mapping
    private void RunScript(
		object Box,
		object Round,
		ref object Length,
		ref object Width,
		ref object Height)
    {
        // 1. Read the rounding mode (None when nothing is connected)
        string mode = ReadRoundMode(Round);

        // Set Component Metadata (Rhino 8 Native)
        // Check prevents unnecessary UI refreshes which can disrupt data tree flows
        string msg = mode == "None" ? "Part Size v2.2" : "Part Size v2.2 | " + mode;
        if (this.Component != null && this.Component.Message != msg)
        {
            this.Component.Message = msg;
            this.Component.NickName = "PSize";
            this.Component.Name = "PartSize";
        }

        // 2. Prepare output containers
        var lengths = new List<double>();
        var widths = new List<double>();
        var heights = new List<double>();

        try
        {
            // 3. Convert input to DataTree<Box> to support Item/List/Tree
            var boxTree = ToTree<Box>(Box);

            if (boxTree.BranchCount == 0)
            {
                Length = lengths; Width = widths; Height = heights;
                return;
            }

            // 4. Process each branch and box
            for (int i = 0; i < boxTree.BranchCount; i++)
            {
                GH_Path path = boxTree.Path(i);
                var branch = boxTree.Branch(path);

                foreach (var bx in branch)
                {
                    // Check for invalid box
                    if (!bx.IsValid)
                    {
                        lengths.Add(double.NaN);
                        widths.Add(double.NaN);
                        heights.Add(double.NaN);
                        continue;
                    }

                    // Geometry Logic: measure along the box's own plane axes
                    // (world dX/dY of the diagonal is wrong when the box is rotated)
                    double dx = bx.X.Length;
                    double dy = bx.Y.Length;
                    double dz = bx.Z.Length;

                    // Sort Max as Length, Min as Width (Native Logic)
                    lengths.Add(ApplyRound(Math.Max(dx, dy), mode));
                    widths.Add(ApplyRound(Math.Min(dx, dy), mode));
                    heights.Add(ApplyRound(dz, mode));
                }
            }
        }
        catch (Exception ex)
        {
            // Professional error reporting to the component "out" window
            Print("Error in PartSize: " + ex.Message);
            Length = Width = Height = null;
            return;
        }

        // 5. Final Assignment to Output Pins
        Length = lengths;
        Width = widths;
        Height = heights;
    }

    // ---------------------------------------------------------------
    // Round mode from a name (None / Nearest / Floor / Ceiling) or 0..3
    private string ReadRoundMode(object input)
    {
        if (input == null) return "None";
        string s = input.ToString().Trim().ToLowerInvariant();
        switch (s)
        {
            case "1": case "nearest": case "round": return "Nearest";
            case "2": case "floor": return "Floor";
            case "3": case "ceiling": case "ceil": return "Ceiling";
            default: return "None";
        }
    }

    // Apply the round mode to one value (whole millimetres)
    private double ApplyRound(double v, string mode)
    {
        // Clean float noise first so 120.0000001 does not ceil to 121
        double clean = Math.Round(v, 6);
        switch (mode)
        {
            case "Nearest": return Math.Round(clean, MidpointRounding.AwayFromZero);
            case "Floor":   return Math.Floor(clean);
            case "Ceiling": return Math.Ceiling(clean);
            default:        return Math.Round(v, 2);   // None: unchanged behaviour
        }
    }

    // ---------------------------------------------------------------
    // Universal Input Normalizer — handles Item, List, or Tree inputs
    private DataTree<T> ToTree<T>(object input)
    {
        var tree = new DataTree<T>();
        if (input == null) return tree;

        if (input is T single)
        {
            tree.Add(single, new GH_Path(0));
            return tree;
        }

        if (input is IEnumerable<T> list)
        {
            var path = new GH_Path(0);
            foreach (var item in list)
                tree.Add(item, path);
            return tree;
        }

        if (input is DataTree<object> dataTree)
        {
            foreach (var path in dataTree.Paths)
            {
                foreach (var o in dataTree.Branch(path))
                {
                    if (o is T cast)
                        tree.Add(cast, path);
                }
            }
            return tree;
        }

        return tree;
    }
}
