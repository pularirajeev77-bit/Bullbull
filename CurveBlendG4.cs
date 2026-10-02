#region Metadata
/*
  Platform    : Rhino 8 | Grasshopper C#
  Component   : Curve Blend G0-G4
  NickName    : BlendCrv
  Message     : Blend G0-G4 v1.1
  Description : Blend curve between the end of CurveA and the start of CurveB
                (like Rhino BlendCrv) with G0 to G4 continuity on each side and
                per-order bulge control. The result is a single Bezier span of
                degree ContinuityA + ContinuityB + 1, built from the exact curve
                derivatives (Faa di Bruno re-parameterisation).

  Inputs:
    CurveA, CurveB           : Curve (Item) - blend from the END of A to the START of B
    FlipA, FlipB             : bool  (Item) - use the other end (A start / B end)
    BulgeA, BulgeB           : List<double> (List) - bulge per order [G1, G2, G3, G4]; empty = 1
    ContinuityA, ContinuityB : int   (Item) - 0 = G0 position, 1 = G1 tangent, 2 = G2 curvature,
                                              3 = G3, 4 = G4 (clamped to 0..4)

  Output:
    BlendCurve : the blend curve
*/
#endregion

using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Geometry;
using Grasshopper.Kernel;

public class Script_Instance : GH_ScriptInstance
{
    private void RunScript(
        Curve CurveA,
        Curve CurveB,
        bool FlipA,
        bool FlipB,
        List<double> BulgeA,
        List<double> BulgeB,
        int ContinuityA,
        int ContinuityB,
        ref object BlendCurve)
    {
        // Metadata + pin tooltips (once)
        if (Component != null && Component.Name != "Curve Blend G0-G4")
        {
            Component.Name = "Curve Blend G0-G4";
            Component.NickName = "BlendCrv";
            Component.Message = "Blend G0-G4 v1.1";
            Component.Description = "Blend curve between two curve ends with G0-G4 continuity per side and bulge control.";

            var pi = Component.Params.Input;
            SetTip(pi, "CurveA", "Blend starts at the END of this curve (START with FlipA).");
            SetTip(pi, "CurveB", "Blend ends at the START of this curve (END with FlipB).");
            SetTip(pi, "FlipA", "Use the start of CurveA instead of its end.");
            SetTip(pi, "FlipB", "Use the end of CurveB instead of its start.");
            SetTip(pi, "BulgeA", "Bulge per order on side A: [G1, G2, G3, G4]. Shorter list repeats its last value. Empty = 1. List access.");
            SetTip(pi, "BulgeB", "Bulge per order on side B: [G1, G2, G3, G4]. Empty = 1. List access.");
            SetTip(pi, "ContinuityA", "0 = G0 position, 1 = G1 tangent, 2 = G2 curvature, 3 = G3, 4 = G4.");
            SetTip(pi, "ContinuityB", "0 = G0 position, 1 = G1 tangent, 2 = G2 curvature, 3 = G3, 4 = G4.");
            SetTip(Component.Params.Output, "BlendCurve", "The blend curve (one Bezier span).");
        }

        // AddRuntimeMessage lives on the component (this.AddRuntimeMessage does not exist on the script instance)
        if (CurveA == null || CurveB == null)
        {
            string miss = (CurveA == null && CurveB == null) ? "Provide both CurveA and CurveB."
                        : (CurveA == null ? "CurveA is missing." : "CurveB is missing.");
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, miss);
            return;
        }

        int contA = Math.Max(0, Math.Min(4, ContinuityA));
        int contB = Math.Max(0, Math.Min(4, ContinuityB));
        if (contA != ContinuityA || contB != ContinuityB)
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Continuity is limited to 0..4 - clamped.");

        if (HasNonPositive(BulgeA) || HasNonPositive(BulgeB))
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Bulge values of 0 or less are treated as 0 and flatten the blend.");

        double tA = FlipA ? CurveA.Domain.T0 : CurveA.Domain.T1;
        double tB = FlipB ? CurveB.Domain.T1 : CurveB.Domain.T0;

        Point3d ptA = CurveA.PointAt(tA);
        Point3d ptB = CurveB.PointAt(tB);
        Component.Message = "Blend G" + contA + " / G" + contB;

        if (contA == 0 && contB == 0)
        {
            BlendCurve = new LineCurve(ptA, ptB);
            return;
        }

        // Exact derivatives, evaluated from the inside of each curve
        // (the old finite differences divided by h^4 and crossed knots, so G3/G4 were noisy)
        Vector3d d1A, d2A, d3A, d4A, d1B, d2B, d3B, d4B;
        Derivatives(CurveA, tA, !FlipA, out d1A, out d2A, out d3A, out d4A);
        Derivatives(CurveB, tB, FlipB, out d1B, out d2B, out d3B, out d4B);

        // Reversing a curve flips the odd derivatives
        if (FlipA) { d1A = -d1A; d3A = -d3A; }
        if (FlipB) { d1B = -d1B; d3B = -d3B; }

