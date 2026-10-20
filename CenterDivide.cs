/*
  Author: Rajeev Pulari + ChatGPT
  Rhino 8 | Grasshopper C#
  Version: 2.1 (2026-10-20)
  Component: CenterDivide

  v2.1: Curve, Distance and Toggle are TREE access. Lists of lists / trees of curves
        used to come out as one flat-looking list: with list access Grasshopper ran
        the script once per branch and every run wrote to {0},{1},... again, so the
        branches of different inputs piled on top of each other. Output path is now
        {input branch ; curve index} (like Grasshopper's Divide Curve); Distance and
        Toggle are matched per branch.
  Description:
    Divide curve from middle with 3 toggle modes:
      • Unplugged → Auto (odd/even division logic)
      • Plugged True → Force ON pattern [0, 1]  (point at the middle)
      • Plugged False → Force OFF pattern [1, 0] (gap at the middle)
*/

using System;
using System.Collections;
using System.Collections.Generic;
using Rhino;
using Rhino.Geometry;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;

public class Script_Instance : GH_ScriptInstance
{
    private void RunScript(
		DataTree<Curve> Curve,
		DataTree<double> Distance,
		DataTree<object> Toggle,
		ref object OUT)
    {
    SetPinTips();   // pin tooltips (set once, matched by name)

        // 1. Set Component Metadata (Rhino 8 Native)
        this.Component.Message = "Center Divide v2.1";
        this.Component.NickName = "CenterDiv";

        // 2. Output: one branch per input curve, {input branch ; curve index},
        //    so lists, lists of lists and trees keep their structure.
        var tree = new DataTree<Point3d>();

        try
        {
            if (Curve == null || Curve.DataCount == 0)
            {
                OUT = tree;
                return;
            }

          for (int b = 0; b < Curve.BranchCount; b++)
          {
            GH_Path inPath = Curve.Path(b);
            List<Curve> curves = Curve.Branch(b);

            // Distance / Toggle: branch with the same path, else the first branch
            // (one flat list then applies to every branch). Unplugged Toggle -> Auto.
            List<double> distList = BranchFor(Distance, inPath);
            List<bool> toggleList = ExtractBoolList(BranchFor(Toggle, inPath));

            for (int i = 0; i < curves.Count; i++)
            {
                var path = inPath.AppendElement(i);
                tree.EnsurePath(path); // keep an (empty) branch for null curves
                Curve crv = curves[i];
                if (crv == null) continue;

                // --- Distance per curve (reuse last value, Grasshopper-style) ---
                double dist = 1.0;
                if (distList.Count > 0)
                    dist = SafePos(distList[Math.Min(i, distList.Count - 1)], 1.0);

                // --- Toggle logic (3 modes) ---
                bool tog;
                if (toggleList.Count > 0)
                {
                    tog = toggleList[Math.Min(i, toggleList.Count - 1)];
                }
                else
                {
                    // Auto mode when Toggle is not wired: odd divisions -> true.
                    // This keeps both end pieces between half and one full
                    // Distance long, so no tiny slivers at the curve ends.
                    double len = crv.GetLength();
                    int div = (int)(len / dist);
                    tog = (div % 2 == 1);
                }

                tree.AddRange(ProcessCurve(crv, dist, tog), path);
            }
          }
        }
        catch (Exception ex)
        {
            // Red balloon on the component instead of only the output window
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                "Error in CenterDivide: " + ex.Message);
        }

