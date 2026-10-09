/*
  Author: Rajeev Pulari + Gemini
  Rhino 8 | Grasshopper C#
  Version: 2026.04.06
  Component: NodeSize v1.0
  Description: Defines node sizes based on curve midpoints and proximity vectors.
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
    private void RunScript(
		List<Curve> lines,
		Point3d unique_pts,
		double thk,
		double dia,
		double round,
		ref object angle,
		ref object length,
		ref object radius,
		ref object refpoint)
    {
    SetPinTips();   // pin tooltips (set once, matched by name)

        // --------------------------------------------------------------
        // 0️⃣ Set Component Metadata
        // --------------------------------------------------------------
        if (this.Component != null)
        {
            this.Component.Name     = "Node Size Calculator";
            this.Component.NickName = "nodeSize";
            this.Component.Message  = "Node Sizes v2.1";
        }

        // --------------------------------------------------------------
        // 1️⃣ Apply Default Values if inputs are unset / zero
        // --------------------------------------------------------------
        const double DEFAULT_DISTANCE = 30.0;
        const double DEFAULT_THK      = 10.0;
        const double DEFAULT_DIA      = 12.0;
        const double DEFAULT_ROUND    = 1.0;

        double _distance = DEFAULT_DISTANCE;          // ← internal only, no longer exposed
        double _thk      = (thk   <= 0) ? DEFAULT_THK   : thk;
        double _dia      = (dia   <= 0) ? DEFAULT_DIA   : dia;
        double _round    = (round <= 0) ? DEFAULT_ROUND : round;

        // --------------------------------------------------------------
        // 2️⃣ Initialize internal variables
        // --------------------------------------------------------------
        Point3d out_pointA   = Point3d.Unset;
        Point3d out_pointB   = Point3d.Unset;
        double  out_angle    = 0.0;
        double  out_length   = 0.0;
        double  out_radius   = 0.0;
        double  out_min_dist = double.MaxValue;

        if (!unique_pts.IsValid || lines == null || lines.Count == 0) return;

        // --------------------------------------------------------------
        // 3️⃣ Reference Point Output
        // --------------------------------------------------------------
        refpoint = unique_pts;

        // --------------------------------------------------------------
        // 4️⃣ Process Curves relative to the Center Point (internal)
        // --------------------------------------------------------------
        List<Point3d> ptsList = new List<Point3d>();
        foreach (Curve crv in lines)
        {
            if (crv == null) continue;

            Point3d  mid = crv.PointAtNormalizedLength(0.5);
            Vector3d vec = mid - unique_pts;
            if (vec.IsZero) continue;

            vec.Unitize();
            vec *= _distance;
            ptsList.Add(unique_pts + vec);
        }

        // --------------------------------------------------------------
        // 5️⃣ Find Closest Pair (internal)
        // --------------------------------------------------------------
        if (ptsList.Count >= 2)
        {
            for (int i = 0; i < ptsList.Count; i++)
            {
                for (int j = i + 1; j < ptsList.Count; j++)
                {
                    double d = ptsList[i].DistanceTo(ptsList[j]);
                    if (d < out_min_dist)
                    {
                        out_min_dist = d;
                        out_pointA   = ptsList[i];
                        out_pointB   = ptsList[j];
                    }
                }
            }
        }

        // --------------------------------------------------------------
        // 6️⃣ Compute Geometry
        // --------------------------------------------------------------
        if (out_pointA.IsValid && out_pointB.IsValid)
        {
            Vector3d v1 = out_pointA - unique_pts;
            Vector3d v2 = out_pointB - unique_pts;
            v1.Unitize();
            v2.Unitize();

            double dot     = Math.Max(-1.0, Math.Min(1.0, v1 * v2));
            double rad_val = Math.Acos(dot);
            out_angle      = RhinoMath.ToDegrees(rad_val);

            double sinHalf = Math.Sin(rad_val * 0.5);
            if (Math.Abs(sinHalf) > 1e-9)
            {
                out_length = (_dia / sinHalf) + _thk;
                out_radius = Math.Ceiling(out_length / _round) * _round;
            }

            angle  = out_angle;
            length = out_length;
            radius = out_radius;
        }
    }

  // ---------------------------------------------------------------- pin tooltips
  private bool _pinTipsSet = false;

  private void SetPinTips()
  {
    if (_pinTipsSet || Component == null) return;
    _pinTipsSet = true;
    TipPin(Component.Params.Input, "lines", "Members touching this node.");
    TipPin(Component.Params.Input, "unique_pts", "The node point.");
    TipPin(Component.Params.Input, "thk", "Added wall thickness (0 or less uses 10)");
    TipPin(Component.Params.Input, "dia", "Member diameter to clear (0 or less uses 12)");
    TipPin(Component.Params.Input, "round", "Radius rounds up to a multiple of this (0 or less uses 1)");
    TipPin(Component.Params.Output, "angle", "Smallest angle between two members, degrees.");
    TipPin(Component.Params.Output, "length", "Node length: dia / sin(angle / 2) + thk.");
    TipPin(Component.Params.Output, "radius", "length rounded up to round.");
    TipPin(Component.Params.Output, "refpoint", "The node point.");
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
