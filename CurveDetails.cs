#region References
// Force references for Rhino 8 Roslyn Engine
#r "sdk:RhinoCommon"
#r "sdk:Grasshopper"

using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Geometry;
using Grasshopper.Kernel;
#endregion

public class Script_Instance : GH_ScriptInstance
{
    /*
    Author: Rajeev Pulari
    Version: 2026.05.13
    Description: Extracting Curve Geometric Properties
    */

    private void RunScript(
		Curve C,
		ref object L,
		ref object D,
		ref object P,
		ref object K,
		ref object I,
		ref object T)
    {
        // Metadata + pin tooltips (once)
        if (this.Component != null && this.Component.Name != "Curve Details")
        {
            this.Component.Name = "Curve Details";
            this.Component.NickName = "CrvProp";
            this.Component.Description = "Extracts geometric properties and details from a Rhino curve.";
            this.Component.Message = "Curve Details v2.0";

            SetTip(this.Component.Params.Input, 0, "Curve", "The curve to inspect.");
            SetTip(this.Component.Params.Output, 0, "Length", "Length of the curve.");
            SetTip(this.Component.Params.Output, 1, "Domain", "Parameter domain (start..end).");
            SetTip(this.Component.Params.Output, 2, "Points", "Key points: start, middle, end.");
            SetTip(this.Component.Params.Output, 3, "Structure", "NURBS structure: degree and span count.");
            SetTip(this.Component.Params.Output, 4, "Info", "Flags: closed, periodic, planar.");
            SetTip(this.Component.Params.Output, 5, "Type", "Object type and .NET class name.");
        }

        // 1. Input validation
        if (C == null)
        {
            this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Input curve 'C' is null.");
            return;
        }

        // 2. Length & Domain
        L = C.GetLength();
        D = C.Domain;

        // 3. Key points (start, mid-parameter, end)
        List<Point3d> keyPoints = new List<Point3d>();
        keyPoints.Add(C.PointAtStart);
        keyPoints.Add(C.PointAt(C.Domain.Mid));
        keyPoints.Add(C.PointAtEnd);
        P = keyPoints;

        // 4. Mathematical structure
        K = "Degree: " + C.Degree + " | Spans: " + C.SpanCount;

        // 5. Geometric intelligence
        // Use the model tolerance, but fall back when no document is open
        // (the original dereferenced RhinoDoc.ActiveDoc directly and would
        // crash in a headless / no-document context).
        double tol = (RhinoDoc.ActiveDoc != null)
            ? RhinoDoc.ActiveDoc.ModelAbsoluteTolerance
            : RhinoMath.ZeroTolerance;
        bool isPlanar = C.IsPlanar(tol);
        I = "Closed: " + C.IsClosed + " | Periodic: " + C.IsPeriodic + " | Planar: " + isPlanar;

        // 6. Detailed type info (LineCurve, PolylineCurve, NurbsCurve, ...)
        T = C.ObjectType + " (" + C.GetType().Name + ")";
    }

    private void SetTip(System.Collections.Generic.IList<IGH_Param> ps, int i, string name, string tip)
    {
        if (ps == null || i < 0 || i >= ps.Count) return;
        ps[i].NickName = name;
        ps[i].Description = tip;
    }
}
