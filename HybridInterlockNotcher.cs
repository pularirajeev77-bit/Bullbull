// Grasshopper Script Instance
// Hybrid Interlock Notcher (Pro) - v2.1 (2026-10-03) - Rajeev Pulari
// Rhino 8 | Grasshopper C# Script. Branch: Bullbull/shape
// v2.1 fixes: see README (input-goo lookup with nulls, metadata wipe,
// 1.001 retry scale shifting the seat, silent failed cuts, tooltips).
#region Usings
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
#endregion

public class Script_Instance : GH_ScriptInstance
{
    #region Notes
    /*
      Members:
        RhinoDoc RhinoDocument
        GH_Document GrasshopperDocument
        IGH_Component Component
        int Iteration

      Methods (Virtual & overridable):
        Reflect(object obj)
        Reflect(object obj, string method_name)
    */
    #endregion

/*
================================================================================
Visual Meta Data:
- Component Name : Hybrid Interlock Notcher (Pro)   v2.1
- Author         : Rajeev Pulari
- Category       : Intersect
- Subcategory    : Shape
- Description    : Generates perfect 3D interlocking notches in ANY orientation.
                   Matrix edition: works dynamically across multiple inputs.
                   Hybrid Engine: Works on BOTH closed Solids and open Surfaces.
                   Free-Axis Engine: for every joint it measures the members
                   (principal-axis analysis), derives the true notch axis, seats
                   the joint on the measured middle of the intersection line and
                   cuts along it. No world-Z assumption.
                   Joint Engine: every measurement is taken from the UNCUT input,
                   so earlier cuts can never shift a later joint.
                   Triple-Lap Engine: an AddBrep passing through a Base x Cutter
                   joint is cut in the SAME frame as that joint - Base keeps one
                   side, AddBrep keeps the centred web, Cutter keeps the other side.
                   Attribute Engine: Preserves Object Names and Colors.
                   Bake Engine: Overwrites original inputs in Rhino automatically.
================================================================================
Inputs:
    BaseBreps    (List<Brep>) : Primary beams/surfaces. Keeps the POSITIVE side
                                of the joint axis (slot opens from the negative side).
    CutterBreps  (List<Brep>) : Intersecting beams/surfaces. Keeps the NEGATIVE
                                side of the joint axis (slot opens from the positive side).
    AddBreps     (List<Brep>) : Optional Breps receiving two-sided H-notches,
                                web centred on the joint's intersection line.
    Gap          (double)     : Clearance along the joint axis between mating parts.
    AddGap       (double)     : Web height left on AddBreps, centred on the joint.
    Tolerance    (double)     : In-plane clearance added to the slot width.
                                0 -> 10 x model tolerance.
    Bake         (bool)       : Connect a Button. True replaces inputs in Rhino.
Outputs:
    SlottedBases   (object) : Matches BaseBreps, preserving all attributes.
    SlottedCutters (object) : Matches CutterBreps, preserving all attributes.
    SlottedAdd     (object) : Matches AddBreps, preserving all attributes.

Joint layout along the joint axis (0 = middle of the intersection line):
    Base + Cutter only   : Base keeps  z > +Gap/2
                           Cutter keeps z < -Gap/2
    Base + Add + Cutter  : Base keeps  z > +(AddGap/2 + Gap)
                           Add keeps   -AddGap/2 < z < +AddGap/2
                           Cutter keeps z < -(AddGap/2 + Gap)
    Add + single member  : same H-notch, measured on that pair alone.

Notch axis derivation (per joint, in priority order):
    plate x plate : normalA x normalB   (the line the two planes share)
    plate x beam  : normalPlate x axisBeam
    beam  x beam  : axisA x axisB
    fallback      : principal axis of the intersection geometry itself
    last resort   : world Z
    The winning axis gets a deterministic sign (biased to +Z, then +Y, then +X)
    so Base and Cutter always land on opposite sides.
================================================================================
*/

    private void RunScript(
		List<Brep> BaseBreps,
		List<Brep> CutterBreps,
		List<Brep> AddBreps,
		double Gap,
		double AddGap,
		double Tolerance,
		bool Bake,
		ref object SlottedBases,
		ref object SlottedCutters,
		ref object SlottedAdd)
    {
        // Set Component Metadata for Rhino 8 (once) and reset per-solve counters
        SetMetadata();
        Component.Message = "Free-Axis Notch Matrix";
        _jointCount = 0;
        _failedCuts = 0;

        // Member frames are cached per solve, keyed on the uncut input geometry.
        _frameCache.Clear();

        // 1. Validation & Pro-Mode Iteration Check
        if (Iteration > 0)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Component is looping! Right-click all list inputs and set them to 'List Access' to prevent geometry duplication.");
        }

