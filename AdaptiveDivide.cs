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
    //   Crv      - curve to divide (any curve: NURBS, polycurve, line, arc, circle ...)
    //   ForcePts - optional points; the division always passes through their
    //              closest points on the curve
    //   CullIdx  - optional indices of output points to remove (-1 = last)
    //   Tol      - max distance allowed between the curve and the polyline
    //              (0 / unplugged = 0.01)
    //   MaxSeg   - max number of polyline segments (0 / unplugged = 100, minimum 4)
    // Outputs:
    //   P  - division points
    //   PL - polyline through the points
    private void RunScript(
		object Crv,
		List<Point3d> ForcePts,
		List<int> CullIdx,
		double Tol,
		int MaxSeg,
		ref object P,
		ref object PL)
    {
        // --- VISUAL META DATA (GRASSHOPPER CANVAS UI) ---
        Component.Name = "Adaptive Variable Divide";
        Component.NickName = "AdaptDiv";
        Component.Message = "AdaptDiv 2.0";
        Component.Description = "Divides a curve adaptively based on maximum curvature deviation.";

        // 1. Safe Casting Curve
        // GH_Convert.ToCurve on the raw input also accepts Line, Arc,
        // Circle and Polyline values. The old "is Curve / is GH_Curve"
        // check returned nothing for those (e.g. a curve coming from a
        // Line or Circle component arrives as a Line/Circle, not a Curve).
        Curve crv = null;
        if (Crv is Curve) crv = (Curve)Crv;
        else GH_Convert.ToCurve(Crv, ref crv, GH_Conversion.Both);

        if (crv == null) return;

        // 2. Safely parse Custom Points (ForcePts)
        List<Point3d> inputPoints = new List<Point3d>();
        if (ForcePts != null)
        {
            foreach (object obj in ForcePts)
            {
                Point3d pOut = Point3d.Unset;
                if (GH_Convert.ToPoint3d(obj, ref pOut, GH_Conversion.Both))
                {
                    inputPoints.Add(pOut);
                }
            }
        }

        // 3. Safely parse Cull Indices
        List<int> rawCullIndices = new List<int>();
        if (CullIdx != null)
        {
            foreach (object obj in CullIdx)
            {
                int idx = 0;
                if (GH_Convert.ToInt32(obj, out idx, GH_Conversion.Both))
                {
                    rawCullIndices.Add(idx);
                }
            }
        }

        // 4. Tolerance
        // A double input can never be null, so an unplugged Tol arrives as 0.
        // Before, 0 fell through to the 0.001 fallback instead of the 0.01
        // default. 0 or less now means "use the default".
        double tol = (Tol > 0) ? Tol : 0.01;

        // 5. Max Edges
        // Same for an int: unplugged MaxSeg arrives as 0, which was raised
        // to the minimum of 4 -- the 4 starting segments already reach
        // that, so no adaptive division happened at all. 0 or less now
        // means the default of 100; 1-3 still become 4.
        int maxEdges = (MaxSeg > 0) ? MaxSeg : 100;
        if (maxEdges < 4) maxEdges = 4;

        // 6. Bootstrap initial segments & inject custom points
        List<double> rawParams = new List<double>
        {
            crv.Domain.Min,
            crv.Domain.Min + crv.Domain.Length * 0.25,
            crv.Domain.Min + crv.Domain.Length * 0.5,
            crv.Domain.Min + crv.Domain.Length * 0.75,
            crv.Domain.Max
        };

        foreach (Point3d point in inputPoints)
        {
            double t;
            if (crv.ClosestPoint(point, out t))
            {
                rawParams.Add(t);
            }
        }

        rawParams.Sort();

        List<double> tParams = new List<double>();
        tParams.Add(rawParams[0]);
        for (int i = 1; i < rawParams.Count; i++)
        {
            if (Math.Abs(rawParams[i] - tParams[tParams.Count - 1]) > 1e-6)
            {
                tParams.Add(rawParams[i]);
            }
        }

        // 7. Core Adaptive Logic
        bool toleranceMet = false;

        while (tParams.Count - 1 < maxEdges && !toleranceMet)
        {
            double globalMaxDev = -1.0;
            int insertIndex = -1;
            double globalBestT = -1.0;

            for (int i = 0; i < tParams.Count - 1; i++)
            {
                double t0 = tParams[i];
                double t1 = tParams[i + 1];

                double localWorstT;
                double localMaxDev = GetMaxDeviation(crv, t0, t1, out localWorstT);

                if (localMaxDev > globalMaxDev)
                {
                    globalMaxDev = localMaxDev;
                    globalBestT = localWorstT;
                    insertIndex = i + 1;
                }
            }

            if (globalMaxDev > tol)
            {
                tParams.Insert(insertIndex, globalBestT);
            }
            else
            {
                toleranceMet = true;
            }
        }

        // 8. Generate Base Output Points
        List<Point3d> pts = new List<Point3d>();
        foreach (double t in tParams)
        {
            pts.Add(crv.PointAt(t));
        }

        if (crv.IsClosed)
        {
            pts[pts.Count - 1] = pts[0];
        }

        // 9. Apply Culling Logic with Negative Wrapping
        HashSet<int> processedCullIndices = new HashSet<int>();
        foreach (int index in rawCullIndices)
        {
            int actualIndex = index;
            if (actualIndex < 0) actualIndex += pts.Count;
            processedCullIndices.Add(actualIndex);
        }

        List<Point3d> finalPts = new List<Point3d>();
        for (int i = 0; i < pts.Count; i++)
        {
            if (!processedCullIndices.Contains(i))
            {
                finalPts.Add(pts[i]);
            }
        }

        // 10. Assign Outputs
        P = finalPts;
        if (finalPts.Count >= 2) PL = new Polyline(finalPts);
        else PL = null;
    }

    private double GetMaxDeviation(Curve crv, double t0, double t1, out double worstT)
    {
        Point3d p0 = crv.PointAt(t0);
        Point3d p1 = crv.PointAt(t1);
        Line segmentLine = new Line(p0, p1);

        worstT = t0 + (t1 - t0) * 0.5;
        double maxDev = -1.0;

        double[] sampleFactors = { 0.25, 0.5, 0.75 };

        foreach (double f in sampleFactors)
        {
            double t = t0 + (t1 - t0) * f;
            Point3d p = crv.PointAt(t);
            double dist = p.DistanceTo(segmentLine.ClosestPoint(p, true));

            if (dist > maxDev)
            {
                maxDev = dist;
                worstT = t;
            }
        }

        return maxDev;
    }
}
