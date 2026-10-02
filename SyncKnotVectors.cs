#region Metadata
/*
  Platform    : Rhino 8 | Grasshopper C#
  Component   : SyncKnotVectors
  NickName    : SyncK
  Message     : SyncK v2.1 (Pro)
  Description : Copies the knot vectors (parameterisation) of a base surface onto
                a target surface with the same NURBS structure, so both share the
                same U/V spacing - e.g. before blending, morphing or matching
                surfaces point-for-point. The target's control points are kept.

  Inputs:
    BaseSurface   : Surface (Item) - surface whose knots are copied
    TargetSurface : Surface (Item) - surface that receives them (same degree and CV counts)

  Output:
    SyncedSurface : the target with the base surface's knot vectors
*/
#endregion

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
        Surface BaseSurface,
        Surface TargetSurface,
        ref object SyncedSurface)
    {
        // Metadata + pin tooltips (once)
        if (Component != null && Component.Name != "SyncKnotVectors")
        {
            Component.Name = "SyncKnotVectors";
            Component.NickName = "SyncK";
            Component.Message = "SyncK v2.1 (Pro)";
            Component.Description = "Transfers the knot vectors (parameterisation) of a base surface to a structurally identical target surface.";

            SetTip(Component.Params.Input, "BaseSurface", "Surface whose knot vectors are copied.");
            SetTip(Component.Params.Input, "TargetSurface", "Surface that receives them - must have the same degrees and control point counts.");
            SetTip(Component.Params.Output, "SyncedSurface", "Target surface with the base surface's knot vectors.");
        }

        SyncedSurface = null;

        if (BaseSurface == null || TargetSurface == null)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Missing surface inputs.");
            return;
        }

        NurbsSurface nBase = BaseSurface.ToNurbsSurface();
        NurbsSurface nTarget = TargetSurface.ToNurbsSurface();
        if (nBase == null || nTarget == null)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Could not convert the surfaces to NURBS.");
            return;
        }

        // Same knot COUNT is not enough: a degree-2 surface with 5 CVs and a degree-3
        // surface with 4 CVs both have 6 knots, but copying knots between them
        // gives a wrong surface. Degrees and CV counts must match too.
        string mismatch = "";
        if (nBase.Degree(0) != nTarget.Degree(0) || nBase.Degree(1) != nTarget.Degree(1))
            mismatch += " Degree: base U" + nBase.Degree(0) + " V" + nBase.Degree(1) +
                        " vs target U" + nTarget.Degree(0) + " V" + nTarget.Degree(1) + ".";
        if (nBase.Points.CountU != nTarget.Points.CountU || nBase.Points.CountV != nTarget.Points.CountV)
            mismatch += " Control points: base " + nBase.Points.CountU + "x" + nBase.Points.CountV +
                        " vs target " + nTarget.Points.CountU + "x" + nTarget.Points.CountV + ".";
        if (mismatch.Length > 0)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Structure mismatch." + mismatch +
                " Rebuild one surface to match the other first.");
            return;
        }

        for (int i = 0; i < nTarget.KnotsU.Count; i++) nTarget.KnotsU[i] = nBase.KnotsU[i];
        for (int j = 0; j < nTarget.KnotsV.Count; j++) nTarget.KnotsV[j] = nBase.KnotsV[j];

        if (!nTarget.IsValid)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "The result is not a valid surface.");
            return;
        }

        SyncedSurface = nTarget;
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
}
