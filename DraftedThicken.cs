#region Visual Meta Data
// Inputs:
//   Surface    : Rhino.Geometry.Brep (Item Access, Type Hint: Brep)   - Surface, polysurface or closed brep
//   Height     : System.Double (Item Access, Type Hint: double)       - Thickness along local surface normal
//   Angle      : System.Double (Item Access, Type Hint: double)       - Draft angle in degrees (0 = walls normal to surface)
//   FlipSide   : System.Boolean (Item Access, Type Hint: bool)        - false = draft inward, true = draft outward
//   MiterLimit : System.Double (Item Access, Type Hint: double)       - Max corner overshoot x draft offset (0 = off, try 2)
//   AutoFit    : System.Boolean (Item Access, Type Hint: bool)        - Reduce draft automatically if the top folds over
//   MergeFaces : System.Boolean (Item Access, Type Hint: bool)        - Merge coplanar faces in the result
//   FastZero   : System.Boolean (Item Access, Type Hint: bool)        - Use Rhino offset when Angle = 0 (open breps)
//   WallsOnly  : System.Boolean (Item Access, Type Hint: bool)        - Output only the side walls (no base / top cap)
//
// Outputs:
//   Solid      : System.Object (Drafted solid, hollow solid for closed input, or walls only)
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using Rhino;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using Grasshopper.Kernel;

public class Script_Instance : GH_ScriptInstance
{
    // Corner point shared by edges; V = displacement of that corner to the top
    private class Node
    {
        public Point3d P;
        public HashSet<int> Faces = new HashSet<int>();
        public List<Vector3d[]> Walls = new List<Vector3d[]>(); // { edge tangent, wall ruling }
        public Vector3d V;
    }

    private class EdgeInfo
    {
        public Curve Seg;
        public int[] Faces;
        public bool Naked;
        public int Sign;   // +1 / -1 : which side of n x t is inside the face
        public Node Start;
        public Node End;
        public Curve Top;  // top edge of the wall (naked edges only)
    }

