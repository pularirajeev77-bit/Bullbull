using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Geometry;
using Grasshopper.Kernel;

public class Script_Instance : GH_ScriptInstance
{
    // Professional Rhino 8 Signature
    // Inputs: plane (Plane), leader_length (double), parameter (double), landing_leg (double)
    // Outputs: plcrv (object), pts (object)
    private void RunScript(
		Plane plane,
		double leader_length,
		double parameter,
		double landing_leg,
		ref object plcrv,
		ref object pts)

    {
    SetPinTips();   // pin tooltips (set once, matched by name)

        // Set Component Metadata (Rhino 8 feature)
        Component.Message = "Leader Points";
        Component.NickName = "Leader Points";

        // Initialize outputs to prevent "unassigned" errors
        plcrv = null;
        pts = null;

        try
        {
            // 1. Core Geometry Logic
            double angle = parameter * 2.0 * Math.PI;

            // Compute coordinates on the circle relative to plane origin
            double x = leader_length * Math.Cos(angle);
            double y = leader_length * Math.Sin(angle);

            // 2. Determine Vector Direction
            // Landing leg points the same way the leader leans (left/right).
            // Based on the actual x coordinate instead of the old
            // "parameter > 0.25 && parameter < 0.75" test, which broke for
            // parameters outside 0..1: e.g. 1.4 draws the leader on the left
            // (same as 0.4) but the old test sent the landing leg right.
            double direction = (x < 0) ? -1.0 : 1.0;

            // 3. Create the Landing Leg Vector
            Vector3d vec = plane.XAxis;
            vec.Unitize();
            vec *= (landing_leg * direction);

            // 4. Construct the Point Sequence
            Point3d pt0 = plane.Origin;
            Point3d pt1 = plane.PointAt(x, y);
            Point3d pt2 = pt1 + vec;

            // 5. Generate the List Output
            var pointList = new List<Point3d> { pt0, pt1, pt2 };
            pts = pointList;

            // 6. Generate the Polyline Output
            // A zero leader_length or landing_leg makes two points coincide,
            // and a polyline with a zero-length segment is an invalid curve.
            // Drop the duplicate points; skip the curve if under 2 remain.
            double tol = RhinoDoc.ActiveDoc != null
                ? RhinoDoc.ActiveDoc.ModelAbsoluteTolerance
                : RhinoMath.ZeroTolerance;

            Polyline pl = new Polyline(pointList);
            pl.DeleteShortSegments(tol);

            if (pl.Count >= 2)
                plcrv = new PolylineCurve(pl);
        }
        catch (Exception ex)
        {
            // Show the error as a red balloon on the component, not only
            // in the output window where it's easy to miss
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                "Error in LeaderPoints: " + ex.Message);
        }
    }

  // ---------------------------------------------------------------- pin tooltips
  private bool _pinTipsSet = false;

  private void SetPinTips()
  {
    if (_pinTipsSet || Component == null) return;
    _pinTipsSet = true;
    TipPin(Component.Params.Input, "plane", "Where the leader starts, and its orientation.");
    TipPin(Component.Params.Input, "leader_length", "Distance from origin to the elbow.");
    TipPin(Component.Params.Input, "parameter", "Direction of the elbow, as a fraction of a full turn: 0 = plane X direction, 0.25 = plane Y, 0.5 = −X, 0.75 = −Y.");
    TipPin(Component.Params.Input, "landing_leg", "Length of the landing leg.");
    TipPin(Component.Params.Output, "plcrv", "The leader as one polyline.");
    TipPin(Component.Params.Output, "pts", "Its 3 points: origin, elbow, end of landing leg.");
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
