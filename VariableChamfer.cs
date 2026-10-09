using System;
using System.Collections;
using System.Collections.Generic;
using Rhino;
using Rhino.Geometry;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

public class Script_Instance : GH_ScriptInstance
{
    // Inputs:
    //   Crv  - polyline to chamfer (other curves are converted to a polyline first)
    //   Dist - chamfer distances; the 1st chamfered corner uses Dist[0], the
    //          2nd Dist[1] ..., cycling back to the start if the list is shorter
    //   Idx  - vertex indices to chamfer (-1 = last vertex); empty = all corners
    // Output:
    //   PLine - the chamfered polyline
    private void RunScript(object Crv, List<double> Dist, List<int> Idx, ref object PLine)
    {
    SetPinTips();   // pin tooltips (set once, matched by name)

        // --- VISUAL META DATA ---
        Component.Name = "Variable Target Chamfer";
        Component.NickName = "VarChamfer 2.0";
        Component.Message = "Pro Mode";
        Component.Description = "Chamfers specific polyline corners with variable distances and smart clamping.";

        double tol = RhinoDoc.ActiveDoc != null ? RhinoDoc.ActiveDoc.ModelAbsoluteTolerance : 0.001;
        double angTol = RhinoDoc.ActiveDoc != null ? RhinoDoc.ActiveDoc.ModelAngleToleranceRadians : RhinoMath.ToRadians(1.0);

        // 1. Safe Casting Curve
        // GH_Convert.ToCurve on the raw input also accepts Polyline, Line,
        // Arc and Circle values, which matched neither "is Curve" nor
        // "is GH_Curve" before and gave no output.
        Curve crv = null;
        if (Crv is Curve) crv = (Curve)Crv;
        else GH_Convert.ToCurve(Crv, ref crv, GH_Conversion.Both);

        if (crv == null) return;

        // 2. Safely Parse Distances (Dist) into a List
        List<double> dists = new List<double>();
        if (Dist != null)
        {
            foreach (object obj in Dist)
            {
                double dOut = 0.0;
                // Grasshopper SDK: Primitives use 'out'
                if (GH_Convert.ToDouble(obj, out dOut, GH_Conversion.Both))
                {
                    dists.Add(dOut);
                }
            }
        }
        if (dists.Count == 0) dists.Add(0.0); // Fallback to 0 if list is empty

        // 3. Safely Parse Indices (Idx) into a List
        List<int> targetIndices = new List<int>();
        if (Idx != null)
        {
            foreach (object obj in Idx)
            {
                int iOut = 0;
                // Grasshopper SDK: Primitives use 'out'
                if (GH_Convert.ToInt32(obj, out iOut, GH_Conversion.Both))
                {
                    targetIndices.Add(iOut);
                }
            }
        }

        // 4. Extract or Convert to Polyline
        Polyline poly;
        if (!crv.TryGetPolyline(out poly))
        {
            // ToPolyline(tolerance, angleTolerance, minimumLength, maximumLength).
            // Before, the curve's Domain.Min was passed as the ANGLE
            // tolerance (0 for most curves, or e.g. 100 radians for a curve
            // with domain 100..200), so the conversion ignored the model's
            // angle tolerance. Now uses the model's angle tolerance, with no
            // min/max segment length.
            PolylineCurve plc = crv.ToPolyline(tol, angTol, 0, 0);
            if (plc != null) plc.TryGetPolyline(out poly);
        }

        if (poly == null || poly.Count < 3)
        {
            PLine = crv; // Cannot chamfer a simple line
            return;
        }

        // 5. Pre-process logic for wrapping and boundaries
        bool isClosed = poly.IsClosed;
        int count = poly.Count;
        int end = isClosed ? count - 1 : count; // The number of unique vertices

        // Convert raw indices into a fast HashSet, mapping negative numbers correctly
        HashSet<int> activeIndices = new HashSet<int>();
        if (targetIndices.Count > 0)
        {
            foreach (int idx in targetIndices)
            {
                int actualIdx = idx;
                if (actualIdx < 0) actualIdx += end;
                activeIndices.Add(actualIdx);
            }
        }

        // 6. Core Variable Chamfer Logic
        List<Point3d> newPts = new List<Point3d>();
        int chamferCounter = 0; // Tracks how many chamfers we've applied to cycle through 'Dist'

        for (int v = 0; v < end; v++)
        {
            // If the curve is open, we cannot chamfer the absolute first and last points
            if (!isClosed && (v == 0 || v == end - 1))
            {
                newPts.Add(poly[v]);
                continue;
            }

            // Determine if the current vertex should be chamfered
            // If the user provided no indices, we chamfer EVERYTHING. Otherwise, check the HashSet.
            bool doChamfer = (targetIndices.Count == 0) || activeIndices.Contains(v);

            if (!doChamfer)
            {
                newPts.Add(poly[v]);
                continue;
            }

            // Get the specific distance for this operation, looping if 'Dist' is shorter than the corners
            double currentDist = dists[chamferCounter % dists.Count];
            chamferCounter++;

            if (currentDist <= 0)
            {
                newPts.Add(poly[v]);
                continue;
            }

            // Calculate neighboring indices with wrap-around support for closed curves
            int prevIdx = isClosed ? (v - 1 + end) % end : v - 1;
            int nextIdx = isClosed ? (v + 1) % end : v + 1;

            Point3d pPrev = poly[prevIdx];
            Point3d pCurr = poly[v];
            Point3d pNext = poly[nextIdx];

            Vector3d vIn = pPrev - pCurr;
            Vector3d vOut = pNext - pCurr;

            double l1 = vIn.Length;
            double l2 = vOut.Length;

            if (l1 < 1e-6 || l2 < 1e-6)
            {
                newPts.Add(poly[v]); // Skip degenerate micro-segments safely
                continue;
            }

            // SMART CLAMPING: Restrict chamfer to 50% of the segment length to prevent tangling
            double d1 = Math.Min(currentDist, l1 * 0.5);
            double d2 = Math.Min(currentDist, l2 * 0.5);

            vIn.Unitize();
            vOut.Unitize();

            newPts.Add(pCurr + vIn * d1);
            newPts.Add(pCurr + vOut * d2);
        }

        // Remove repeated points. When two neighbouring corners are both
        // clamped to 50% of their shared segment, their chamfer points land
        // on the same midpoint -- a zero-length segment in the output
        // polyline, which makes it an invalid curve for many operations.
        List<Point3d> cleanPts = new List<Point3d>();
        foreach (Point3d p in newPts)
        {
            if (cleanPts.Count == 0 || p.DistanceTo(cleanPts[cleanPts.Count - 1]) > tol)
                cleanPts.Add(p);
        }
        if (isClosed && cleanPts.Count > 1 && cleanPts[0].DistanceTo(cleanPts[cleanPts.Count - 1]) <= tol)
            cleanPts.RemoveAt(cleanPts.Count - 1);

        // Re-close the loop for closed curves
        if (isClosed && cleanPts.Count > 0)
        {
            cleanPts.Add(cleanPts[0]);
        }

        // 7. Output the final Polyline Curve
        PLine = new Polyline(cleanPts);
    }

  // ---------------------------------------------------------------- pin tooltips
  private bool _pinTipsSet = false;

  private void SetPinTips()
  {
    if (_pinTipsSet || Component == null) return;
    _pinTipsSet = true;
    TipPin(Component.Params.Input, "Crv", "The polyline. Other curves are first converted to a polyline.");
    TipPin(Component.Params.Input, "Dist", "Chamfer distance, measured from the corner along each edge.");
    TipPin(Component.Params.Input, "Idx", "Which corners (vertex numbers, 0 = first; -1 = last). Empty → every corner.");
    TipPin(Component.Params.Output, "PLine", "The chamfered polyline.");
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
