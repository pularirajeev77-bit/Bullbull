/*
  Surface Extender v2.1
  Rhino 8 | Grasshopper C#
  Extends one or more surfaces (or every face of a Brep) by a distance on each
  side: North (v max), East (u max), South (v min), West (u min).
  Optionally outputs the frame at the centre of each original surface.
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
    // Surfaces : list access | North, East, South, West, CenterPlanes : item access
    private void RunScript(
        List<object> Surfaces,
        double North,
        double East,
        double South,
        double West,
        bool CenterPlanes,
        ref object Extended,
        ref object Planes)
    {
        // Metadata + pin tooltips (once)
        if (Component != null && Component.Name != "Surface Extender")
        {
            Component.Name = "Surface Extender";
            Component.NickName = "SrfExt";
            Component.Message = "Surface Extender v2.1";
            Component.Description = "Extends surfaces (or Brep faces) by a distance on each side: North, East, South, West.";

            var pi = Component.Params.Input;
            SetTip(pi, "Surfaces", "Surfaces or Breps (each face is extended). List access.");
            SetTip(pi, "North", "Extension at the v-max edge. 0 = none.");
            SetTip(pi, "East", "Extension at the u-max edge. 0 = none.");
            SetTip(pi, "South", "Extension at the v-min edge. 0 = none.");
            SetTip(pi, "West", "Extension at the u-min edge. 0 = none.");
            SetTip(pi, "CenterPlanes", "True = also output the frame at the centre of each original surface.");
            var po = Component.Params.Output;
            SetTip(po, "Extended", "Extended surfaces as Breps (one per input surface / face).");
            SetTip(po, "Planes", "Centre frames of the original surfaces (only when CenterPlanes is True).");
        }

        var surfOut = new List<Brep>();
        var planeOut = new List<Plane>();
        Extended = surfOut;
        Planes = planeOut;

        // 1. Collect surfaces (unwrap GH types; Breps/extrusions give one surface per face)
        var srfs = new List<Surface>();
        int skipped = 0;
        if (Surfaces != null)
        {
            foreach (var raw in Surfaces)
            {
                object item = (raw is IGH_Goo) ? ((IGH_Goo)raw).ScriptVariable() : raw;
                if (item is Surface) srfs.Add((Surface)item);
                else if (item is Brep) foreach (BrepFace f in ((Brep)item).Faces) srfs.Add(f.UnderlyingSurface());
                else if (item is Extrusion)
                {
                    Brep b = ((Extrusion)item).ToBrep();
                    if (b != null) foreach (BrepFace f in b.Faces) srfs.Add(f.UnderlyingSurface());
                }
                else if (item != null) skipped++;
            }
        }
        if (skipped > 0)
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, skipped + " input item(s) are not surfaces and were skipped.");
        if (srfs.Count == 0)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "No surfaces to extend.");
            return;
        }
        if (North < 0 || East < 0 || South < 0 || West < 0)
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Negative distances cannot extend; those sides are left as they are.");

        // 2. Extend each surface independently (a failure on one does not stop the rest)
        int failedSides = 0;
        for (int i = 0; i < srfs.Count; i++)
        {
            Surface srf = srfs[i];
            if (srf == null) continue;
            try
            {
                Surface dup = srf.Duplicate() as Surface;
                if (dup == null) continue;

                if (CenterPlanes)
                {
                    Plane pl;
                    if (dup.FrameAt(dup.Domain(0).Mid, dup.Domain(1).Mid, out pl)) planeOut.Add(pl);
                }

                dup = ExtendSide(dup, IsoStatus.North, North, ref failedSides);
                dup = ExtendSide(dup, IsoStatus.East, East, ref failedSides);
                dup = ExtendSide(dup, IsoStatus.South, South, ref failedSides);
                dup = ExtendSide(dup, IsoStatus.West, West, ref failedSides);

                Brep result = dup.ToBrep();
                if (result != null) surfOut.Add(result);
            }
            catch (Exception ex)
            {
                Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Surface " + i + ": " + ex.Message);
            }
        }

        if (failedSides > 0)
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                failedSides + " side extension(s) failed (e.g. closed/periodic direction); those sides were left unextended.");
    }

    // Extends one side; on failure keeps the surface as it was instead of losing it
    private Surface ExtendSide(Surface srf, IsoStatus side, double dist, ref int failed)
    {
        if (dist <= 0) return srf;
        Surface ext = srf.Extend(side, dist, true);
        if (ext == null) { failed++; return srf; }
        return ext;
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
