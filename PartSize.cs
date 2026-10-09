/*
  Author: Rajeev Pulari + ChatGPT
  Rhino 8 | Grasshopper C#
  Version: 2026.04.05
  Component: partSize
  Description:
    Calculates length, width, and height of a Box (in millimeters).
    Auto-adaptive for Item, List, or Tree input.
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
		ref object Length,
		ref object Width,
		ref object Height)
    {
    SetPinTips();   // pin tooltips (set once, matched by name)

        // 1. Set Component Metadata (Rhino 8 Native)
        // Check prevents unnecessary UI refreshes which can disrupt data tree flows
        if (this.Component != null && this.Component.Message != "Part Size v2.0")
        {
            this.Component.Message = "Part Size v2.0";
            this.Component.NickName = "PSize";
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

                    Point3d[] corners = bx.GetCorners();
                    if (corners == null || corners.Length < 8)
                    {
                        lengths.Add(double.NaN);
                        widths.Add(double.NaN);
                        heights.Add(double.NaN);
                        continue;
                    }

                    // Geometry Logic: pt1 to pt2 diagonal calculation
                    Point3d pt1 = corners[0];
                    Point3d pt2 = corners[6];

                    double dx = Math.Abs(pt2.X - pt1.X);
                    double dy = Math.Abs(pt2.Y - pt1.Y);
                    double dz = Math.Abs(pt2.Z - pt1.Z);

                    // Sort Max as Length, Min as Width (Native Logic)
                    lengths.Add(Math.Round(Math.Max(dx, dy), 2));
                    widths.Add(Math.Round(Math.Min(dx, dy), 2));
                    heights.Add(Math.Round(dz, 2));
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

  // ---------------------------------------------------------------- pin tooltips
  private bool _pinTipsSet = false;

  private void SetPinTips()
  {
    if (_pinTipsSet || Component == null) return;
    _pinTipsSet = true;
    TipPin(Component.Params.Input, "Box", "The box(es) to measure (item, list or tree)");
    TipPin(Component.Params.Output, "Length", "The larger of the two base edges.");
    TipPin(Component.Params.Output, "Width", "The smaller of the two base edges.");
    TipPin(Component.Params.Output, "Height", "The box height (its Z size in the box's own frame)");
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