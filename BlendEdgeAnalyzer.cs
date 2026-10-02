#region Metadata
/*
  Author      : Rajeev Pulari + Gemini
  Platform    : Rhino 8 | Grasshopper C#
  Version     : 2026.10.02
  Component   : Brep Advanced Blender
  NickName    : BlendAnalyze
  Message     : Brep Edge Blend v2.2
  Description : Blend surface between an edge of Brep A and an edge of Brep B
                (like Rhino BlendSrf), then reads its NURBS structure: control
                points, weights and Greville (u, v) parameters, in Grasshopper's
                native order (U outer, V inner).

  Inputs:
    BrepA, BrepB             : Brep (Item) - the two breps
    EdgeIndexA, EdgeIndexB   : int  (Item) - edge to blend from on each brep
    FlipA, FlipB             : bool (Item) - flip the blend direction on that side
    ContinuityA, ContinuityB : int  (Item) - 0 = G0 position, 1 = G1 tangency, 2 = G2 curvature

  Outputs:
    BlendBrep     : the blend surface(s)
    ControlPoints : control points, U outer / V inner
    Weights       : matching weights
    Greville      : Greville parameters as points {u, v, 0}
    UCount, VCount: control point counts
    Report        : degrees, CV grid, continuity used
*/
#endregion

#region Usings
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Text;

using Rhino;
using Rhino.Geometry;
using Grasshopper.Kernel;
#endregion

public class Script_Instance : GH_ScriptInstance
{
    private void RunScript(
        Brep BrepA,
        int EdgeIndexA,
        bool FlipA,
        int ContinuityA,
        Brep BrepB,
        int EdgeIndexB,
        bool FlipB,
        int ContinuityB,
        ref object BlendBrep,
        ref object ControlPoints,
        ref object Weights,
        ref object Greville,
        ref object UCount,
        ref object VCount,
        ref object Report)
    {
        // Metadata + pin tooltips (once)
        if (Component != null && Component.Name != "Brep Advanced Blender")
        {
            Component.Name = "Brep Advanced Blender";
            Component.NickName = "BlendAnalyze";
            Component.Message = "Brep Edge Blend v2.2";
            Component.Description = "Blend surface between two brep edges (G0-G2) plus its control points, weights and Greville parameters.";

            var pi = Component.Params.Input;
            SetTip(pi, "BrepA", "First brep.");
            SetTip(pi, "EdgeIndexA", "Edge of BrepA to blend from.");
            SetTip(pi, "FlipA", "Flip the blend direction on side A.");
            SetTip(pi, "ContinuityA", "0 = G0 position, 1 = G1 tangency, 2 = G2 curvature.");
            SetTip(pi, "BrepB", "Second brep.");
            SetTip(pi, "EdgeIndexB", "Edge of BrepB to blend to.");
            SetTip(pi, "FlipB", "Flip the blend direction on side B.");
            SetTip(pi, "ContinuityB", "0 = G0 position, 1 = G1 tangency, 2 = G2 curvature.");
            var po = Component.Params.Output;
            SetTip(po, "BlendBrep", "The blend surface(s).");
            SetTip(po, "ControlPoints", "Control points of the blend, U outer / V inner (like Surface CP).");
            SetTip(po, "Weights", "Control point weights, same order.");
            SetTip(po, "Greville", "Greville parameters as points {u, v, 0}, same order.");
            SetTip(po, "UCount", "Control point count in U.");
            SetTip(po, "VCount", "Control point count in V.");
            SetTip(po, "Report", "Degrees, CV grid and continuity used.");
        }

        if (BrepA == null || BrepB == null)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Waiting for Brep inputs.");
            return;
        }