    private void RunScript(
        Brep Surface,
        double Height,
        double Angle,
        bool FlipSide,
        double MiterLimit,
        bool AutoFit,
        bool MergeFaces,
        bool FastZero,
        bool WallsOnly,
        ref object Solid)
    {
        // Metadata + pin tooltips (name/tooltips once, message every solve)
        if (Component != null)
        {
            if (Component.Name != "Drafted Thicken")
            {
                Component.Name = "Drafted Thicken";
                Component.NickName = "DraftThick";
                Component.Description = "Thickens a surface, polysurface or closed brep along local normals, " +
                                        "with a draft angle on the open edges (inward / outward via FlipSide).";
                var pi = Component.Params.Input;
                SetTip(pi, 0, "Surface", "Surface, polysurface or closed brep to thicken.");
                SetTip(pi, 1, "Height", "Thickness along the local surface normal (negative = other side).");
                SetTip(pi, 2, "Angle", "Draft angle in degrees on the open edges (0 = walls normal to surface).");
                SetTip(pi, 3, "FlipSide", "False = draft inward, True = draft outward.");
                SetTip(pi, 4, "MiterLimit", "Max corner overshoot x draft offset (0 = off, try 2).");
                SetTip(pi, 5, "AutoFit", "Reduce the draft automatically if the top folds over.");
                SetTip(pi, 6, "MergeFaces", "Merge coplanar faces in the result.");
                SetTip(pi, 7, "FastZero", "Use Rhino's own offset when Angle = 0 (open breps).");
                SetTip(pi, 8, "WallsOnly", "Output only the side walls (no base / top cap).");
                SetTip(Component.Params.Output, 0, "Solid", "Drafted solid, hollow solid for closed input, or walls only.");
            }
            Component.Message = WallsOnly ? "Walls Only" : "Draft Offset Solid";
        }

        Solid = null;
        try
        {
            Solid = Run(Surface, Height, Angle, FlipSide, MiterLimit, AutoFit, MergeFaces, FastZero, WallsOnly);
        }
        catch (Exception ex)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Unexpected error: " + ex.Message);
        }
    }

    private void SetTip(IList<IGH_Param> ps, int i, string name, string tip)
    {
        if (ps == null || i < 0 || i >= ps.Count) return;
        ps[i].NickName = name;
        ps[i].Description = tip;
    }

    private object Run(Brep input, double Height, double Angle, bool FlipSide,
        double MiterLimit, bool AutoFit, bool MergeFaces, bool FastZero, bool WallsOnly)
    {
        // ---------- input checks ----------
        if (input == null || !input.IsValid)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Input brep is missing or invalid.");
            return null;
        }
        if (Math.Abs(Height) < RhinoMath.ZeroTolerance)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Height is zero.");
            return null;
        }

        RhinoDoc doc = RhinoDoc.ActiveDoc;
        double tol = (doc != null) ? doc.ModelAbsoluteTolerance : 0.001;
        double angTol = (doc != null) ? doc.ModelAngleToleranceRadians : RhinoMath.ToRadians(1.0);
        double kinkAngle = Math.Max(angTol, RhinoMath.ToRadians(3.0));

        Brep brep = input.DuplicateBrep();

        // ---------- closed brep: hollow offset solid ----------
        if (brep.IsSolid)
        {
            if (WallsOnly)
            {
                Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    "Closed brep has no open edges, so there are no walls to output.");
                return null;
            }
            if (Math.Abs(Angle) > 1e-9)
                Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                    "Closed brep: Angle and FlipSide are ignored (no open edges).");

            return BuildClosed(brep, Height, tol, kinkAngle, MergeFaces);
        }

        // ---------- option: Rhino offset for zero draft ----------
        if (FastZero && !WallsOnly && Math.Abs(Angle) < 1e-9)
        {
            Brep fast = RhinoOffset(brep, Height, tol);
            if (fast != null)
            {
                Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Angle = 0: used Rhino offset.");
                return Finish(fast, MergeFaces, tol);
            }
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Rhino offset failed; using script method.");
        }

        // 1. Draft setup
        //    Angle > 0, FlipSide = false -> top edge moves inward along the surface
        //    Angle > 0, FlipSide = true  -> top edge moves outward
        double draftDeg = Math.Max(-89.0, Math.Min(89.0, Angle));
        double draft = Math.Abs(Height) * Math.Tan(RhinoMath.ToRadians(Math.Abs(draftDeg)));
        bool shrink = (draftDeg >= 0) != FlipSide;
        double side = shrink ? draft : -draft; // positive = toward material

        if (draft > tol)
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                "Draft: " + (shrink ? "INWARD" : "OUTWARD") + " (FlipSide=" + FlipSide + "), offset " + draft.ToString("0.###"));

        // 2-4. Corners, displacement fields, wall top edges
        List<Node> nodes;
        List<EdgeInfo> edges;
        FaceField[] fields;
        if (!Prepare(brep, Height, side, tol, kinkAngle, MiterLimit, out nodes, out edges, out fields))
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No usable edges found on input brep.");
            return null;
        }

        // ---------- option: fold-over check / auto fit ----------
        int folds = CountFolds(edges, tol);
        if (folds > 0)
        {
            if (AutoFit && draft > tol)
            {
                double lo = 0.0;
                double hi = draft;
                for (int it = 0; it < 12; it++)
                {
                    double mid = 0.5 * (lo + hi);
                    List<Node> tn;
                    List<EdgeInfo> te;
                    FaceField[] tf;
                    bool ok = Prepare(brep, Height, shrink ? mid : -mid, tol, kinkAngle, MiterLimit, out tn, out te, out tf)
                              && CountFolds(te, tol) == 0;
                    if (ok) lo = mid;
                    else hi = mid;
                }

                side = shrink ? lo : -lo;
                Prepare(brep, Height, side, tol, kinkAngle, MiterLimit, out nodes, out edges, out fields);

                double fitAngle = RhinoMath.ToDegrees(Math.Atan(lo / Math.Abs(Height)));
                Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    "Top folded over at " + Math.Abs(draftDeg).ToString("0.##") + "°. AutoFit reduced draft to " +
                    fitAngle.ToString("0.##") + "° (offset " + lo.ToString("0.###") + ").");

                if (CountFolds(edges, tol) > 0)
                    Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Top still folds even with minimal draft; check Height.");
            }
            else
            {
                Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    "Top edges fold over or cross (" + folds + " issue(s)). Reduce Angle/Height, set MiterLimit, or enable AutoFit.");
            }
        }

        // 5. Side walls on naked edges
        List<Brep> walls = new List<Brep>();
        foreach (EdgeInfo info in edges)
        {
            if (!info.Naked) continue;

            if (info.Top == null)
            {
                Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Wall edge morph failed.");
                continue;
            }

            NurbsSurface rs = NurbsSurface.CreateRuledSurface(info.Seg, info.Top);
            if (rs == null)
            {
                Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "A wall face failed.");
                continue;
            }
            walls.Add(rs.ToBrep());
        }

        // ---------- option: walls only (no base / top cap) ----------
        if (WallsOnly)
        {
            if (walls.Count == 0)
            {
                Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No walls were created.");
                return null;
            }

            Brep[] wallJoin = Rhino.Geometry.Brep.JoinBreps(walls, tol);
            if (wallJoin == null || wallJoin.Length == 0) return walls;

            if (MergeFaces)
                for (int i = 0; i < wallJoin.Length; i++)
                    wallJoin[i] = TryMerge(wallJoin[i], tol);

            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                "Walls only: " + wallJoin.Length + " wall band(s).");
            return wallJoin;
        }

        List<Brep> parts = new List<Brep> { brep.DuplicateBrep() };
        parts.AddRange(walls);

        // 6. Top faces: each face pushed through its own field (follows the surface)
        for (int fi = 0; fi < brep.Faces.Count; fi++)
        {
            Brep fb = brep.Faces[fi].DuplicateFace(false);
            if (fb == null) continue;
            fb.MakeDeformable();

            if (!fields[fi].Morph(fb))
            {
                Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Top face " + fi + " morph failed.");
                continue;
            }
            parts.Add(fb);
        }

        // 7. Join with escalating tolerance
        double usedMult;
        Brep[] joined = JoinWithRetry(parts, tol, out usedMult);

        if (joined == null || joined.Length == 0)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Join failed; outputting loose parts.");
            return parts;
        }

        if (joined.Length > 1)
        {
            string info2 = string.Join(", ", joined.Select((b, k) =>
                "#" + k + ": " + b.Faces.Count + " faces / " +
                b.Edges.Count(e => e.Valence == EdgeAdjacency.Naked) + " naked edges"));
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                "Join produced " + joined.Length + " pieces -> " + info2);
            return joined;
        }

        if (usedMult > 1.0)
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Joined at " + usedMult + "x document tolerance.");

        return Finish(joined[0], MergeFaces, tol);
    }

    // ---------- closed brep ----------

    private object BuildClosed(Brep brep, double Height, double tol, double kinkAngle, bool merge)
    {
        List<Node> nodes;
        List<EdgeInfo> edges;
        FaceField[] fields;
        if (!Prepare(brep, Height, 0.0, tol, kinkAngle, 0.0, out nodes, out edges, out fields))
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No usable edges found on closed brep.");
            return null;
        }

        // Offset every face through its field (creases stay sharp and joined)
        List<Brep> tops = new List<Brep>();
        for (int fi = 0; fi < brep.Faces.Count; fi++)
        {
            Brep fb = brep.Faces[fi].DuplicateFace(false);
            if (fb == null) continue;
            fb.MakeDeformable();

            if (!fields[fi].Morph(fb))
            {
                Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Offset face " + fi + " morph failed.");
                continue;
            }
            tops.Add(fb);
        }

        double usedMult;
        Brep[] shell = JoinWithRetry(tops, tol, out usedMult);
        if (shell == null || shell.Length != 1 || !shell[0].IsSolid)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                "Offset shell did not close; outputting loose offset faces.");
            return tops;
        }
        if (usedMult > 1.0)
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Joined at " + usedMult + "x document tolerance.");

        Brep baseShell = brep.DuplicateBrep();
        Brep offShell = shell[0];
        if (merge)
        {
            baseShell = TryMerge(baseShell, tol);
            offShell = TryMerge(offShell, tol);
        }

        // Bigger shell is the outside, smaller shell is the cavity
        bool offIsOuter = ShellVolume(offShell) >= ShellVolume(baseShell);
        Brep outer = offIsOuter ? offShell : baseShell;
        Brep inner = offIsOuter ? baseShell : offShell;

        if (outer.SolidOrientation == BrepSolidOrientation.Inward) outer.Flip();
        if (inner.SolidOrientation == BrepSolidOrientation.Outward) inner.Flip();

        Brep result = outer.DuplicateBrep();
        result.Append(inner);

        if (!result.IsValid)
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Hollow solid is not valid (offset may self-intersect).");

        Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Closed brep: output is a hollow solid (outer + inner shell).");
        return result;
    }

    private static double ShellVolume(Brep b)
    {
        VolumeMassProperties vmp = VolumeMassProperties.Compute(b);
        if (vmp != null) return Math.Abs(vmp.Volume);
        return b.GetBoundingBox(true).Diagonal.Length;
    }

    // ---------- core build: corners, fields, top edges ----------

    private static bool Prepare(Brep brep, double Height, double side, double tol, double kinkAngle, double miterLimit,
        out List<Node> nodes, out List<EdgeInfo> edges, out FaceField[] fields)
    {
        nodes = new List<Node>();
        edges = new List<EdgeInfo>();
        fields = new FaceField[brep.Faces.Count];

        // 2. Collect edges (split at corners) and corner nodes
        foreach (BrepEdge edge in brep.Edges)
        {
            int[] adj = edge.AdjacentFaces();
            if (adj == null || adj.Length == 0) continue;
            adj = adj.Distinct().ToArray();

            Curve ec = edge.DuplicateCurve();
            if (ec == null) continue;

            bool naked = edge.Valence == EdgeAdjacency.Naked;
            int sign = naked ? InsideSign(brep.Faces[adj[0]], ec, tol) : 0;

            foreach (Curve seg in Segments(ec, Kinks(ec, kinkAngle)))
            {
                EdgeInfo info = new EdgeInfo();
                info.Seg = seg;
                info.Faces = adj;
                info.Naked = naked;
                info.Sign = sign;
                info.Start = GetNode(nodes, seg.PointAtStart, tol);
                info.End = GetNode(nodes, seg.PointAtEnd, tol);

                foreach (int fi in adj)
                {
                    info.Start.Faces.Add(fi);
                    info.End.Faces.Add(fi);
                }

                if (naked)
                {
                    BrepFace f = brep.Faces[adj[0]];
                    info.Start.Walls.Add(WallData(f, seg.PointAtStart, seg.TangentAtStart, sign, Height, side));
                    info.End.Walls.Add(WallData(f, seg.PointAtEnd, seg.TangentAtEnd, sign, Height, side));
                }

                edges.Add(info);
            }
        }

        if (edges.Count == 0) return false;

        // 3. Solve each corner: on every face offset plane and every wall plane meeting there
        foreach (Node nd in nodes)
            nd.V = SolveNode(brep, nd, Height);

        // Option: limit corner overshoot (single-face corners only, keeps ridge corners exact)
        if (miterLimit > 0.0 && Math.Abs(side) > tol)
        {
            double maxLen = miterLimit * Math.Abs(side);
            foreach (Node nd in nodes)
                if (nd.Walls.Count > 0 && nd.Faces.Count == 1)
                    LimitCorner(nd, maxLen);
        }

        // 4. Per-face displacement fields
        for (int fi = 0; fi < brep.Faces.Count; fi++)
            fields[fi] = new FaceField(brep.Faces[fi], Height, tol);

        foreach (EdgeInfo info in edges)
        {
            foreach (int fi in info.Faces)
            {
                BrepFace f = brep.Faces[fi];
                int gi = -1;
                foreach (int other in info.Faces)
                    if (other != fi) gi = other;

                int count = info.Seg.IsLinear(tol) ? 1 : 24;

                Point3d prevP = info.Seg.PointAtStart;
                Vector3d prevR = info.Start.V - FaceNormal(f, prevP) * Height;

                for (int j = 1; j <= count; j++)
                {
                    Point3d p;
                    Vector3d r;

                    if (j == count)
                    {
                        p = info.Seg.PointAtEnd;
                        r = info.End.V - FaceNormal(f, p) * Height;
                    }
                    else
                    {
                        double t = info.Seg.Domain.ParameterAt((double)j / count);
                        p = info.Seg.PointAt(t);
                        Vector3d n = FaceNormal(f, p);

                        if (info.Naked)
                        {
                            r = UnitCross(n, info.Seg.TangentAt(t)) * (info.Sign * side);
                        }
                        else if (gi >= 0)
                        {
                            Vector3d ng = FaceNormal(brep.Faces[gi], p);
                            r = (Miter(n, ng) - n) * Height;
                        }
                        else
                        {
                            r = Vector3d.Zero;
                        }
                    }

                    fields[fi].AddPiece(prevP, p, prevR, r);
                    prevP = p;
                    prevR = r;
                }
            }
        }

        // Top edge of each wall
        foreach (EdgeInfo info in edges)
            if (info.Naked)
                info.Top = TopCurve(brep, info, fields, tol);

        return true;
    }

    private static Curve TopCurve(Brep brep, EdgeInfo info, FaceField[] fields, double tol)
    {
        int fi = info.Faces[0];

        if (info.Seg.IsLinear(tol) && brep.Faces[fi].IsPlanar(tol))
            return new LineCurve(info.Seg.PointAtStart + info.Start.V, info.Seg.PointAtEnd + info.End.V);

        NurbsCurve nc = info.Seg.ToNurbsCurve();
        if (nc == null || !fields[fi].Morph(nc)) return null;
        return nc;
    }

    // ---------- options ----------

    // Pull an over-long corner back toward the average of its wall rulings (stays on the offset surface)
    private static void LimitCorner(Node nd, double maxLen)
    {
        Vector3d v0 = Vector3d.Zero;
        foreach (Vector3d[] w in nd.Walls) v0 += w[1];
        v0 /= nd.Walls.Count;

        Vector3d excess = nd.V - v0;
        double len = excess.Length;
        if (len > maxLen && len > RhinoMath.ZeroTolerance)
            nd.V = v0 + excess * (maxLen / len);
    }

    // Counts collapsed wall edges and crossing top loops
    private static int CountFolds(List<EdgeInfo> edges, double tol)
    {
        int problems = 0;
        List<Curve> tops = new List<Curve>();

        foreach (EdgeInfo info in edges)
        {
            if (!info.Naked) continue;
            if (info.Top == null)
            {
                problems++;
                continue;
            }

            Vector3d baseDir = info.Seg.PointAtEnd - info.Seg.PointAtStart;
            Vector3d topDir = info.Top.PointAtEnd - info.Top.PointAtStart;
            if (baseDir.Length > tol && baseDir * topDir <= 0.0) problems++; // edge flipped or collapsed

            tops.Add(info.Top);
        }

        if (tops.Count == 0) return problems;

        Curve[] loops = Curve.JoinCurves(tops, tol * 10.0);
        if (loops == null) return problems;

        for (int i = 0; i < loops.Length; i++)
        {
            CurveIntersections self = Intersection.CurveSelf(loops[i], tol);
            if (self != null) problems += self.Count;

            for (int j = i + 1; j < loops.Length; j++)
            {
                CurveIntersections x = Intersection.CurveCurve(loops[i], loops[j], tol, tol);
                if (x != null) problems += x.Count;
            }
        }
        return problems;
    }

    private static Brep RhinoOffset(Brep brep, double height, double tol)
    {
        Brep[] blends;
        Brep[] walls;
        Brep[] res = Brep.CreateOffsetBrep(brep, height, true, true, tol, out blends, out walls);
        if (res == null || res.Length != 1 || !res[0].IsValid || !res[0].IsSolid) return null;
        return res[0];
    }

    private static Brep[] JoinWithRetry(List<Brep> parts, double tol, out double usedMult)
    {
        Brep[] joined = null;
        usedMult = 1.0;
        foreach (double mult in new[] { 1.0, 2.0, 5.0, 10.0 })
        {
            joined = Brep.JoinBreps(parts, tol * mult);
            usedMult = mult;
            if (joined != null && joined.Length == 1) break;
        }
        return joined;
    }

    private static Brep TryMerge(Brep b, double tol)
    {
        Brep merged = b.DuplicateBrep();
        if (merged.MergeCoplanarFaces(tol) && merged.IsValid) return merged;
        return b;
    }

    private object Finish(Brep result, bool merge, double tol)
    {
        if (merge) result = TryMerge(result, tol);

        if (!result.IsValid)
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Result brep is not valid.");

        if (!result.IsSolid)
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Result is not closed.");
        else if (result.SolidOrientation == BrepSolidOrientation.Inward)
            result.Flip();

        return result;
    }

    // ---------- corner solve ----------

    private static Vector3d[] WallData(BrepFace f, Point3d p, Vector3d tangent, int sign, double h, double side)
    {
        Vector3d n = FaceNormal(f, p);
        Vector3d b = UnitCross(n, tangent) * sign;
        return new[] { tangent, n * h + b * side };
    }

    private static Vector3d SolveNode(Brep brep, Node nd, double h)
    {
        double[,] m = new double[3, 3];
        double[] rhs = new double[3];

        List<Vector3d> normals = new List<Vector3d>();
        foreach (int fi in nd.Faces)
        {
            Vector3d n = FaceNormal(brep.Faces[fi], nd.P);
            normals.Add(n);
            AddRow(m, rhs, n, h);
        }

        Vector3d v0 = Vector3d.Zero;
        foreach (Vector3d[] w in nd.Walls)
        {
            Vector3d wn = Vector3d.CrossProduct(w[0], w[1]);
            if (wn.Unitize()) AddRow(m, rhs, wn, 0.0);
            v0 += w[1];
        }

        if (nd.Walls.Count > 0) v0 /= nd.Walls.Count;
        else if (normals.Count >= 2) v0 = Miter(normals[0], normals[1]) * h;
        else if (normals.Count == 1) v0 = normals[0] * h;

        // Weak pull toward the naive answer for under-constrained corners
        const double lambda = 1e-6;
        for (int i = 0; i < 3; i++)
        {
            m[i, i] += lambda;
            rhs[i] += lambda * v0[i];
        }

        return Solve3(m, rhs, v0);
    }

    private static void AddRow(double[,] m, double[] rhs, Vector3d a, double c)
    {
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++) m[i, j] += a[i] * a[j];
            rhs[i] += a[i] * c;
        }
    }

    private static Vector3d Solve3(double[,] m, double[] r, Vector3d fallback)
    {
        double det = Det(m[0, 0], m[0, 1], m[0, 2], m[1, 0], m[1, 1], m[1, 2], m[2, 0], m[2, 1], m[2, 2]);
        if (Math.Abs(det) < 1e-18) return fallback;

        double x = Det(r[0], m[0, 1], m[0, 2], r[1], m[1, 1], m[1, 2], r[2], m[2, 1], m[2, 2]) / det;
        double y = Det(m[0, 0], r[0], m[0, 2], m[1, 0], r[1], m[1, 2], m[2, 0], r[2], m[2, 2]) / det;
        double z = Det(m[0, 0], m[0, 1], r[0], m[1, 0], m[1, 1], r[1], m[2, 0], m[2, 1], r[2]) / det;
        return new Vector3d(x, y, z);
    }

    private static double Det(double a, double b, double c, double d, double e, double f, double g, double h, double i)
    {
        return a * (e * i - f * h) - b * (d * i - f * g) + c * (d * h - e * g);
    }

    // ---------- helpers ----------

    private static Vector3d FaceNormal(BrepFace face, Point3d p)
    {
        double u, v;
        face.ClosestPoint(p, out u, out v);
        Vector3d n = face.NormalAt(u, v);
        if (face.OrientationIsReversed) n.Reverse();
        n.Unitize();
        return n;
    }

    private static Vector3d UnitCross(Vector3d a, Vector3d b)
    {
        Vector3d c = Vector3d.CrossProduct(a, b);
        if (!c.Unitize()) return Vector3d.Zero;
        return c;
    }

    // Offset direction that sits at unit distance from both planes (sharp crease)
    private static Vector3d Miter(Vector3d n1, Vector3d n2)
    {
        double denom = 1.0 + n1 * n2;
        if (denom < 1e-3) return n1;
        return (n1 + n2) / denom;
    }

    // +1 if (normal x tangent) points into the face, else -1
    private static int InsideSign(BrepFace face, Curve ec, double tol)
    {
        double tm = ec.Domain.Mid;
        Point3d mid = ec.PointAt(tm);
        Vector3d b = UnitCross(FaceNormal(face, mid), ec.TangentAt(tm));
        if (b.IsZero) return 1;

        double len = ec.GetLength();
        foreach (double delta in new[] { tol * 10.0, tol * 100.0, len * 0.01 })
        {
            bool plus = IsInside(face, mid + b * delta);
            bool minus = IsInside(face, mid - b * delta);
            if (plus && !minus) return 1;
            if (minus && !plus) return -1;
        }
        return 1;
    }

    private static bool IsInside(BrepFace face, Point3d p)
    {
        double u, v;
        if (!face.ClosestPoint(p, out u, out v)) return false;
        return face.IsPointOnFace(u, v) == PointFaceRelation.Interior;
    }

    private static Node GetNode(List<Node> nodes, Point3d p, double tol)
    {
        foreach (Node nd in nodes)
            if (nd.P.DistanceTo(p) < tol * 10.0) return nd;

        Node created = new Node();
        created.P = p;
        nodes.Add(created);
        return created;
    }

    private static List<double> Kinks(Curve c, double kinkAngle)
    {
        List<double> ts = new List<double>();
        double cosTol = Math.Cos(kinkAngle);
        double start = c.Domain.Min;
        double end = c.Domain.Max;
        double t;

        while (c.GetNextDiscontinuity(Continuity.G1_continuous, start, end, cosTol, 1e-3, out t))
        {
            if (t <= start || t >= end - RhinoMath.ZeroTolerance) break;
            ts.Add(t);
            start = t;
        }
        return ts;
    }

    private static List<Curve> Segments(Curve c, List<double> kinks)
    {
        List<Curve> segs = new List<Curve>();
        if (kinks.Count == 0)
        {
            segs.Add(c.DuplicateCurve());
            return segs;
        }

        List<double> p = new List<double> { c.Domain.Min };
        p.AddRange(kinks);
        p.Add(c.Domain.Max);

        for (int i = 0; i < p.Count - 1; i++)
        {
            Curve s = c.Trim(p[i], p[i + 1]);
            if (s != null) segs.Add(s);
        }
        return segs;
    }

    // ---------- per-face displacement ----------

    // Point on face -> point + normal * h + in-surface shift.
    // Shift is exact on the face's edges and blended smoothly inside.
    private class FaceField : SpaceMorph
    {
        private readonly BrepFace _face;
        private readonly double _h;
        private readonly List<Point3d[]> _pts = new List<Point3d[]>();
        private readonly List<Vector3d[]> _res = new List<Vector3d[]>();

        public FaceField(BrepFace face, double h, double tol)
        {
            _face = face;
            _h = h;
            Tolerance = tol;
            PreserveStructure = false;
            QuickPreview = false;
        }

        public void AddPiece(Point3d a, Point3d b, Vector3d ra, Vector3d rb)
        {
            _pts.Add(new[] { a, b });
            _res.Add(new[] { ra, rb });
        }

        public override Point3d MorphPoint(Point3d point)
        {
            Vector3d n = FaceNormal(_face, point);
            return point + n * _h + Shift(point, n);
        }

        private Vector3d Shift(Point3d p, Vector3d n)
        {
            Vector3d sum = Vector3d.Zero;
            double wSum = 0.0;

            for (int i = 0; i < _pts.Count; i++)
            {
                Point3d a = _pts[i][0];
                Vector3d ab = _pts[i][1] - a;
                double l2 = ab.SquareLength;

                double u = (l2 > 1e-18) ? ((p - a) * ab) / l2 : 0.0;
                u = Math.Max(0.0, Math.Min(1.0, u));

                double d2 = p.DistanceToSquared(a + ab * u);
                Vector3d val = _res[i][0] * (1.0 - u) + _res[i][1] * u;
                if (d2 < 1e-14) return val; // on an edge: exact

                double w = Math.Sqrt(l2) / d2;
                sum += val * w;
                wSum += w;
            }

            if (wSum <= 0.0) return Vector3d.Zero;
            Vector3d s = sum / wSum;
            return s - (s * n) * n; // keep the shift tangent to the surface
        }
    }
}