        // 3. Final Assignment
        OUT = tree;
    }

    // --- Helpers ---

    // Branch with the same path, else the first branch, else empty.
    private static List<T> BranchFor<T>(DataTree<T> t, GH_Path path)
    {
        if (t == null || t.BranchCount == 0) return new List<T>();
        if (t.PathExists(path)) return t.Branch(path);
        return t.Branch(0);
    }

    private List<bool> ExtractBoolList(IList toggle)
    {
        var list = new List<bool>();
        if (toggle == null) return list;

        foreach (var o in toggle)
        {
            if (TryToBool(o, out bool val)) list.Add(val);
        }
        return list;
    }

    private bool TryToBool(object o, out bool result)
    {
        result = false;
        if (o == null) return false;
        if (o is bool b) { result = b; return true; }
        if (o is int i) { result = (i != 0); return true; }
        if (o is double d) { result = Math.Abs(d) > 1e-12; return true; }

        string s = o.ToString().Trim().ToLowerInvariant();
        if (s == "true" || s == "1" || s == "yes" || s == "on") { result = true; return true; }
        if (s == "false" || s == "0" || s == "no" || s == "off") { result = false; return true; }

        return false;
    }

    private double SafePos(double v, double fallback)
    {
        if (double.IsNaN(v) || double.IsInfinity(v)) return fallback;
        return (v > 0.0) ? v : fallback;
    }

    private List<Point3d> ProcessCurve(Curve curve, double distance, bool toggle)
    {
        var output = new List<Point3d>();
        if (curve == null || distance <= 0.0) return output;

        // Middle by LENGTH, not by parameter. The domain midpoint is only
        // the true middle on uniformly parameterized curves; on a polyline
        // with unequal segments or most NURBS it is off-center, which broke
        // the symmetry of the whole division.
        double midT;
        if (!curve.NormalizedLengthParameter(0.5, out midT))
            midT = curve.Domain.Mid;

        Point3d midPt = curve.PointAt(midT);
        Point3d startPt = curve.PointAtStart;
        Point3d endPt = curve.PointAtEnd;

        Curve[] shattered = curve.Split(midT);
        if (shattered == null || shattered.Length < 2) return output;

        Curve seg1 = shattered[0];   // start -> mid
        Curve seg2 = shattered[1];   // mid -> end
        Curve flipped = seg1.DuplicateCurve();
        flipped.Reverse();           // mid -> start

        double half = distance * 0.5;
        double[] tA = seg2.DivideByLength(half, false);
        double[] tB = flipped.DivideByLength(half, false);

        // Points at half, 2·half, 3·half ... measured outward from the middle.
        // toggle ON keeps the odd indices (d, 2d, 3d ...), OFF keeps the even
        // ones (d/2, 3d/2 ...) -- same selection as the original dispatch.
        var sideA = new List<Point3d>();   // mid -> end, outward order
        if (tA != null)
            for (int i = 0; i < tA.Length; i++)
                if ((i % 2 == 1) == toggle) sideA.Add(seg2.PointAt(tA[i]));

        var sideB = new List<Point3d>();   // mid -> start, outward order
        if (tB != null)
            for (int i = 0; i < tB.Length; i++)
                if ((i % 2 == 1) == toggle) sideB.Add(flipped.PointAt(tB[i]));

        // Build the result already in order (start -> mid -> end) instead
        // of sorting with ClosestPoint inside the comparer: that ran a
        // closest-point search on every comparison (slow on big inputs)
        // and could misplace points at the seam of a closed curve.
        sideB.Reverse();
        var ordered = new List<Point3d>();
        ordered.Add(startPt);
        ordered.AddRange(sideB);
        if (toggle) ordered.Add(midPt);
        ordered.AddRange(sideA);
        ordered.Add(endPt);

        // Drop coincident neighbours by distance. The rounded string keys
        // missed near-duplicates that rounded differently (1e-7 apart
        // across a rounding boundary).
        double tol = RhinoDoc.ActiveDoc != null
            ? RhinoDoc.ActiveDoc.ModelAbsoluteTolerance
            : 1e-6;

        foreach (var p in ordered)
        {
            if (output.Count == 0 || p.DistanceTo(output[output.Count - 1]) > tol)
                output.Add(p);
        }

        // Closed curve: end point is the start point again
        if (output.Count > 1 && output[0].DistanceTo(output[output.Count - 1]) <= tol)
            output.RemoveAt(output.Count - 1);

        return output;
    }

  // ---------------------------------------------------------------- pin tooltips
  private bool _pinTipsSet = false;

  private void SetPinTips()
  {
    if (_pinTipsSet || Component == null) return;
    _pinTipsSet = true;
    Component.Name = "Center Divide";
    Component.Description = "Divides curves into points spaced Distance apart, symmetric about each curve's middle. "
                           + "Works on items, lists and trees; output {branch;curve}. Rajeev Pulari, v2.1.";
    TipPin(Component.Params.Input, "Curve", "The curves to divide - item, list, list of lists or tree.");
    TipPin(Component.Params.Input, "Distance", "Spacing between points, per branch (same path, else the first branch). One per curve; a shorter list reuses its last value. Missing or ≤ 0 → 1.");
    TipPin(Component.Params.Input, "Toggle", "Middle mode per branch, one per curve (last value reused): True = a point at the middle, False = a gap at the middle (±D/2), unplugged = auto (no short end pieces). Also accepts 1/0, yes/no, on/off.");
    TipPin(Component.Params.Output, "OUT", "The points, one branch per curve: {input branch ; curve index}, in order from start to end.");
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