        // Edge indices checked up front (out-of-range used to surface as a raw exception)
        if (EdgeIndexA < 0 || EdgeIndexA >= BrepA.Edges.Count)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "EdgeIndexA must be 0.." + (BrepA.Edges.Count - 1) + ".");
            return;
        }
        if (EdgeIndexB < 0 || EdgeIndexB >= BrepB.Edges.Count)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "EdgeIndexB must be 0.." + (BrepB.Edges.Count - 1) + ".");
            return;
        }

        // RhinoCommon's BlendContinuity only has Position (0), Tangency (1) and Curvature (2).
        // Values 3/4 were cast to non-existent enum values - clamp and say so.
        int cA = Clamp(ContinuityA, "ContinuityA");
        int cB = Clamp(ContinuityB, "ContinuityB");

        try
        {
            // 1. Blend
            BrepEdge eA = BrepA.Edges[EdgeIndexA];
            BrepEdge eB = BrepB.Edges[EdgeIndexB];
            int[] facesA = eA.AdjacentFaces();
            int[] facesB = eB.AdjacentFaces();
            if (facesA == null || facesA.Length == 0 || facesB == null || facesB.Length == 0)
            {
                Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "An edge has no adjacent face.");
                return;
            }
            if (eA.Valence != EdgeAdjacency.Naked || eB.Valence != EdgeAdjacency.Naked)
                Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                    "An edge is not a naked (open) edge - the blend uses its first adjacent face.");

            Brep[] results = Brep.CreateBlendSurface(
                BrepA.Faces[facesA[0]], eA, eA.Domain, FlipA, (BlendContinuity)cA,
                BrepB.Faces[facesB[0]], eB, eB.Domain, FlipB, (BlendContinuity)cB);

            if (results == null || results.Length == 0)
            {
                Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Blend creation failed. Check the edges and try FlipA / FlipB.");
                return;
            }
            BlendBrep = results;

            // 2. NURBS structure of the first blend surface
            NurbsSurface nurb = results[0].Surfaces[0].ToNurbsSurface();
            int uC = nurb.Points.CountU;
            int vC = nurb.Points.CountV;
            int degU = nurb.Degree(0);
            int degV = nurb.Degree(1);
            UCount = uC;
            VCount = vC;

            double[] grevU = GrevilleParams(nurb.KnotsU, degU, uC);
            double[] grevV = GrevilleParams(nurb.KnotsV, degV, vC);

            var pts = new List<Point3d>();
            var wts = new List<double>();
            var grev = new List<Point3d>();
            for (int u = 0; u < uC; u++)          // U outer, V inner (native GH order)
            {
                for (int v = 0; v < vC; v++)
                {
                    ControlPoint cp = nurb.Points.GetControlPoint(u, v);
                    pts.Add(cp.Location);
                    wts.Add(cp.Weight);
                    grev.Add(new Point3d(grevU[u], grevV[v], 0.0));
                }
            }
            ControlPoints = pts;
            Weights = wts;
            Greville = grev;

            // 3. Report (states what was actually built)
            string[] names = { "G0 position", "G1 tangency", "G2 curvature" };
            var sb = new StringBuilder();
            sb.AppendLine("--- Blend NURBS Analysis ---");
            sb.AppendLine("Continuity: A = " + names[cA] + ", B = " + names[cB]);
            sb.AppendLine("Surface degree: U(" + degU + ") V(" + degV + ")");
            sb.AppendLine("CV grid: " + uC + " x " + vC);
            sb.AppendLine("Rational: " + (nurb.IsRational ? "yes" : "no"));
            if (results.Length > 1) sb.AppendLine("Blend pieces: " + results.Length + " (analysis uses the first)");
            Report = sb.ToString();

            Component.Message = "Brep Edge Blend v2.2 | G" + cA + "/G" + cB;
        }
        catch (Exception ex)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            Report = "Error: " + ex.Message;
        }
    }

    // <Custom additional code>
    private int Clamp(int value, string name)
    {
        int c = Math.Max(0, Math.Min(2, value));
        if (c != value)
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                name + " = " + value + " is not available - RhinoCommon blends support G0-G2; G" + c + " used.");
        return c;
    }

    // Greville abscissa i = average of 'degree' consecutive knots from i (Rhino knot vector)
    private static double[] GrevilleParams(Rhino.Geometry.Collections.NurbsSurfaceKnotList knots, int degree, int cvCount)
    {
        var g = new double[cvCount];
        for (int i = 0; i < cvCount; i++)
        {
            double sum = 0.0;
            for (int j = 0; j < degree; j++) sum += knots[i + j];
            g[i] = degree > 0 ? sum / degree : 0.0;
        }
        return g;
    }

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
