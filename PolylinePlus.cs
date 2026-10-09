/*
  Author: Rajeev Pulari + Assistant
  Rhino 8 | Grasshopper C#
  Version: 2026.04.11
  Component: PolylinePlus – Multi-Mode Polyline 2.1

  Description:
    Creates joined curves (polyline + arc segments) branch-wise.
    • If arc ranges are given (ArcStartIdx, ArcEndIdx) → arcs are fitted.
    • Otherwise → pure polyline.
    • Optionally closes the curve based on the 'Close' boolean.
    • Preserves input tree structure of Pts.
*/

using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Geometry;
using Grasshopper;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel;

public class Script_Instance : GH_ScriptInstance
{
    // Inputs:
    //   Pts         - points, tree access: one curve is made per branch
    //   ArcStartIdx - point index where each arc starts (same for every branch)
    //   ArcEndIdx   - point index where each arc ends, paired with ArcStartIdx
    //   Close       - true: close the curve back to the first point
    // Output:
    //   Crv         - one joined curve per branch (null for branches with < 2 points)
    private void RunScript(
		DataTree<Point3d> Pts,
		List<int> ArcStartIdx,
		List<int> ArcEndIdx,
		bool Close,
		ref object Crv)
    {
    SetPinTips();   // pin tooltips (set once, matched by name)

        // 1. Set Component Metadata (Rhino 8 Native)
        this.Component.Message = "Polyline Plus v2.1";
        this.Component.NickName = "PolyPlus";

        // 2. Setup & Output Initialization
        double tol = RhinoDoc.ActiveDoc?.ModelAbsoluteTolerance ?? 0.001;
        var resultTree = new DataTree<object>();

        try
        {
            if (Pts == null || Pts.BranchCount == 0)
            {
                // Warning balloon + empty tree. Before, the text
                // "⚠ Empty input Pts tree." was sent out as if it were
                // a curve, which breaks anything downstream.
                this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Empty input Pts tree.");
                Crv = resultTree;
                return;
            }

            // Collect arc ranges into a modern C# tuple list
            var arcRanges = new List<(int start, int end)>();
            if (ArcStartIdx != null && ArcEndIdx != null)
            {
                if (ArcStartIdx.Count == ArcEndIdx.Count)
                {
                    for (int j = 0; j < ArcStartIdx.Count; j++)
                        arcRanges.Add((ArcStartIdx[j], ArcEndIdx[j]));
                }
                else
                {
                    // Before: all arcs were silently dropped
                    this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        "ArcStartIdx and ArcEndIdx have different counts - no arcs made.");
                }
            }

            // 3. Process each branch independently
            for (int b = 0; b < Pts.BranchCount; b++)
            {
                GH_Path path = Pts.Path(b);
                List<Point3d> pts = Pts.Branch(b);

                if (pts == null || pts.Count < 2)
                {
                    resultTree.Add(null, path);
                    continue;
                }

                var segments = new List<Curve>();
                int i = 0;

                while (i < pts.Count - 1)
                {
                    bool isArc = false;
                    int arcEnd = -1;

                    // Detect arc start using tuple decomposition
                    foreach (var (rStart, rEnd) in arcRanges)
                    {
                        if (i == rStart)
                        {
                            isArc = true;
                            arcEnd = rEnd;
                            break;
                        }
                    }

                    if (isArc && arcEnd > i + 1 && arcEnd < pts.Count)
                    {
                        // Construct Arc through start, a middle point and end.
                        // The middle point is the one halfway through the
                        // range (was always pts[i + 1]). For points on a true
                        // arc it's the same arc; for long ranges it's far
                        // better conditioned than a point right next to the
                        // start, and the arc follows the points more closely.
                        Point3d p0 = pts[i];
                        Point3d pm = pts[(i + arcEnd) / 2];
                        Point3d p2 = pts[arcEnd];
                        bool ok = false;

                        try
                        {
                            Arc arc = new Arc(p0, pm, p2);
                            if (arc.IsValid)
                            {
                                segments.Add(arc.ToNurbsCurve());
                                ok = true;
                            }
                        }
                        catch { /* Fallback to line */ }

                        if (!ok) AddLine(segments, p0, p2, tol);

                        i = arcEnd;
                    }
                    else
                    {
                        // Construct Line Segment
                        AddLine(segments, pts[i], pts[i + 1], tol);
                        i += 1;
                    }
                }

                // Append closing segment if Close is true and start/end points don't already match
                if (Close)
                {
                    Point3d firstPt = pts[0];
                    Point3d lastPt = pts[pts.Count - 1];

                    if (firstPt.DistanceTo(lastPt) > tol)
                    {
                        AddLine(segments, lastPt, firstPt, tol);
                    }
                }

                if (segments.Count == 0)
                {
                    resultTree.Add(null, path);
                    continue;
                }

                // Join segments back into a single curve per branch
                Curve[] joined = Curve.JoinCurves(segments, tol);
                if (joined != null && joined.Length > 0)
                {
                    if (joined.Length > 1)
                        this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                            "Branch " + path + ": segments did not join into one curve; only the first piece is output.");
                    resultTree.Add(joined[0], path);
                }
                else
                {
                    resultTree.Add(null, path);
                }
            }
        }
        catch (Exception ex)
        {
            // Red balloon on the component, not only the output window
            this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Error in PolyPlus: " + ex.Message);
        }

        // 4. Final Assignment
        Crv = resultTree;
    }

    // Adds a straight segment, skipping zero-length ones. Before, two
    // identical points in a row made Line.ToNurbsCurve() return null; the
    // null went into JoinCurves, which threw -- and that error wiped out the
    // curves of every branch after it, not just the one with the duplicate.
    private void AddLine(List<Curve> segments, Point3d a, Point3d b, double tol)
    {
        if (a.DistanceTo(b) <= tol) return;
        Curve c = new Line(a, b).ToNurbsCurve();
        if (c != null) segments.Add(c);
    }

  // ---------------------------------------------------------------- pin tooltips
  private bool _pinTipsSet = false;

  private void SetPinTips()
  {
    if (_pinTipsSet || Component == null) return;
    _pinTipsSet = true;
    TipPin(Component.Params.Input, "Pts", "The points, in order. One curve per branch.");
    TipPin(Component.Params.Input, "ArcStartIdx", "Point number where each arc starts (0 = first point)");
    TipPin(Component.Params.Input, "ArcEndIdx", "Point number where each arc ends — paired with ArcStartIdx (1st start ↔ 1st end …)");
    TipPin(Component.Params.Input, "Close", "True → adds a straight segment back to the first point.");
    TipPin(Component.Params.Output, "Crv", "One joined curve per branch, same branch paths as Pts.");
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
