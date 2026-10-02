#region Metadata
/*
  Author      : Rajeev Pulari
  Platform    : Rhino 8 | Grasshopper C#
  Version     : 2026.10.02
  Component   : Dynamic Section Split
  NickName    : DynSplit
  Message     : Section & Split v2.1
  Description : Live section tool. A cutting plane slides over the geometry's
                bounding box (Location X/Y as 0-1 sliders, e.g. an MD Slider) and
                rotates about that point (Rotation X/Y/Z in multiples of pi). It
                outputs the section curves and the geometry kept on one side of
                the plane - capped where possible, so it reads as a solid cut.

  Inputs:
    Geometry : List<GeometryBase> (List) - Breps, Extrusions, Surfaces or Meshes
    Location : Point3d (Item) - X, Y in 0..1 across the bounding box (Z ignored; plane at box centre height)
    Rotation : Point3d (Item) - rotation about world X, Y, Z in multiples of pi (0.5 = 90 deg)
    KeepSide : int     (Item) - 0 (even) = keep the side BEHIND the plane normal, 1 (odd) = in FRONT

  Outputs:
    Sections      : section curves
    SplitGeometry : the kept part of each object (whole object if the plane misses it)
    CutPlane      : the cutting plane
*/
#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Rhino;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

public class Script_Instance : GH_ScriptInstance
{
    // Geometry : list | Location, Rotation, KeepSide : item
    private void RunScript(
        List<GeometryBase> Geometry,
        Point3d Location,
        Point3d Rotation,
        int KeepSide,
        ref object Sections,
        ref object SplitGeometry,
        ref object CutPlane)
    {
        // Metadata + pin tooltips (once)
        if (Component != null && Component.Name != "Dynamic Section Split")
        {
            Component.Name = "Dynamic Section Split";
            Component.NickName = "DynSplit";
            Component.Message = "Section & Split v2.1";
            Component.Description = "Slides and rotates a cutting plane over the geometry; outputs section curves and the kept side.";

            var pi = Component.Params.Input;
            SetTip(pi, "Geometry", "Breps, Extrusions, Surfaces or Meshes. List access.");
            SetTip(pi, "Location", "X, Y from 0 to 1 across the bounding box (MD Slider). Z is ignored - the plane sits at the box centre height.");
            SetTip(pi, "Rotation", "Rotation about world X, Y, Z in multiples of pi (0.5 = 90 deg). 0,0,0 = horizontal plane.");
            SetTip(pi, "KeepSide", "0 = keep the side behind the plane normal, 1 = in front (any even / odd number works).");
            var po = Component.Params.Output;
            SetTip(po, "Sections", "Section curves.");
            SetTip(po, "SplitGeometry", "Kept part of each object (capped where possible); the whole object if the plane misses it.");
            SetTip(po, "CutPlane", "The cutting plane.");
        }

        if (Geometry == null || Geometry.Count(g => g != null) == 0)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Geometry list is empty. Please connect Breps or Meshes.");
            return;
        }

        RhinoDoc doc = RhinoDocument ?? RhinoDoc.ActiveDoc;
        double tol = (doc != null) ? doc.ModelAbsoluteTolerance : 0.001;

        // 1. Bounding box of everything
        BoundingBox bbox = BoundingBox.Empty;
        foreach (GeometryBase g in Geometry)
            if (g != null) bbox.Union(g.GetBoundingBox(true));
        if (!bbox.IsValid)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Could not compute a bounding box for the geometry.");
            return;
        }

        // 2. Plane origin from the 0-1 location across the box
        Point3d origin = new Point3d(
            bbox.Min.X + Location.X * (bbox.Max.X - bbox.Min.X),
            bbox.Min.Y + Location.Y * (bbox.Max.Y - bbox.Min.Y),
            bbox.Center.Z);

        // 3. Rotations in multiples of pi, about world axes through the origin
        Plane plane = new Plane(origin, Vector3d.ZAxis);
        plane.Rotate(Rotation.X * Math.PI, Vector3d.XAxis, origin);
        plane.Rotate(Rotation.Y * Math.PI, Vector3d.YAxis, origin);
        plane.Rotate(Rotation.Z * Math.PI, Vector3d.ZAxis, origin);

        // Cutter sized to the geometry (the fixed +-100000 was too small for large models)
        double size = Math.Max(1.0, bbox.Diagonal.Length * 2.0);
        Brep cutter = new PlaneSurface(plane, new Interval(-size, size), new Interval(-size, size)).ToBrep();

        bool keepFront = (Math.Abs(KeepSide) % 2) == 1;

        var outSections = new List<Curve>();
        var outKept = new List<GeometryBase>();
        int unsupported = 0;

        // 4. Each object
        foreach (GeometryBase g in Geometry)
        {
            if (g == null) continue;

            Brep brep = g as Brep;
            if (brep == null && g is Extrusion) brep = ((Extrusion)g).ToBrep();
            if (brep == null && g is Surface) brep = ((Surface)g).ToBrep();

            if (brep != null)
            {
                Curve[] crvs; Point3d[] pts;
                if (Intersection.BrepPlane(brep, plane, tol, out crvs, out pts) && crvs != null)
                    outSections.AddRange(crvs);

                Brep[] pieces = brep.Split(new Brep[] { cutter }, tol);
                if (pieces == null || pieces.Length < 2) { outKept.Add(brep); continue; }

                // Pieces come back in no fixed order: pick by SIDE of the plane, not by index
                foreach (Brep p in pieces)
                    if (OnKeptSide(p.GetBoundingBox(true).Center, plane, keepFront))
                    {
                        Brep capped = p.CapPlanarHoles(tol);
                        outKept.Add(capped ?? p);
                    }
                continue;
            }

            Mesh mesh = g as Mesh;
            if (mesh != null)
            {
                Polyline[] pls = Intersection.MeshPlane(mesh, plane);
                if (pls != null) foreach (Polyline pl in pls) outSections.Add(new PolylineCurve(pl));

                Mesh[] pieces = mesh.Split(plane);
                if (pieces == null || pieces.Length < 2) { outKept.Add(mesh); continue; }

                foreach (Mesh p in pieces)
                    if (OnKeptSide(p.GetBoundingBox(true).Center, plane, keepFront)) outKept.Add(p);
                continue;
            }

            unsupported++;
        }

        if (unsupported > 0)
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                unsupported + " object(s) of an unsupported type were skipped (use Breps, Extrusions, Surfaces or Meshes).");

        Component.Message = "Section & Split v2.1 | keep " + (keepFront ? "front" : "back");

        Sections = outSections;
        SplitGeometry = outKept;
        CutPlane = plane;
    }

    // <Custom additional code>
    private static bool OnKeptSide(Point3d pt, Plane plane, bool keepFront)
    {
        double d = plane.DistanceTo(pt);   // signed: + = in front of the normal
        return keepFront ? d > 0 : d <= 0;
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
