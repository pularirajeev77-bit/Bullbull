/*
  Author: Rajeev Pulari + ChatGPT
  Rhino 8 | Grasshopper C#
  Version: 2026.04.05
  Component: InternalAngleFilter
  Description:
    Detects vertices where the INTERNAL angle is LESS than the threshold.
    Inputs:
      • Crv → Curve (Polyline/Polycurve)
      • Ang → Maximum internal angle threshold (radians)
    Outputs:
      • P → Points where internal angle < Ang
      • N → Bisector vector at that corner (pointing inside the shape)
      • t → Parameter values on Crv
*/

using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Geometry;
using Grasshopper.Kernel;

public class Script_Instance : GH_ScriptInstance
{
    private void RunScript(
		Curve Crv,
		double Ang,
		ref object P,
		ref object N,
		ref object t)
    {
    SetPinTips();   // pin tooltips (set once, matched by name)

        this.Component.Message = "Internal Angle v2.0";
        this.Component.NickName = "IntAng";

        var outP = new List<Point3d>();
        var outN = new List<Vector3d>();
        var outT = new List<double>();

        double tol = RhinoDoc.ActiveDoc != null
            ? RhinoDoc.ActiveDoc.ModelAbsoluteTolerance
            : RhinoMath.ZeroTolerance;

        try
        {
            if (Crv == null || !Crv.TryGetPolyline(out Polyline pline))
            {
                P = outP; N = outN; t = outT;
                return;
            }

            // Merge repeated points. Before, a corner drawn with a doubled
            // vertex (A, B, B, C) was never tested: each copy of B had a
            // zero-length edge on one side and was skipped.
            pline.DeleteShortSegments(tol);

            var pts = new List<Point3d>(pline);
            bool isClosed = Crv.IsClosed;
            if (isClosed && pts.Count >= 2 && pts[0].DistanceTo(pts[pts.Count - 1]) <= tol)
                pts.RemoveAt(pts.Count - 1);  // treat as n unique vertices

            int count = pts.Count;
            if (count < 3) { P = outP; N = outN; t = outT; return; }

            // For a closed polyline: the Newell normal. It always points so
            // the polyline runs counter-clockwise around it, which tells us
            // which side is inside -- needed to tell a sharp corner from a
            // concave (reflex) one.
            Vector3d polyNormal = Vector3d.Zero;
            bool useInside = false;
            if (isClosed)
            {
                polyNormal = NewellNormal(pts);
                useInside = polyNormal.Unitize();
            }

            for (int i = 0; i < count; i++)
            {
                if (!isClosed && (i == 0 || i == count - 1)) continue;

                Point3d curr = pts[i];
                Point3d prev = pts[(i + count - 1) % count];
                Point3d next = pts[(i + 1) % count];

                Vector3d uIn = curr - prev;
                Vector3d uOut = next - curr;
                if (!uIn.Unitize() || !uOut.Unitize()) continue;

                double angle;
                bool reflex = false;

                if (useInside)
                {
                    // TRUE internal angle, 0..360°. Before, VectorAngle only
                    // gave 0..180°, so the concave corner of an L-shape
                    // (270° inside) was reported as 90° and flagged as sharp.
                    double turn = Math.Atan2(
                        Vector3d.CrossProduct(uIn, uOut) * polyNormal,
                        uIn * uOut);
                    angle = Math.PI - turn;
                    reflex = angle > Math.PI;
                }
                else
                {
                    // Open polyline: no inside, so use the angle between
                    // the two edges (0..180°)
                    angle = Vector3d.VectorAngle(-uIn, uOut);
                }

                if (angle < Ang && angle > RhinoMath.ZeroTolerance)
                {
                    outP.Add(curr);

                    // Parameter on the input curve itself. Before, this was
                    // the Polyline's own index parameter (0, 1, 2 ... per
                    // vertex), which doesn't match Crv's domain.
                    Crv.ClosestPoint(curr, out double tc);
                    outT.Add(tc);

                    // Bisector of the corner, pointing inside the shape
                    Vector3d bisector = -uIn + uOut;
                    if (reflex) bisector = -bisector;
                    if (!bisector.Unitize())
                    {
                        // Straight vertex (only reached when Ang > 180°)
                        Vector3d up = useInside ? polyNormal : Vector3d.ZAxis;
                        bisector = Vector3d.CrossProduct(up, uIn);
                        if (!bisector.Unitize()) bisector.PerpendicularTo(uIn);
                        bisector.Unitize();
                    }
                    outN.Add(bisector);
                }
            }
        }
        catch (Exception ex)
        {
            // Red balloon on the component, not only the output window
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Error: " + ex.Message);
        }

        P = outP;
        N = outN;
        t = outT;
    }

    // Normal of a closed polygon (Newell's method), oriented so the
    // points run counter-clockwise around it
    private Vector3d NewellNormal(List<Point3d> pts)
    {
        var n = Vector3d.Zero;
        for (int i = 0; i < pts.Count; i++)
        {
            Point3d a = pts[i];
            Point3d b = pts[(i + 1) % pts.Count];
            n.X += (a.Y - b.Y) * (a.Z + b.Z);
            n.Y += (a.Z - b.Z) * (a.X + b.X);
            n.Z += (a.X - b.X) * (a.Y + b.Y);
        }
        return n;
    }

  // ---------------------------------------------------------------- pin tooltips
  private bool _pinTipsSet = false;

  private void SetPinTips()
  {
    if (_pinTipsSet || Component == null) return;
    _pinTipsSet = true;
    TipPin(Component.Params.Input, "Crv", "A polyline (a list works; you get one branch per curve)");
    TipPin(Component.Params.Input, "Ang", "Angle limit, in radians (90° = π/2 ≈ 1.5708)");
    TipPin(Component.Params.Output, "P", "The corner points with an angle smaller than Ang.");
    TipPin(Component.Params.Output, "N", "At each of those corners, the direction splitting the angle in half, pointing inside the shape.");
    TipPin(Component.Params.Output, "t", "Where each corner is on the curve (curve parameter)");
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