        if (BaseBreps == null || BaseBreps.Count == 0)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "BaseBreps list is empty or missing.");
            return;
        }
        if (CutterBreps == null || CutterBreps.Count == 0)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "CutterBreps list is empty or missing.");
            SlottedBases = BaseBreps;
            return;
        }
        if (Tolerance <= 0.0)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Tolerance is 0 - slots get only the minimum clearance (10 x model tolerance).");
            Tolerance = 0.0;
        }
        if (Gap <= 0.0)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Gap is 0. Notch cuts will be flush.");
            Gap = 0.0;
        }
        if (AddGap < 0.0)
        {
            AddGap = 0.0;
        }

        double tol = RhinoDocument.ModelAbsoluteTolerance;

        // 2. Initialize working lists maintaining exact 1-to-1 input index structure
        List<Brep> workingBases = new List<Brep>();
        foreach (Brep b in BaseBreps) workingBases.Add((b != null && b.IsValid) ? b.DuplicateBrep() : null);

        List<Brep> workingCutters = new List<Brep>();
        foreach (Brep c in CutterBreps) workingCutters.Add((c != null && c.IsValid) ? c.DuplicateBrep() : null);

        List<Brep> workingAdds = new List<Brep>();
        if (AddBreps == null || AddBreps.Count == 0)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "AddBreps is null or empty. SlottedAdd will be empty.");
        }
        else
        {
            foreach (Brep a in AddBreps) workingAdds.Add((a != null && a.IsValid) ? a.DuplicateBrep() : null);
            if (AddGap <= 0.0)
                Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "AddGap is 0. AddBreps have no web and will be cut clean through at every joint.");
        }

        // Uncut snapshots. Cuts only ever REPLACE list entries with new Breps, so these
        // references stay pristine and every joint is measured on the original geometry.
        List<Brep> pristineBases = new List<Brep>(workingBases);
        List<Brep> pristineCutters = new List<Brep>(workingCutters);
        List<Brep> pristineAdds = new List<Brep>(workingAdds);

        // 3. Joint Engine
        ProcessJoints(workingBases, workingCutters, workingAdds,
                      pristineBases, pristineCutters, pristineAdds,
                      Gap, AddGap, Tolerance, tol);

        // 4. Extract Attributes, Wrap Output Geometries, and Execute Replacement Bake
        List<object> finalBases = new List<object>();
        for (int i = 0; i < workingBases.Count; i++)
        {
            finalBases.Add(FinalizeOutput(BaseBreps[i], workingBases[i], 0, i));
            if (Bake && workingBases[i] != null) BakeGeometry(workingBases[i], 0, i);
        }

        List<object> finalCutters = new List<object>();
        for (int i = 0; i < workingCutters.Count; i++)
        {
            finalCutters.Add(FinalizeOutput(CutterBreps[i], workingCutters[i], 1, i));
            if (Bake && workingCutters[i] != null) BakeGeometry(workingCutters[i], 1, i);
        }

        List<object> finalAdds = new List<object>();
        if (AddBreps != null)
        {
            for (int i = 0; i < workingAdds.Count; i++)
            {
                finalAdds.Add(FinalizeOutput(AddBreps[i], workingAdds[i], 2, i));
                if (Bake && workingAdds[i] != null) BakeGeometry(workingAdds[i], 2, i);
            }
        }

        if (Bake)
        {
            RhinoDocument.Views.Redraw();
        }

        if (_failedCuts > 0)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                _failedCuts + " cut(s) failed (boolean could not be solved) - those members were left uncut at that joint. " +
                "Check for bad/open geometry or try a slightly different Tolerance.");
        }
        Component.Message = "Free-Axis Notch Matrix\n" + _jointCount + " joint(s)" +
                            (_failedCuts > 0 ? ", " + _failedCuts + " failed" : "");

        // 5. Final 1-to-1 Output Assigments
        SlottedBases = finalBases;
        SlottedCutters = finalCutters;
        SlottedAdd = finalAdds;
    }

    // <Custom additional code>

    // ==========================================================================
    // FREE-AXIS ENGINE
    // ==========================================================================

    /// <summary>
    /// Measured description of a member: principal directions sorted by real
    /// extent, plus a plate/beam classification.
    /// </summary>
    private class MemberFrame
    {
        public Vector3d Axis   = Vector3d.XAxis;   // longest extent
        public Vector3d Mid    = Vector3d.YAxis;   // second extent
        public Vector3d Normal = Vector3d.ZAxis;   // thinnest extent
        public double LenAxis, LenMid, LenNormal;
        public bool IsPlate;
    }

    private Dictionary<Brep, MemberFrame> _frameCache = new Dictionary<Brep, MemberFrame>();

    // Per-solve report counters
    private int _jointCount = 0;
    private int _failedCuts = 0;

    /// <summary>
    /// Samples vertices plus edge points so the analysis survives curved and
    /// trimmed geometry, not just boxes.
    /// </summary>
    private List<Point3d> SampleBrepPoints(Brep brep)
    {
        List<Point3d> pts = new List<Point3d>();
        if (brep == null) return pts;

        foreach (BrepVertex v in brep.Vertices) pts.Add(v.Location);

        int divisions = (brep.Edges.Count > 400) ? 2 : 6;
        foreach (BrepEdge e in brep.Edges)
        {
            double[] ts = e.DivideByCount(divisions, true);
            if (ts == null) continue;
            foreach (double t in ts) pts.Add(e.PointAt(t));
        }

        if (pts.Count < 4)
        {
            BoundingBox bb = brep.GetBoundingBox(true);
            if (bb.IsValid) pts.AddRange(bb.GetCorners());
        }
        return pts;
    }

    /// <summary>
    /// Symmetric 3x3 Jacobi eigen decomposition. The columns of the result are
    /// the orthonormal principal directions of the covariance matrix.
    /// </summary>
    private static void JacobiEigen3(double[,] input, out Vector3d[] vecs)
    {
        double[,] m = (double[,])input.Clone();
        double[,] v = new double[3, 3] { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } };

        for (int sweep = 0; sweep < 32; sweep++)
        {
            double off = Math.Abs(m[0, 1]) + Math.Abs(m[0, 2]) + Math.Abs(m[1, 2]);
            if (off < 1e-14) break;

            for (int p = 0; p < 2; p++)
            {
                for (int q = p + 1; q < 3; q++)
                {
                    if (Math.Abs(m[p, q]) < 1e-18) continue;

                    double theta = (m[q, q] - m[p, p]) / (2.0 * m[p, q]);
                    double t;
                    if (theta == 0.0) t = 1.0;
                    else t = Math.Sign(theta) / (Math.Abs(theta) + Math.Sqrt(theta * theta + 1.0));

                    double c = 1.0 / Math.Sqrt(t * t + 1.0);
                    double s = t * c;

                    for (int k = 0; k < 3; k++)
                    {
                        double mkp = m[k, p], mkq = m[k, q];
                        m[k, p] = c * mkp - s * mkq;
                        m[k, q] = s * mkp + c * mkq;
                    }
                    for (int k = 0; k < 3; k++)
                    {
                        double mpk = m[p, k], mqk = m[q, k];
                        m[p, k] = c * mpk - s * mqk;
                        m[q, k] = s * mpk + c * mqk;
                    }
                    for (int k = 0; k < 3; k++)
                    {
                        double vkp = v[k, p], vkq = v[k, q];
                        v[k, p] = c * vkp - s * vkq;
                        v[k, q] = s * vkp + c * vkq;
                    }
                }
            }
        }

        vecs = new Vector3d[3];
        for (int k = 0; k < 3; k++)
        {
            Vector3d e = new Vector3d(v[0, k], v[1, k], v[2, k]);
            if (!e.Unitize()) e = (k == 0) ? Vector3d.XAxis : (k == 1 ? Vector3d.YAxis : Vector3d.ZAxis);
            vecs[k] = e;
        }
    }

    /// <summary>
    /// Principal directions of a point cloud, sorted by physical extent (largest
    /// first). Returns false when the cloud is too small to analyse.
    /// </summary>
    private bool PrincipalAxes(List<Point3d> pts, out Vector3d[] axes, out double[] extents)
    {
        axes = new Vector3d[] { Vector3d.XAxis, Vector3d.YAxis, Vector3d.ZAxis };
        extents = new double[] { 0.0, 0.0, 0.0 };
        if (pts == null || pts.Count < 3) return false;

        double mx = 0, my = 0, mz = 0;
        foreach (Point3d p in pts) { mx += p.X; my += p.Y; mz += p.Z; }
        mx /= pts.Count; my /= pts.Count; mz /= pts.Count;

        double[,] cov = new double[3, 3];
        foreach (Point3d p in pts)
        {
            double dx = p.X - mx, dy = p.Y - my, dz = p.Z - mz;
            cov[0, 0] += dx * dx; cov[0, 1] += dx * dy; cov[0, 2] += dx * dz;
            cov[1, 1] += dy * dy; cov[1, 2] += dy * dz; cov[2, 2] += dz * dz;
        }
        cov[1, 0] = cov[0, 1]; cov[2, 0] = cov[0, 2]; cov[2, 1] = cov[1, 2];

        Vector3d[] raw;
        JacobiEigen3(cov, out raw);

        // Rank by true measured extent rather than by variance.
        double[] rawExt = new double[3];
        for (int k = 0; k < 3; k++)
        {
            double lo = double.MaxValue, hi = double.MinValue;
            foreach (Point3d p in pts)
            {
                double d = raw[k].X * p.X + raw[k].Y * p.Y + raw[k].Z * p.Z;
                if (d < lo) lo = d;
                if (d > hi) hi = d;
            }
            rawExt[k] = hi - lo;
        }

        int[] order = new int[] { 0, 1, 2 };
        for (int a = 0; a < 2; a++)
        {
            for (int b = a + 1; b < 3; b++)
            {
                if (rawExt[order[b]] > rawExt[order[a]])
                {
                    int tmp = order[a]; order[a] = order[b]; order[b] = tmp;
                }
            }
        }

        for (int k = 0; k < 3; k++) { axes[k] = raw[order[k]]; extents[k] = rawExt[order[k]]; }

        // Guarantee a right-handed frame.
        if (Vector3d.CrossProduct(axes[0], axes[1]) * axes[2] < 0) axes[2] = -axes[2];
        return true;
    }

    /// <summary>
    /// Measures a member once and caches it: longitudinal axis, thin direction,
    /// and whether it behaves like a plate or like a beam.
    /// </summary>
    private MemberFrame GetMemberFrame(Brep brep)
    {
        MemberFrame cached;
        if (brep != null && _frameCache.TryGetValue(brep, out cached)) return cached;

        MemberFrame f = new MemberFrame();
        if (brep == null) return f;

        Vector3d[] axes;
        double[] ext;
        if (PrincipalAxes(SampleBrepPoints(brep), out axes, out ext))
        {
            f.Axis = axes[0]; f.Mid = axes[1]; f.Normal = axes[2];
            f.LenAxis = ext[0]; f.LenMid = ext[1]; f.LenNormal = ext[2];

            // Plate-like when the thinnest direction is clearly thinner than the
            // middle one: a sheet, a fin, a waffle rib. Square-ish sections stay beams.
            f.IsPlate = (ext[1] > 1e-9) && (ext[2] <= 0.35 * ext[1]);
        }

        _frameCache[brep] = f;
        return f;
    }

    /// <summary>
    /// Principal direction of the intersection geometry itself: the last measured
    /// fallback when the member analysis comes out degenerate.
    /// </summary>
    private Vector3d GetIntersectionAxis(Brep[] overlaps, Curve[] intCurves)
    {
        List<Point3d> pts = new List<Point3d>();

        if (intCurves != null)
        {
            foreach (Curve c in intCurves)
            {
                if (c == null) continue;
                double[] ts = c.DivideByCount(8, true);
                if (ts != null) foreach (double t in ts) pts.Add(c.PointAt(t));
            }
        }
        if (pts.Count < 3 && overlaps != null)
        {
            foreach (Brep ov in overlaps)
            {
                if (ov == null) continue;
                pts.AddRange(SampleBrepPoints(ov));
            }
        }

        Vector3d[] axes;
        double[] ext;
        if (PrincipalAxes(pts, out axes, out ext)) return axes[0];
        return Vector3d.Unset;
    }

    /// <summary>
    /// Derives the true notch axis for one intersecting pair. Candidates are tried
    /// in order of physical meaning; a cross product of unit vectors has length
    /// sin(angle), so its own length doubles as the degeneracy test.
    /// </summary>
    private Vector3d ComputeNotchAxis(Brep a, Brep b, Brep[] overlaps, Curve[] intCurves)
    {
        MemberFrame fa = GetMemberFrame(a);
        MemberFrame fb = GetMemberFrame(b);

        List<Vector3d> candidates = new List<Vector3d>();

        if (fa.IsPlate && fb.IsPlate)
        {
            // Two sheets: the slot runs along the line their planes share.
            candidates.Add(Vector3d.CrossProduct(fa.Normal, fb.Normal));
            candidates.Add(Vector3d.CrossProduct(fa.Axis, fb.Axis));
        }
        else if (fa.IsPlate)
        {
            // Sheet against a beam: in the sheet's plane, across the beam.
            candidates.Add(Vector3d.CrossProduct(fa.Normal, fb.Axis));
            candidates.Add(Vector3d.CrossProduct(fa.Axis, fb.Axis));
        }
        else if (fb.IsPlate)
        {
            candidates.Add(Vector3d.CrossProduct(fb.Normal, fa.Axis));
            candidates.Add(Vector3d.CrossProduct(fa.Axis, fb.Axis));
        }
        else
        {
            // Two beams: perpendicular to both, the direction one nests into the other.
            candidates.Add(Vector3d.CrossProduct(fa.Axis, fb.Axis));
            candidates.Add(Vector3d.CrossProduct(fa.Normal, fb.Normal));
        }

        foreach (Vector3d cand in candidates)
        {
            Vector3d n = cand;
            if (!n.IsValid) continue;
            if (n.Length < 0.15) continue;      // members within ~8.6 deg of parallel: try the next rule
            if (!n.Unitize()) continue;
            return OrientNotchAxis(n);
        }

        // Every member rule came out degenerate: measure the intersection itself.
        Vector3d fromIntersection = GetIntersectionAxis(overlaps, intCurves);
        if (fromIntersection.IsValid && fromIntersection.Unitize()) return OrientNotchAxis(fromIntersection);

        return Vector3d.ZAxis;
    }

    /// <summary>
    /// Deterministic sign so Base and Cutter always notch on opposite sides.
    /// Biased to +Z, so flat-in-XY assemblies behave exactly as they did before.
    /// </summary>
    private Vector3d OrientNotchAxis(Vector3d n)
    {
        double dz = n * Vector3d.ZAxis;
        if (Math.Abs(dz) > 1e-6) { if (dz < 0) n.Reverse(); return n; }

        double dy = n * Vector3d.YAxis;
        if (Math.Abs(dy) > 1e-6) { if (dy < 0) n.Reverse(); return n; }

        if ((n * Vector3d.XAxis) < 0) n.Reverse();
        return n;
    }

    /// <summary>
    /// Builds the local cutting frame for one member: plane Z is the shared notch
    /// axis, plane X follows that member's own longitudinal direction projected
    /// into the cut plane, so the slot box hugs its section instead of the world.
    /// </summary>
    private Plane GetLocalCutPlane(Brep brep, Point3d center, Vector3d notchAxis)
    {
        Vector3d xDir = Vector3d.Unset;
        double bestScore = -1.0;

        if (brep != null)
        {
            foreach (BrepEdge edge in brep.Edges)
            {
                if (!edge.IsLinear()) continue;

                Vector3d tangent = edge.TangentAtStart;
                tangent -= (tangent * notchAxis) * notchAxis;   // flatten into the cut plane

                double score = tangent.Length * edge.GetLength();
                if (score > bestScore && score > 1e-6)
                {
                    bestScore = score;
                    xDir = tangent;
                }
            }
        }

        if (bestScore <= 0 || !xDir.Unitize())
        {
            MemberFrame f = GetMemberFrame(brep);

            Vector3d ax = f.Axis;
            ax -= (ax * notchAxis) * notchAxis;
            if (!ax.Unitize())
            {
                ax = f.Mid;
                ax -= (ax * notchAxis) * notchAxis;
                if (!ax.Unitize())
                {
                    ax = new Vector3d();
                    ax.PerpendicularTo(notchAxis);
                    if (!ax.Unitize()) ax = Vector3d.XAxis;
                }
            }
            xDir = ax;
        }

        Vector3d yDir = Vector3d.CrossProduct(notchAxis, xDir);
        if (!yDir.Unitize())
        {
            yDir = new Vector3d();
            yDir.PerpendicularTo(xDir);
            yDir.Unitize();
        }

        // Plane Z = xDir x yDir = notchAxis, since both are perpendicular to it.
        return new Plane(center, xDir, yDir);
    }

    /// <summary>
    /// How far the slot box must run along the notch axis to break clean out of
    /// the far face of this member, measured in the member's own cutting frame.
    /// </summary>
    private double BreakoutDistance(Brep brep, Plane plane)
    {
        if (brep == null) return 10.0;
        BoundingBox bb = brep.GetBoundingBox(plane);
        if (!bb.IsValid) return 10.0;
        double reach = Math.Max(Math.Abs(bb.Max.Z), Math.Abs(bb.Min.Z));
        return Math.Max(reach * 2.0 + 10.0, 10.0);
    }

    // ==========================================================================
    // JOINT ENGINE
    // ==========================================================================

    /// <summary>
    /// Where two UNCUT members touch: the solid overlap when there is one,
    /// otherwise the intersection curves.
    /// </summary>
    private class Contact
    {
        public Brep[] Overlaps;
        public Curve[] Curves;
        public Point3d WorldCenter = Point3d.Unset;
    }

    private static bool BoxesTouch(BoundingBox a, BoundingBox b, double pad)
    {
        return a.Min.X <= b.Max.X + pad && b.Min.X <= a.Max.X + pad
            && a.Min.Y <= b.Max.Y + pad && b.Min.Y <= a.Max.Y + pad
            && a.Min.Z <= b.Max.Z + pad && b.Min.Z <= a.Max.Z + pad;
    }

    private Contact FindContact(Brep a, Brep b, double tol)
    {
        if (a == null || b == null) return null;

        // Cheap reject before any boolean work.
        if (!BoxesTouch(a.GetBoundingBox(false), b.GetBoundingBox(false), tol)) return null;

        Contact c = new Contact();
        BoundingBox world = BoundingBox.Empty;

        Brep[] overlaps = Brep.CreateBooleanIntersection(new Brep[] { a }, new Brep[] { b }, tol);
        if (overlaps != null && overlaps.Length > 0 && overlaps[0].IsSolid)
        {
            c.Overlaps = overlaps;
            foreach (Brep ov in overlaps) { if (ov != null) world.Union(ov.GetBoundingBox(true)); }
        }
        else
        {
            Curve[] curves;
            if (Rhino.Geometry.Intersect.Intersection.BrepBrep(a, b, tol, out curves, out _) && curves != null && curves.Length > 0)
            {
                c.Curves = curves;
                foreach (Curve crv in curves) { if (crv != null) world.Union(crv.GetBoundingBox(true)); }
            }
        }

        if (!world.IsValid) return null;
        c.WorldCenter = world.Center;
        return c;
    }

    /// <summary>
    /// Extent of a contact expressed in a cutting plane's coordinates.
    /// </summary>
    private BoundingBox LocalBox(Contact c, Plane plane)
    {
        BoundingBox bb = BoundingBox.Empty;
        if (c == null) return bb;

        if (c.Overlaps != null)
        {
            foreach (Brep ov in c.Overlaps) { if (ov != null) bb.Union(ov.GetBoundingBox(plane)); }
        }
        else if (c.Curves != null)
        {
            foreach (Curve crv in c.Curves) { if (crv != null) bb.Union(crv.GetBoundingBox(plane)); }
        }
        return bb;
    }

    /// <summary>
    /// The middle of the intersection line, measured ALONG the joint axis. A world
    /// bounding-box centre only lands there when the axis happens to be world Z.
    /// </summary>
    private Point3d CenterOnAxis(Contact c, Vector3d axis)
    {
        Point3d center = c.WorldCenter;
        Plane probe = new Plane(center, axis);   // plane Z = joint axis
        if (!probe.IsValid) return center;

        BoundingBox bb = LocalBox(c, probe);
        if (!bb.IsValid) return center;

        return center + axis * ((bb.Min.Z + bb.Max.Z) * 0.5);
    }

    /// <summary>
    /// True when an AddBrep actually runs through the Base x Cutter joint centre,
    /// i.e. all three members share that intersection line.
    /// </summary>
    private bool PassesThrough(Brep add, Point3d point, double reach, double tol)
    {
        if (add == null || !point.IsValid) return false;
        if (add.IsSolid && add.IsPointInside(point, tol, false)) return true;

        Point3d cp = add.ClosestPoint(point);
        if (!cp.IsValid) return false;

        return cp.DistanceTo(point) <= reach + 0.5 * GetMemberFrame(add).LenNormal;
    }

    private void Widen(ref BoundingBox bb, double halfTol, double tol)
    {
        bb.Inflate(halfTol, halfTol, 0);
        if (bb.Diagonal.X < tol) bb.Inflate(halfTol, 0, 0);
        if (bb.Diagonal.Y < tol) bb.Inflate(0, halfTol, 0);
    }

    private Brep SlotBox(Plane plane, BoundingBox footprint, double zFrom, double zTo)
    {
        Interval x = new Interval(footprint.Min.X, footprint.Max.X);
        Interval y = new Interval(footprint.Min.Y, footprint.Max.Y);
        Interval z = new Interval(Math.Min(zFrom, zTo), Math.Max(zFrom, zTo));
        if (z.Length < 1e-9) return null;
        return Brep.CreateFromBox(new Box(plane, x, y, z));
    }

    private void Subtract(List<Brep> members, int index, List<Brep> boxes, double tol)
    {
        if (members[index] == null) return;
        boxes.RemoveAll(b => b == null);
        if (boxes.Count == 0) return;

        Brep[] result = RobustDifference(members[index], boxes, tol);
        if (result != null && result.Length > 0) members[index] = JoinOrKeepLargest(result);
        else _failedCuts++;
    }

    /// <summary>
    /// One-sided slot. side = -1 : opens from the negative side, member keeps z > +half (Base).
    ///                side = +1 : opens from the positive side, member keeps z < -half (Cutter).
    /// </summary>
    private void CutSide(List<Brep> members, int index, Brep pristine, Plane plane, BoundingBox footprint, int side, double half, double halfTol, double tol)
    {
        if (members[index] == null || !footprint.IsValid) return;
        Widen(ref footprint, halfTol, tol);

        double breakout = BreakoutDistance(pristine, plane);
        Brep box = (side < 0)
            ? SlotBox(plane, footprint, footprint.Min.Z - breakout, half)
            : SlotBox(plane, footprint, -half, footprint.Max.Z + breakout);

        Subtract(members, index, new List<Brep> { box }, tol);
    }

    /// <summary>
    /// Two-sided H-notch: member keeps only the web -webHalf < z < +webHalf.
    /// </summary>
    private void CutH(List<Brep> members, int index, Brep pristine, Plane plane, BoundingBox footprint, double webHalf, double halfTol, double tol)
    {
        if (members[index] == null || !footprint.IsValid) return;
        Widen(ref footprint, halfTol, tol);

        double breakout = BreakoutDistance(pristine, plane);
        List<Brep> boxes = new List<Brep>
        {
            SlotBox(plane, footprint, webHalf, footprint.Max.Z + breakout),
            SlotBox(plane, footprint, footprint.Min.Z - breakout, -webHalf)
        };

        Subtract(members, index, boxes, tol);
    }

    /// <summary>
    /// Every joint is measured on uncut geometry and cut in one shared frame.
    ///
    ///   Base x Cutter joint, no AddBrep : Base keeps z > +Gap/2, Cutter keeps z < -Gap/2.
    ///   Base x Cutter joint + AddBrep   : Base keeps z > +(AddGap/2 + Gap),
    ///                                     AddBrep keeps the web |z| < AddGap/2,
    ///                                     Cutter keeps z < -(AddGap/2 + Gap).
    ///   AddBrep x single member         : the same H-notch, framed on that pair alone.
    /// </summary>
    private void ProcessJoints(
        List<Brep> bases, List<Brep> cutters, List<Brep> adds,
        List<Brep> pBases, List<Brep> pCutters, List<Brep> pAdds,
        double Gap, double AddGap, double Tolerance, double tol)
    {
        double cutTol = Math.Max(Tolerance, tol * 10.0);
        double halfTol = cutTol / 2.0;
        double addVoid = AddGap / 2.0 + Gap;

        int nA = pAdds.Count, nB = pBases.Count, nC = pCutters.Count;

        // Every AddBrep contact, measured once on uncut geometry.
        Contact[,] addBase = new Contact[nA, nB];
        Contact[,] addCutter = new Contact[nA, nC];
        bool[,] addBaseDone = new bool[nA, nB];
        bool[,] addCutterDone = new bool[nA, nC];

        for (int a = 0; a < nA; a++)
        {
            if (pAdds[a] == null) continue;
            for (int i = 0; i < nB; i++) addBase[a, i] = FindContact(pAdds[a], pBases[i], tol);
            for (int j = 0; j < nC; j++) addCutter[a, j] = FindContact(pAdds[a], pCutters[j], tol);
        }

        int crowdedJoints = 0;

        // 1. Base x Cutter joints, with any AddBrep that shares their intersection line.
        for (int i = 0; i < nB; i++)
        {
            if (pBases[i] == null) continue;

            for (int j = 0; j < nC; j++)
            {
                if (pCutters[j] == null) continue;

                Contact bc = FindContact(pBases[i], pCutters[j], tol);
                if (bc == null) continue;
                _jointCount++;

                Vector3d axis = ComputeNotchAxis(pBases[i], pCutters[j], bc.Overlaps, bc.Curves);
                Point3d center = CenterOnAxis(bc, axis);

                List<int> through = new List<int>();
                for (int a = 0; a < nA; a++)
                {
                    if (addBase[a, i] == null || addCutter[a, j] == null) continue;
                    if (PassesThrough(pAdds[a], center, cutTol, tol)) through.Add(a);
                }
                if (through.Count > 1) crowdedJoints++;

                double half = (through.Count > 0) ? addVoid : Gap / 2.0;

                // Base: one slot covering both the Cutter and the AddBrep, keeps the positive side.
                Plane bp = GetLocalCutPlane(pBases[i], center, axis);
                BoundingBox bFoot = LocalBox(bc, bp);
                foreach (int a in through) bFoot.Union(LocalBox(addBase[a, i], bp));
                CutSide(bases, i, pBases[i], bp, bFoot, -1, half, halfTol, tol);

                // Cutter: one slot covering both the Base and the AddBrep, keeps the negative side.
                Plane cp = GetLocalCutPlane(pCutters[j], center, axis);
                BoundingBox cFoot = LocalBox(bc, cp);
                foreach (int a in through) cFoot.Union(LocalBox(addCutter[a, j], cp));
                CutSide(cutters, j, pCutters[j], cp, cFoot, +1, half, halfTol, tol);

                // AddBrep: centred web on the same axis and the same centre.
                foreach (int a in through)
                {
                    Plane ap = GetLocalCutPlane(pAdds[a], center, axis);
                    BoundingBox aFoot = LocalBox(addBase[a, i], ap);
                    aFoot.Union(LocalBox(addCutter[a, j], ap));
                    CutH(adds, a, pAdds[a], ap, aFoot, AddGap / 2.0, halfTol, tol);

                    addBaseDone[a, i] = true;
                    addCutterDone[a, j] = true;
                }
            }
        }

        // 2. AddBreps meeting a single Base or Cutter away from any shared joint.
        for (int a = 0; a < nA; a++)
        {
            if (pAdds[a] == null) continue;

            for (int i = 0; i < nB; i++)
            {
                if (addBase[a, i] == null || addBaseDone[a, i]) continue;
                CutAddPair(adds, a, pAdds[a], bases, i, pBases[i], addBase[a, i], -1, AddGap, addVoid, halfTol, tol);
            }

            for (int j = 0; j < nC; j++)
            {
                if (addCutter[a, j] == null || addCutterDone[a, j]) continue;
                CutAddPair(adds, a, pAdds[a], cutters, j, pCutters[j], addCutter[a, j], +1, AddGap, addVoid, halfTol, tol);
            }
        }

        if (crowdedJoints > 0)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                crowdedJoints + " joint(s) have more than one AddBrep on the same intersection line. They all share the centre web and will clash.");
        }
    }

    private void CutAddPair(List<Brep> adds, int a, Brep pAdd, List<Brep> members, int m, Brep pMember, Contact c, int side, double AddGap, double addVoid, double halfTol, double tol)
    {
        _jointCount++;
        Vector3d axis = ComputeNotchAxis(pAdd, pMember, c.Overlaps, c.Curves);
        Point3d center = CenterOnAxis(c, axis);

        Plane ap = GetLocalCutPlane(pAdd, center, axis);
        CutH(adds, a, pAdd, ap, LocalBox(c, ap), AddGap / 2.0, halfTol, tol);

        Plane mp = GetLocalCutPlane(pMember, center, axis);
        CutSide(members, m, pMember, mp, LocalBox(c, mp), side, addVoid, halfTol, tol);
    }

    // ==========================================================================
    // BAKE / ATTRIBUTE ENGINE
    // ==========================================================================

    /// <summary>
    /// Executes the active document bake. If the input geometry was referenced from Rhino,
    /// it replaces the original geometry in place. If it was virtual, it adds it as a new object.
    /// </summary>
    private void BakeGeometry(Brep geom, int paramIndex, int listIndex)
    {
        if (geom == null || !geom.IsValid) return;

        Guid originalId = Guid.Empty;
        Rhino.DocObjects.ObjectAttributes bakeAttributes = new Rhino.DocObjects.ObjectAttributes();

        IGH_Goo goo = GetInputGoo(paramIndex, listIndex);
        if (goo != null)
        {
            if (goo is Grasshopper.Kernel.Types.IGH_GeometricGoo geomGoo && geomGoo.IsReferencedGeometry)
            {
                originalId = geomGoo.ReferenceID;
            }

            Type gooType = goo.GetType();

            if (originalId == Guid.Empty)
            {
                var idProp = gooType.GetProperty("Id") ?? gooType.GetProperty("ReferenceID");
                if (idProp != null)
                {
                    object idVal = idProp.GetValue(goo);
                    if (idVal is Guid guid) originalId = guid;
                }
            }

            var attrProp = gooType.GetProperty("Attributes") ?? gooType.GetProperty("ObjectAttributes");
            if (attrProp != null)
            {
                object attrs = attrProp.GetValue(goo);
                if (attrs is Rhino.DocObjects.ObjectAttributes rhAttrs)
                {
                    bakeAttributes = rhAttrs.Duplicate();
                }
            }
            else if (originalId != Guid.Empty)
            {
                var rhObj = RhinoDocument.Objects.FindId(originalId);
                if (rhObj != null) bakeAttributes = rhObj.Attributes.Duplicate();
            }
        }

        if (originalId != Guid.Empty && RhinoDocument.Objects.FindId(originalId) != null)
        {
            RhinoDocument.Objects.Replace(originalId, geom);

            var replacedObj = RhinoDocument.Objects.FindId(originalId);
            if (replacedObj != null && geom.UserDictionary != null)
            {
                replacedObj.Attributes.UserDictionary.ReplaceContentsWith(CleanUserData(geom));
                replacedObj.CommitChanges();
            }
        }
        else
        {
            if (bakeAttributes.UserDictionary != null && geom.UserDictionary != null)
            {
                bakeAttributes.UserDictionary.ReplaceContentsWith(CleanUserData(geom));
            }
            bakeAttributes.ObjectId = Guid.Empty;
            RhinoDocument.Objects.AddBrep(geom, bakeAttributes);
        }
    }

    /// <summary>
    /// Safely pulls raw volatile data directly from the component parameters, bypassing internal casting.
    /// </summary>
    private IGH_Goo GetInputGoo(int paramIndex, int listIndex)
    {
        try
        {
            IGH_Param param = FindInput(paramIndex);
            if (param == null || param.VolatileData.DataCount == 0) return null;

            // AllData(false): keep null items so listIndex stays aligned with the input list
            int counter = 0;
            foreach (IGH_Goo goo in param.VolatileData.AllData(false))
            {
                if (counter == listIndex) return goo;
                counter++;
            }
        }
        catch { }
        return null;
    }

    /// <summary>
    /// Deep-extracts metadata from Rhino 8 Native Model Objects and Reference IDs.
    /// Transfers Name, Color, Layer, and custom fields to the newly formed Brep.
    /// </summary>
    private void TransferDeepMetadata(Brep sourceGeom, Brep targetGeom, int paramIndex, int listIndex)
    {
        if (sourceGeom == null || targetGeom == null) return;

        targetGeom.UserDictionary.ReplaceContentsWith(sourceGeom.UserDictionary);

        IGH_Goo goo = GetInputGoo(paramIndex, listIndex);
        if (goo == null) return;

        Type gooType = goo.GetType();
        var attrProp = gooType.GetProperty("Attributes") ?? gooType.GetProperty("ObjectAttributes");
        if (attrProp != null)
        {
            object attrs = attrProp.GetValue(goo);
            if (attrs is Rhino.DocObjects.ObjectAttributes rhAttrs)
            {
                // Object user data first - setting it afterwards used to wipe Name/Color/Layer
                var rhDict = rhAttrs.UserDictionary;
                if (rhDict != null && rhDict.Count > 0)
                    targetGeom.UserDictionary.ReplaceContentsWith(rhDict);

                if (!string.IsNullOrEmpty(rhAttrs.Name))
                    targetGeom.UserDictionary.Set("Name", rhAttrs.Name);

                targetGeom.UserDictionary.Set("Color", rhAttrs.ObjectColor.ToArgb());
                targetGeom.UserDictionary.Set("LayerIndex", rhAttrs.LayerIndex);
            }
        }

        if (goo is Grasshopper.Kernel.Types.IGH_GeometricGoo geomGoo)
        {
            if (geomGoo.IsReferencedGeometry && geomGoo.ReferenceID != Guid.Empty)
            {
                var rhObj = RhinoDocument.Objects.FindId(geomGoo.ReferenceID);
                if (rhObj != null)
                {
                    if (!string.IsNullOrEmpty(rhObj.Attributes.Name))
                        targetGeom.UserDictionary.Set("Name", rhObj.Attributes.Name);

                    targetGeom.UserDictionary.Set("Color", rhObj.Attributes.ObjectColor.ToArgb());
                    targetGeom.UserDictionary.Set("LayerIndex", rhObj.Attributes.LayerIndex);
                }
            }
        }
    }

    /// <summary>
    /// Executes the final output assignment, wrapping the geometry back into a Rhino 8
    /// Model Object when possible to preserve visual characteristics on the canvas.
    /// </summary>
    private object FinalizeOutput(Brep originalGeom, Brep notchedGeom, int paramIndex, int listIndex)
    {
        if (notchedGeom == null) return null;

        TransferDeepMetadata(originalGeom, notchedGeom, paramIndex, listIndex);

        IGH_Goo goo = GetInputGoo(paramIndex, listIndex);
        if (goo != null)
        {
            Type gooType = goo.GetType();
            if (gooType.Name.Contains("ModelObject") || gooType.Name.Contains("GH_RhinoAny"))
            {
                try
                {
                    IGH_Goo duplicate = goo.Duplicate();

                    var geomProp = gooType.GetProperty("Geometry") ?? gooType.GetProperty("Value");
                    if (geomProp != null && geomProp.CanWrite)
                    {
                        geomProp.SetValue(duplicate, notchedGeom);
                        return duplicate;
                    }

                    var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                    var field = gooType.GetField("<Geometry>k__BackingField", flags)
                             ?? gooType.GetField("m_value", flags);
                    if (field != null)
                    {
                        field.SetValue(duplicate, notchedGeom);
                        return duplicate;
                    }
                }
                catch { }
            }
        }

        return notchedGeom;
    }

    // ==========================================================================
    // BOOLEAN ENGINE
    // ==========================================================================

    /// <summary>
    /// Bulletproof Boolean Difference that ensures the void volume is permanently removed.
    /// Shatters open surfaces with 3D intersection curves for 100% reliability.
    /// </summary>
    private Brep[] RobustDifference(Brep target, List<Brep> cutters, double tol)
    {
        if (target == null || cutters == null || cutters.Count == 0) return new Brep[] { target };

        Brep[] diff = Brep.CreateBooleanDifference(new Brep[] { target }, cutters, tol);
        if (diff != null && diff.Length > 0) return diff;

        // Failsafe 1: If boolean fails on a closed Solid, micro-scale the cutter to break coincident faces
        if (target.IsSolid)
        {
            List<Brep> scaledCutters = new List<Brep>();
            foreach (Brep c in cutters)
            {
                if (c == null) continue;
                Brep scaled = c.DuplicateBrep();
                BoundingBox cbb = scaled.GetBoundingBox(true);
                // Grow by ~2 x tolerance only. A fixed 1.001 grew long breakout boxes by
                // millimetres and shifted the slot seat (Gap) by up to half of that.
                double size = Math.Max(cbb.Diagonal.MaximumCoordinate, 1e-9);
                double factor = 1.0 + (4.0 * tol) / size;
                scaled.Transform(Transform.Scale(cbb.Center, factor));
                scaledCutters.Add(scaled);
            }
            diff = Brep.CreateBooleanDifference(new Brep[] { target }, scaledCutters, tol);
            if (diff != null && diff.Length > 0) return diff;
        }

        // Failsafe 2: Curve-Shatter strategy for open zero-thickness surfaces
        if (!target.IsSolid)
        {
            Brep current = target;
            foreach (Brep cutter in cutters)
            {
                Brep[] pieces = current.Split(cutter, tol);

                // If standard Split failed, fallback to extracting strict intersection curves
                if (pieces == null || pieces.Length <= 1)
                {
                    Curve[] intCrvs;
                    if (Rhino.Geometry.Intersect.Intersection.BrepBrep(current, cutter, tol, out intCrvs, out _))
                    {
                        if (intCrvs != null && intCrvs.Length > 0)
                        {
                            Brep[] crvPieces = current.Split(intCrvs, tol);
                            if (crvPieces != null && crvPieces.Length > 1) pieces = crvPieces;
                        }
                    }
                }

                if (pieces != null && pieces.Length > 1)
                {
                    List<Brep> outsidePieces = new List<Brep>();
                    foreach (Brep p in pieces)
                    {
                        Point3d testPt;
                        AreaMassProperties amp = AreaMassProperties.Compute(p);

                        // Guarantee the test point sits physically on the shattered face
                        if (amp != null) p.ClosestPoint(amp.Centroid, out testPt, out _, out _, out _, 0.0, out _);
                        else p.ClosestPoint(p.GetBoundingBox(true).Center, out testPt, out _, out _, out _, 0.0, out _);

                        // Discard the piece if its center of mass is trapped inside the cutting box
                        if (!cutter.IsPointInside(testPt, tol, false))
                        {
                            outsidePieces.Add(p);
                        }
                    }

                    if (outsidePieces.Count > 0)
                    {
                        Brep[] joined = Brep.JoinBreps(outsidePieces, tol);
                        current = (joined != null && joined.Length > 0) ? joined[0] : outsidePieces[0];
                    }
                    else
                    {
                        current = null;
                        break;
                    }
                }
            }
            return current != null ? new Brep[] { current } : null;
        }

        return null;
    }

    /// <summary>
    /// Prevents data trees from expanding by returning the most significant resulting geometry.
    /// </summary>
    private Brep JoinOrKeepLargest(Brep[] brepArray)
    {
        if (brepArray == null || brepArray.Length == 0) return null;
        if (brepArray.Length == 1) return brepArray[0];

        Brep largest = brepArray[0];
        double maxSize = -1;

        foreach (Brep b in brepArray)
        {
            if (b == null) continue;
            double size = b.IsSolid ? b.GetVolume() : b.GetArea();
            if (size > maxSize)
            {
                maxSize = size;
                largest = b;
            }
        }
        return largest;
    }

    // ==========================================================================
    // METADATA / HELPERS
    // ==========================================================================

    private bool _metaSet = false;
    private static readonly string[] InputNames = { "BaseBreps", "CutterBreps", "AddBreps" };
    private static readonly string[] HelperKeys = { "Name", "Color", "LayerIndex" };

    /// <summary>Input pin by script name (falls back to its index).</summary>
    private IGH_Param FindInput(int paramIndex)
    {
        var inputs = Component.Params.Input;
        if (paramIndex >= 0 && paramIndex < InputNames.Length)
        {
            foreach (IGH_Param p in inputs)
                if (string.Equals(p.Name, InputNames[paramIndex], StringComparison.OrdinalIgnoreCase)) return p;
        }
        return (paramIndex >= 0 && paramIndex < inputs.Count) ? inputs[paramIndex] : null;
    }

    /// <summary>
    /// Copy of the Brep's user data without the Name/Color/LayerIndex helper keys
    /// (those are for the Grasshopper outputs, not for the Rhino object's user text).
    /// </summary>
    private Rhino.Collections.ArchivableDictionary CleanUserData(Brep geom)
    {
        var d = geom.UserDictionary.Clone();
        foreach (string k in HelperKeys) d.Remove(k);
        return d;
    }

    private void SetMetadata()
    {
        if (_metaSet) return;
        _metaSet = true;

        Component.Name = "Hybrid Interlock Notcher";
        Component.NickName = "Hybrid Interlock";
        Component.Description =
            "Cuts 3D interlocking (egg-crate / lap) notches between Base, Cutter and optional Add Breps " +
            "in any orientation. Works on solids and open surfaces, measures every joint on the uncut input, " +
            "keeps names/colours and can replace the originals in Rhino. Rajeev Pulari, v2.1.";

        var ins = Component.Params.Input;
        SetTip(ins, "BaseBreps", "Primary members (list). Keep the POSITIVE side of each joint axis - the slot opens from below.");
        SetTip(ins, "CutterBreps", "Crossing members (list). Keep the NEGATIVE side of each joint axis - the slot opens from above.");
        SetTip(ins, "AddBreps", "Optional third members (list) that get a two-sided H-notch, web centred on the joint.");
        SetTip(ins, "Gap", "Clearance along the joint axis between mating parts. 0 = flush.");
        SetTip(ins, "AddGap", "Web height left on AddBreps, centred on the joint. 0 = AddBreps cut clean through.");
        SetTip(ins, "Tolerance", "Extra slot width (in-plane clearance). 0 = 10 x model tolerance.");
        SetTip(ins, "Bake", "Connect a Button. On press: replaces referenced inputs in Rhino, or adds the result for internal geometry.");

        var outs = Component.Params.Output;
        SetTip(outs, "SlottedBases", "Notched BaseBreps, 1:1 with the input (name/colour kept).");
        SetTip(outs, "SlottedCutters", "Notched CutterBreps, 1:1 with the input (name/colour kept).");
        SetTip(outs, "SlottedAdd", "Notched AddBreps, 1:1 with the input (name/colour kept).");
    }

    // Match pins by Name (the script variable), fall back to NickName.
    // Only NickName/Description are changed - never Name.
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