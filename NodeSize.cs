/*
  Author: Rajeev Pulari + Gemini
  Rhino 8 | Grasshopper C#
  Version: 2026.09.30
  Component: Node Size Calculator v2.2
  Description: Sizes a space-frame node from the two closest members meeting at it.
               Angle between them -> node length -> rounded-up node radius.
*/

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
    // Full-word names (were lines / unique_pts / thk / dia / round and angle / length / radius / refpoint)
    // Use item access for Node, Thickness, Diameter, Rounding; list access for Curves.
    private void RunScript(
        List<Curve> Curves,
        Point3d Node,
        double Thickness,
        double Diameter,
        double Rounding,
        ref object Angle,
        ref object Length,
        ref object Radius,
        ref object RefPoint)
    {
        // Metadata + pin tooltips (once)
        if (this.Component != null && this.Component.Name != "Node Size Calculator")
        {
            this.Component.Name = "Node Size Calculator";
            this.Component.NickName = "nodeSize";
            this.Component.Message = "Node Sizes v2.2";
            this.Component.Description = "Sizes a node from the smallest angle between the members meeting at it.";

            var pi = this.Component.Params.Input;
            SetTip(pi, 0, "Curves", "Curves (members) touching this node - e.g. one branch of NodeCurves from sharedNodes. List access.");
            SetTip(pi, 1, "Node", "The node point. Item access.");
            SetTip(pi, 2, "Thickness", "Added wall thickness. Zero or less uses 10.");
            SetTip(pi, 3, "Diameter", "Member diameter to clear. Zero or less uses 12.");
            SetTip(pi, 4, "Rounding", "Radius is rounded UP to a multiple of this. Zero or less uses 1.");
            var po = this.Component.Params.Output;
            SetTip(po, 0, "Angle", "Smallest angle between two members at the node, in degrees.");
            SetTip(po, 1, "Length", "Node length = Diameter / sin(Angle/2) + Thickness.");
            SetTip(po, 2, "Radius", "Length rounded up to a multiple of Rounding.");
            SetTip(po, 3, "RefPoint", "The node point (for placing labels / geometry).");
        }

        // 1. Defaults for unset / non-positive values
        const double DEFAULT_DISTANCE = 30.0;   // internal probe distance (angle only, so value is irrelevant)
        const double DEFAULT_THICKNESS = 10.0;
        const double DEFAULT_DIAMETER = 12.0;
        const double DEFAULT_ROUNDING = 1.0;

        double thk = (Thickness <= 0) ? DEFAULT_THICKNESS : Thickness;
        double dia = (Diameter <= 0) ? DEFAULT_DIAMETER : Diameter;
        double rnd = (Rounding <= 0) ? DEFAULT_ROUNDING : Rounding;

        if (!Node.IsValid)
        {
            this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Node point is invalid.");
            return;
        }
        if (Curves == null || Curves.Count == 0)
        {
            this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No curves at this node.");
            return;
        }

        RefPoint = Node;

        // 2. Probe point on each member, at a fixed distance from the node
        List<Point3d> probes = new List<Point3d>();
        foreach (Curve crv in Curves)
        {
            if (crv == null) continue;

            Point3d mid = crv.PointAtNormalizedLength(0.5);
            Vector3d vec = mid - Node;
            if (vec.IsZero) continue;

            vec.Unitize();
            vec *= DEFAULT_DISTANCE;
            probes.Add(Node + vec);
        }

        if (probes.Count < 2)
        {
            this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                "Fewer than two usable members at this node - no angle to size.");
            return;
        }

        // 3. Closest pair of probes = smallest angle between members
        double minDist = double.MaxValue;
        Point3d pointA = Point3d.Unset;
        Point3d pointB = Point3d.Unset;
        for (int i = 0; i < probes.Count; i++)
        {
            for (int j = i + 1; j < probes.Count; j++)
            {
                double d = probes[i].DistanceTo(probes[j]);
                if (d < minDist)
                {
                    minDist = d;
                    pointA = probes[i];
                    pointB = probes[j];
                }
            }
        }

        // 4. Geometry
        Vector3d v1 = pointA - Node;
        Vector3d v2 = pointB - Node;
        v1.Unitize();
        v2.Unitize();

        double dot = Math.Max(-1.0, Math.Min(1.0, v1 * v2));
        double angleRad = Math.Acos(dot);

        double sinHalf = Math.Sin(angleRad * 0.5);
        if (Math.Abs(sinHalf) <= 1e-9)
        {
            this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                "Two members overlap (angle ~0): node length is undefined.");
            return;
        }

        double length = (dia / sinHalf) + thk;
        Angle = RhinoMath.ToDegrees(angleRad);
        Length = length;
        Radius = Math.Ceiling(length / rnd) * rnd;
    }

    private void SetTip(IList<IGH_Param> ps, int i, string name, string tip)
    {
        if (ps == null || i < 0 || i >= ps.Count) return;
        ps[i].NickName = name;
        ps[i].Description = tip;
    }
}