        double dist = ptA.DistanceTo(ptB);
        if (dist < 1e-12)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Connection points are nearly coincident. Blend may be degenerate.");
            dist = 1e-12;
        }
        if (d1A.Length < 1e-12 || d1B.Length < 1e-12)
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "A curve has no tangent at the blend end (degenerate end) - result may be poor.");

        double baseA = (d1A.Length > 1e-12) ? dist / d1A.Length : 1.0;
        double baseB = (d1B.Length > 1e-12) ? dist / d1B.Length : 1.0;

        // Bezier degree = sum of both continuity levels + 1
        int n = contA + contB + 1;

        Vector3d s1A, s2A, s3A, s4A, s1B, s2B, s3B, s4B;
        GContinuityDerivatives(BulgeA, baseA, n, contA, d1A, d2A, d3A, d4A, out s1A, out s2A, out s3A, out s4A, 1);
        GContinuityDerivatives(BulgeB, baseB, n, contB, d1B, d2B, d3B, d4B, out s1B, out s2B, out s3B, out s4B, -1);

        Vector3d[] sA = { Vector3d.Zero, s1A, s2A, s3A, s4A };
        Vector3d[] sB = { Vector3d.Zero, s1B, s2B, s3B, s4B };

        // A side: P_0..P_contA from B^(k)(0) = perm(n,k) * delta^k P_0
        var cpA = new Vector3d[contA + 1];
        cpA[0] = Vec(ptA);
        for (int k = 1; k <= contA; k++)
        {
            cpA[k] = sA[k] / Perm(n, k);
            for (int j = 0; j < k; j++)
            {
                double sign = ((k - j) % 2 == 0) ? 1.0 : -1.0;
                cpA[k] -= sign * Binom(k, j) * cpA[j];
            }
        }

        // B side: w_0 = P_n, w_1 = P_(n-1) ... from B^(k)(1)
        var cpB = new Vector3d[contB + 1];
        cpB[0] = Vec(ptB);
        for (int k = 1; k <= contB; k++)
        {
            Vector3d inner = sB[k] / Perm(n, k);
            for (int j = 0; j < k; j++)
            {
                double signJ = (j % 2 == 0) ? 1.0 : -1.0;
                inner -= signJ * Binom(k, j) * cpB[j];
            }
            cpB[k] = ((k % 2 == 0) ? 1.0 : -1.0) * inner;
        }

        var allCp = new Vector3d[n + 1];
        for (int i = 0; i <= contA; i++) allCp[i] = cpA[i];
        for (int i = 0; i <= contB; i++) allCp[n - i] = cpB[i];

        var blend = new NurbsCurve(3, false, n + 1, n + 1);   // single Bezier span, degree n
        for (int i = 0; i <= n; i++) blend.Points.SetPoint(i, new Point3d(allCp[i].X, allCp[i].Y, allCp[i].Z));
        for (int i = 0; i < n; i++) blend.Knots[i] = 0.0;
        for (int i = n; i < 2 * n; i++) blend.Knots[i] = 1.0;

        if (!blend.IsValid)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "The blend curve is not valid.");
            return;
        }
        BlendCurve = blend;
    }

    // <Custom additional code>

    // d1..d4 at t; fromInsideBelow = evaluate from below the parameter (curve END)
    private static void Derivatives(Curve crv, double t, bool fromInsideBelow,
                                    out Vector3d d1, out Vector3d d2, out Vector3d d3, out Vector3d d4)
    {
        Vector3d[] d = crv.DerivativeAt(t, 4, fromInsideBelow ? CurveEvaluationSide.Below : CurveEvaluationSide.Above);
        d1 = (d != null && d.Length > 1) ? d[1] : Vector3d.Zero;
        d2 = (d != null && d.Length > 2) ? d[2] : Vector3d.Zero;
        d3 = (d != null && d.Length > 3) ? d[3] : Vector3d.Zero;
        d4 = (d != null && d.Length > 4) ? d[4] : Vector3d.Zero;
    }

    // Derivatives of the curve re-parameterised for G-continuity (Faa di Bruno)
    private static void GContinuityDerivatives(
        List<double> bulges, double baseVal, int n, int cont,
        Vector3d c1, Vector3d c2, Vector3d c3, Vector3d c4,
        out Vector3d s1, out Vector3d s2, out Vector3d s3, out Vector3d s4,
        int evenCorrectionSign)
    {
        double b0 = Bulge(bulges, 0);
        double a1 = b0 * baseVal;
        double a2 = 0, a3 = 0, a4 = 0;
        if (cont >= 2) a2 = evenCorrectionSign * (Bulge(bulges, 1) - b0) * Perm(n - 1, 1) * baseVal;
        if (cont >= 3) a3 = (Bulge(bulges, 2) - b0) * Perm(n - 1, 2) * baseVal;
        if (cont >= 4) a4 = evenCorrectionSign * (Bulge(bulges, 3) - b0) * Perm(n - 1, 3) * baseVal;

        s1 = a1 * c1;
        s2 = a1 * a1 * c2 + a2 * c1;
        s3 = a1 * a1 * a1 * c3 + 3.0 * a1 * a2 * c2 + a3 * c1;
        s4 = a1 * a1 * a1 * a1 * c4 + 6.0 * a1 * a1 * a2 * c3 + (4.0 * a1 * a3 + 3.0 * a2 * a2) * c2 + a4 * c1;
    }

    private static double Bulge(List<double> bulges, int index)
    {
        if (bulges == null || bulges.Count == 0) return 1.0;
        return Math.Max(bulges[Math.Min(index, bulges.Count - 1)], 0.0);
    }

    private static bool HasNonPositive(List<double> values)
    {
        if (values == null) return false;
        foreach (double v in values) if (v <= 0) return true;
        return false;
    }

    private static double Perm(int n, int k)
    {
        double r = 1.0;
        for (int i = 0; i < k; i++) r *= (n - i);
        return r;
    }

    private static double Binom(int n, int k)
    {
        if (k > n) return 0;
        if (k == 0 || k == n) return 1;
        double r = 1.0;
        for (int i = 0; i < k; i++) { r *= (n - i); r /= (i + 1); }
        return r;
    }

    private static Vector3d Vec(Point3d p) { return new Vector3d(p.X, p.Y, p.Z); }

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
    // </Custom additional code>
}
