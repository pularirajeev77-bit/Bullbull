/*
  Author: Rajeev Pulari + ChatGPT
  Rhino 8 | Grasshopper C#
  Version: 2026.04.05
  Component: RemoveHoles
  Description:
    Removes inner loops (holes) from Brep faces, chosen by All / Diameter /
    Length / Index. Uses warning messages for empty inputs to keep the
    canvas clean. Preserves the input tree structure.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using Rhino;
using Rhino.Geometry;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;

public class Script_Instance : GH_ScriptInstance
{
    private void RunScript(
		DataTree<Brep> Brep,
		bool AllHoles,
		List<double> TargetDiameters,
		List<double> TargetLengths,
		List<int> HoleIndices,
		double Tolerance,
		ref object Result)
    {
        // 1. Set Metadata + pin tooltips (once)
        if (this.Component != null && this.Component.Message != "Remove Holes v2.0")
        {
            this.Component.Message = "Remove Holes v2.0";
            this.Component.NickName = "RemHole";
            this.Component.Name = "Remove Holes";
            this.Component.Description =
                "Removes inner loops (holes) from Brep faces, chosen by All / Diameter / Length / Index.";

            SetTip(this.Component.Params.Input, 0, "Brep",
                "Breps to remove holes from (tree; structure is preserved).");
            SetTip(this.Component.Params.Input, 1, "AllHoles",
                "True = remove every hole; ignores the Dia/Len/Idx inputs.");
            SetTip(this.Component.Params.Input, 2, "TargetDiameters",
                "Remove holes whose diameter matches one of these (within Tolerance). Diameter = loop length / Pi.");
            SetTip(this.Component.Params.Input, 3, "TargetLengths",
                "Remove holes whose loop length (perimeter) matches one of these (within Tolerance).");
            SetTip(this.Component.Params.Input, 4, "HoleIndices",
                "Remove holes by index (0-based, per Brep, in the order the holes are found).");
            SetTip(this.Component.Params.Input, 5, "Tolerance",
                "Match/geometry tolerance. 0 or less uses the model tolerance.");
            SetTip(this.Component.Params.Output, 0, "Result",
                "The Breps with matching holes removed (same tree structure as input).");
        }

        // 2. Initialize Output
        DataTree<Brep> outTree = new DataTree<Brep>();
        double tol = (Tolerance > 0) ? Tolerance : (RhinoDoc.ActiveDoc?.ModelAbsoluteTolerance ?? 0.01);

        try
        {
            // 3. Warning logic (no hard errors)
            if (Brep == null || Brep.DataCount == 0)
            {
                this.Component?.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    "Input 'Brep' is empty or null. Please provide geometry.");
                Result = outTree;
                return;
            }

            bool hasCriteria = AllHoles ||
                              (TargetDiameters != null && TargetDiameters.Count > 0) ||
                              (TargetLengths != null && TargetLengths.Count > 0) ||
                              (HoleIndices != null && HoleIndices.Count > 0);

            if (!hasCriteria)
            {
                this.Component?.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    "No removal criteria provided (All/Dia/Len/Idx). Outputting original Breps.");
                Result = Brep; // Pass through instead of failing
                return;
            }

            // 4. Process Tree
            foreach (GH_Path path in Brep.Paths)
            {
                foreach (Brep b in Brep.Branch(path))
                {
                    if (b == null)
                    {
                        // Keep the slot so output stays aligned with input
                        outTree.Add(null, path);
                        continue;
                    }

                    Brep cleaned = ProcessBrep(b, TargetDiameters, TargetLengths, HoleIndices, tol, AllHoles);
                    outTree.Add(cleaned, path);
                }
            }
        }
        catch (Exception ex)
        {
            // Red balloon on the component, not only the output window
            this.Component?.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Logic Error: " + ex.Message);
        }

        Result = outTree;
    }

    // --------------------------------------------------------------------------
    // Logic Core
    // --------------------------------------------------------------------------

    private Brep ProcessBrep(Brep brep, List<double> diameters, List<double> lengths, List<int> indices, double tol, bool allHoles)
    {
        Brep dup = brep.DuplicateBrep();
        var innerLoops = new List<BrepLoop>();

        foreach (var face in dup.Faces)
            foreach (var loop in face.Loops)
                if (loop.LoopType == BrepLoopType.Inner)
                    innerLoops.Add(loop);

        if (innerLoops.Count == 0) return dup;

        var toRemove = new List<ComponentIndex>();

        if (allHoles)
        {
            foreach (var lp in innerLoops) toRemove.Add(lp.ComponentIndex());
        }
        else
        {
            // 1) Indices
            if (indices != null)
            {
                foreach (int idx in indices)
                {
                    // Negative index counts from the end (-1 = last hole)
                    int actual = idx < 0 ? idx + innerLoops.Count : idx;
                    if (actual >= 0 && actual < innerLoops.Count)
                        toRemove.Add(innerLoops[actual].ComponentIndex());
                }
            }

            // 2) Geometry Properties
            bool checkLen = (lengths != null && lengths.Count > 0);
            bool checkDia = (diameters != null && diameters.Count > 0);

            if (checkLen || checkDia)
            {
                foreach (var lp in innerLoops)
                {
                    Curve crv = lp.To3dCurve();
                    if (crv == null) continue;   // guard: To3dCurve can return null

                    double L = crv.GetLength();
                    double D = L / Math.PI;      // diameter, treating the hole as circular

                    if ((checkLen && lengths.Any(t => Math.Abs(L - t) <= tol)) ||
                        (checkDia && diameters.Any(t => Math.Abs(D - t) <= tol)))
                    {
                        toRemove.Add(lp.ComponentIndex());
                    }
                }
            }
        }

        // Distinct check to prevent duplicate removal attempts
        var uniqueIndices = toRemove.Distinct().ToList();

        if (uniqueIndices.Count > 0)
        {
            Brep result = dup.RemoveHoles(uniqueIndices, tol);
            return result ?? dup;
        }

        return dup;
    }

    // Sets the name, nickname and hover tooltip of one input/output pin,
    // guarded by index in case the component has fewer params than expected.
    private void SetTip(System.Collections.Generic.IList<IGH_Param> ps, int i, string name, string tip)
    {
        if (ps == null || i < 0 || i >= ps.Count) return;
        ps[i].Name = name;
        ps[i].NickName = name;
        ps[i].Description = tip;
    }
}
