/*
  Author: Rajeev Pulari + Gemini
  Rhino 8 | Grasshopper C#
  Version: 2025.11.11
  Component: Dynamic Plane Generator
  Description:
    Creates camera-facing planes at given points.
    Auto-refreshes while AutoRefresh = true using RhinoApp.Idle.
*/

using System;
using System.Collections;
using System.Collections.Generic;

using Rhino;
using Rhino.Geometry;
using Rhino.Display;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

public class Script_Instance : GH_ScriptInstance
{
  // Track one Idle handler per component, plus a simple throttle
  private static readonly Dictionary<Guid, EventHandler> IdleHandlers = new Dictionary<Guid, EventHandler>();
  private static readonly Dictionary<Guid, DateTime> LastTick = new Dictionary<Guid, DateTime>();

  private void RunScript(object Points, bool AutoRefresh, ref object Planes)
  {
    // --- Metadata + pin tooltips (once) ---
    if (this.Component != null && this.Component.Name != "Dynamic Plane Generator")
    {
      this.Component.Name = "Dynamic Plane Generator";
      this.Component.NickName = "CamPlane";
      this.Component.Description = "Makes planes at points that face the active viewport's camera. Can auto-refresh as you orbit.";

      SetTip(this.Component.Params.Input, 0, "Points",
        "Point(s) to place planes at (item, list or tree).");
      SetTip(this.Component.Params.Input, 1, "AutoRefresh",
        "True = re-solve continuously so the planes keep facing the camera as you orbit. Uses CPU; turn off when idle.");
      SetTip(this.Component.Params.Output, 0, "Planes",
        "One camera-facing plane per input point.");
    }
    if (this.Component != null)
      this.Component.Message = AutoRefresh ? "Auto-Refreshing" : "Static";

    // --- Gather points (item/list/tree), robust to GH_Point wrappers ---
    // The old To<Point3d> only matched a raw Point3d, so a single point wired
    // as an item (which arrives as GH_Point) fell through and the plane was
    // silently placed at the world origin. GH_Convert handles every case.
    var pts = GetPoints(Points);
    if (pts.Count == 0) pts.Add(Point3d.Origin);

    // --- Active view ---
    RhinoView view = RhinoDoc.ActiveDoc?.Views?.ActiveView;
    if (view == null)
    {
      Planes = new List<Plane> { Plane.WorldXY };
      ManageIdle(false); // ensure no handler left attached
      return;
    }

    // --- Build planes facing camera ---
    var planeList = new List<Plane>();
    foreach (var pt in pts)
      planeList.Add(CreateCameraFacingPlane(pt, view));

    Planes = planeList;

    // --- Manage idle-based auto-refresh ---
    ManageIdle(AutoRefresh);
  }

  // Register/unregister RhinoApp.Idle for this component, with throttle
  private void ManageIdle(bool enable)
  {
    if (Component == null) return;
    Guid id = Component.InstanceGuid;

    if (enable)
    {
      if (!IdleHandlers.ContainsKey(id))
      {
        EventHandler h = null;
        h = (sender, args) =>
        {
          try
          {
            // Self-detach if the component was deleted or its document closed,
            // so a removed component can't keep re-solving forever (the old
            // version never unregistered on delete).
            if (Component == null || Component.OnPingDocument() == null)
            {
              RhinoApp.Idle -= h;
              IdleHandlers.Remove(id);
              LastTick.Remove(id);
              return;
            }

            // throttle to ~10 Hz (100ms) to avoid CPU spikes
            DateTime last;
            if (!LastTick.TryGetValue(id, out last) || (DateTime.UtcNow - last).TotalMilliseconds > 100)
            {
              LastTick[id] = DateTime.UtcNow;
              Component.ExpireSolution(true);
            }
          }
          catch { /* swallow background errors */ }
        };

        RhinoApp.Idle += h;
        IdleHandlers[id] = h;
        LastTick[id] = DateTime.UtcNow;
      }
    }
    else
    {
      EventHandler h;
      if (IdleHandlers.TryGetValue(id, out h))
      {
        try { RhinoApp.Idle -= h; } catch { }
        IdleHandlers.Remove(id);
      }
      LastTick.Remove(id);
    }
  }

  // Create a plane at pt whose normal is aligned with the camera direction,
  // so its XY lies flat in the screen (e.g. for text tags that always face you)
  private Plane CreateCameraFacingPlane(Point3d pt, RhinoView view)
  {
    var vp = view.ActiveViewport;
    Vector3d camDir = vp.CameraDirection;
    Vector3d up = vp.CameraUp;

    Plane pln = new Plane(pt, camDir);

    // Build an orthonormal basis using the view "up" as reference
    Vector3d x = Vector3d.CrossProduct(pln.Normal, up);
    if (!x.IsValid || x.IsTiny())
      x = Vector3d.CrossProduct(pln.Normal, Vector3d.ZAxis);

    x.Unitize();
    Vector3d y = Vector3d.CrossProduct(x, pln.Normal);
    y.Unitize();

    return new Plane(pt, x, y);
  }

  // Convert a GH input (item/list/tree) into a list of Point3d, robustly
  private List<Point3d> GetPoints(object input)
  {
    var list = new List<Point3d>();
    if (input == null) return list;

    if (input is IEnumerable enumerable && !(input is string))
    {
      foreach (var item in enumerable)
      {
        Point3d p = Point3d.Unset;
        if (GH_Convert.ToPoint3d(item, ref p, GH_Conversion.Both) && p.IsValid)
          list.Add(p);
      }
      return list;
    }

    // Single item (raw Point3d or a GH_Point wrapper)
    Point3d single = Point3d.Unset;
    if (GH_Convert.ToPoint3d(input, ref single, GH_Conversion.Both) && single.IsValid)
      list.Add(single);

    return list;
  }

  private void SetTip(System.Collections.Generic.IList<IGH_Param> ps, int i, string name, string tip)
  {
    if (ps == null || i < 0 || i >= ps.Count) return;
    ps[i].Name = name;
    ps[i].NickName = name;
    ps[i].Description = tip;
  }
}
