/*
  Polyline Frame Generator v2.0
  Converts any polyline or list of curves into:
  - Vertex points
  - Bisector vectors
  - Planes at each vertex

  Author: Rajeev Pulari + ChatGPT
  Rhino 8 | Grasshopper C#
  Version: 2025.11.04
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Rhino;
using Rhino.Geometry;
using Grasshopper;
using Grasshopper.Kernel;

public class Script_Instance : GH_ScriptInstance
{
  // --- Metadata ---
  public override void AfterRunScript()
  {
    if (Component != null)
    {
      Component.Message = "Bisect Frame v2.0";
      Component.NickName = "Bisect Frame";
    }
  }

  // --- Helper Methods ---
  Vector3d Normalize(Vector3d v)
  {
    if (!v.IsValid) return Vector3d.Zero;
    if (!v.Unitize()) return Vector3d.Zero;
    return v;
  }

  Vector3d VectorBetween(Point3d a, Point3d b)
  {
    return b - a;
  }

  // Unit vector perpendicular to `tan`, on its left when looking down `up`.
  // Used for end vertices and straight (collinear) vertices, so both follow
  // the same side convention. If tan is parallel to up (e.g. a vertical
  // line), falls back to any perpendicular instead of a zero vector.
  Vector3d SideVector(Vector3d up, Vector3d tan)
  {
    Vector3d side = Vector3d.CrossProduct(up, tan);
    if (side.IsTiny() || !side.Unitize())
    {
      side = Vector3d.Zero;
      side.PerpendicularTo(tan);
      if (!side.Unitize()) side = Vector3d.XAxis;
    }
    return side;
  }

  Plane SafePlaneFromFrame(Point3d origin, Vector3d x_axis, Vector3d y_axis, Vector3d up_hint)
  {
    Vector3d x = x_axis.IsValid ? Normalize(x_axis) : Vector3d.XAxis;
    Vector3d y = y_axis.IsValid ? Normalize(y_axis) : Vector3d.YAxis;

    if (x.IsTiny() || !x.Unitize()) x = Vector3d.XAxis;

    if (y.IsTiny() || !y.Unitize() || x.IsParallelTo(y, RhinoMath.ZeroTolerance) != 0)
    {
      Vector3d z_axis = up_hint.IsValid ? up_hint : Vector3d.ZAxis;
      if (z_axis.IsTiny()) z_axis = Vector3d.ZAxis;

      y = Vector3d.CrossProduct(z_axis, x);
      // Was new Vector3d(-x.Y, x.X, x.Z): for a vertical x that is
      // parallel to x itself and produced an invalid plane
      if (y.IsTiny()) y.PerpendicularTo(x);
    }

    x.Unitize();
    y.Unitize();

    Vector3d z_cross = Vector3d.CrossProduct(x, y);
    if (z_cross.IsTiny() || !z_cross.Unitize()) z_cross = Vector3d.ZAxis;

    y = Vector3d.CrossProduct(z_cross, x);
    y.Unitize();

    return new Plane(origin, x, y);
  }

  Vector3d FitUpHint(IEnumerable<Point3d> pts)
  {
    if (pts != null && pts.Count() >= 3)
    {
      Plane pl;
      if (Plane.FitPlaneToPoints(pts, out pl) == PlaneFitResult.Success)
      {
        Vector3d n = pl.Normal;
        if (n.Unitize())
        {
          // A fitted plane's normal sign is arbitrary: a flat polyline in
          // XY could get -Z and flip every frame. Point it upward.
          if (n * Vector3d.ZAxis < 0) n.Reverse();
          return n;
        }
      }
    }
    return Vector3d.ZAxis;
  }

  bool GetPolylineVertices(Curve crv, out List<Point3d> vertices, out bool isClosed)
  {
    vertices = new List<Point3d>();
    isClosed = false;

    if (crv == null) return false;

    Polyline pline;
    if (crv.TryGetPolyline(out pline))
    {
      vertices = new List<Point3d>(pline);
      isClosed = crv.IsClosed;

      if (vertices.Count >= 2 && vertices[0].DistanceTo(vertices.Last()) <= RhinoMath.ZeroTolerance)
        vertices.RemoveAt(vertices.Count - 1);

      return vertices.Count >= 2;
    }
    return false;
  }

  void ProcessPolyline(Curve crv, out List<Point3d> points, out List<Vector3d> bisectors, out List<Plane> planes)
  {
    points = new List<Point3d>();
    bisectors = new List<Vector3d>();
    planes = new List<Plane>();

    List<Point3d> verts;
    bool is_closed;

    if (!GetPolylineVertices(crv, out verts, out is_closed)) return;
    if (verts.Count < 2) return;
    if (verts.Count == 2 && is_closed) is_closed = false;

    Vector3d up_hint = FitUpHint(verts);
    int n = verts.Count;

    for (int i = 0; i < n; i++)
    {
      Point3d p_curr = verts[i];
      Vector3d bis;
      Vector3d normal;
      bool is_end = !is_closed && (i == 0 || i == n - 1);

      if (is_end)
      {
        // End vertex of an open polyline: frame from the end segment.
        // Previously used tan x up (right side) while straight interior
        // vertices used up x tan (left side), so the end frames pointed
        // the opposite way on a straight run. Both now use SideVector.
        Vector3d tan = (i == 0)
          ? Normalize(VectorBetween(verts[0], verts[1]))
          : Normalize(VectorBetween(verts[n - 2], verts[n - 1]));
        if (tan.IsTiny()) tan = Vector3d.XAxis;

        bis = SideVector(up_hint, tan);
        normal = Normalize(Vector3d.CrossProduct(tan, bis));
      }
      else
      {
        // Interior vertex (or any vertex of a closed polyline)
        int i_prev = (i + n - 1) % n;
        int i_next = (i + 1) % n;

        Vector3d v1_hat = Normalize(VectorBetween(verts[i_prev], p_curr));
        Vector3d v2_hat = Normalize(VectorBetween(p_curr, verts[i_next]));

        bis = Normalize(-v1_hat + v2_hat);
        normal = Normalize(Vector3d.CrossProduct(-v1_hat, v2_hat));

        if (bis.IsTiny())
        {
          // Straight vertex: no angle to bisect, use the side vector
          Vector3d tan = Normalize(v1_hat + v2_hat);
          if (tan.IsTiny()) tan = Vector3d.XAxis;
          bis = SideVector(up_hint, tan);
          normal = Normalize(Vector3d.CrossProduct(tan, bis));
        }
      }

      // The corner normal's sign depends on the turn direction (left vs
      // right turn), and open vs closed polylines used opposite cross
      // product orders -- so frames flipped upside down from vertex to
      // vertex. Orient every normal toward the up hint.
      if (normal * up_hint < 0) normal = -normal;

      Plane plane = SafePlaneFromFrame(p_curr, bis, normal, up_hint);
      points.Add(p_curr);
      bisectors.Add(bis);
      planes.Add(plane);
    }
  }

  // --- Main Grasshopper Entry ---
  // Crv is item access: Grasshopper runs this once per curve and outputs
  // one branch per curve. (The old "Crv is IList" branch could never run,
  // since Crv is typed Curve.)
  private void RunScript(Curve Crv, ref object Pt, ref object Vec, ref object Pln)
  {
    List<Point3d> pts;
    List<Vector3d> vecs;
    List<Plane> plns;

    // Null or non-polyline curve gives empty lists
    ProcessPolyline(Crv, out pts, out vecs, out plns);

    Pt = pts;
    Vec = vecs;
    Pln = plns;
  }
}
