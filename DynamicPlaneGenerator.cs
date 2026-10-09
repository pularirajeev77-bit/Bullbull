/*
  Component : Dynamic Plane Generator  (nickname: CamPlane)
  Author    : Rajeev Pulari
  Version   : 2.1  (2026-10-09)
  Platform  : Rhino 8 | Grasshopper C# Script

  Purpose
  -------
  Makes a plane at each point that faces the active viewport's camera
  (X = screen right, Y = screen up, normal toward you) - for text tags,
  icons or symbols that should always face the viewer. With AutoRefresh on,
  the planes follow the camera as you orbit.

  Inputs
  ------
  Points       (list) Points to place planes at (list or tree - one branch at a time).
  AutoRefresh  (item) True = follow the camera while you orbit / pan / zoom.

  Outputs
  -------
  Planes       One camera-facing plane per point, same order as Points.

  v2.1 changes
  ------------
  - Wrong / missing pin names and tooltips: they were set by pin POSITION, and
    Rhino 8 script components can have an extra "out" pin first, so the
    "Planes" tooltip landed on the wrong pin. It also overwrote each pin's Name
    (the script variable). Pins are now found by Name and only NickName and
    Description are set.
  - AutoRefresh re-solved 10 times a second even with the camera standing still
    (constant CPU). It now re-solves only when the camera actually moves or the
    active view changes.
  - No points silently gave one plane at the world origin; now a warning and no
    output.
  - Points input is a typed list (List<Point3d>) instead of a hand-converted
    object - lists and trees work natively, tree structure is kept.
*/

using System;
using System.Collections.Generic;

using Rhino;
using Rhino.Geometry;
using Rhino.Display;

using Grasshopper;
using Grasshopper.Kernel;

public class Script_Instance : GH_ScriptInstance
{
  // One Idle handler and one last-seen camera state per component instance
  private static readonly Dictionary<Guid, EventHandler> IdleHandlers = new Dictionary<Guid, EventHandler>();
  private static readonly Dictionary<Guid, string> LastCamera = new Dictionary<Guid, string>();
  private static readonly Dictionary<Guid, DateTime> LastTick = new Dictionary<Guid, DateTime>();

  private bool _metaSet = false;

  private void RunScript(List<Point3d> Points, bool AutoRefresh, ref object Planes)
  {
    SetMetadata();
    Component.Message = AutoRefresh ? "Auto-Refreshing" : "Static";

    // --- Points ---
    var pts = new List<Point3d>();
    if (Points != null)
      foreach (Point3d p in Points)
        if (p.IsValid) pts.Add(p);

    // --- Active view ---
    RhinoView view = RhinoDoc.ActiveDoc != null ? RhinoDoc.ActiveDoc.Views.ActiveView : null;
    if (view == null)
    {
      Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No active Rhino view.");
      ManageIdle(false);
      return;
    }

    // Remember the camera we solved for, so Idle only re-solves when it changes
    LastCamera[Component.InstanceGuid] = CameraKey(view);

    if (pts.Count == 0)
    {
      Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Points: connect at least one valid point.");
      ManageIdle(AutoRefresh);
      return;
    }

    // --- Camera-facing planes ---
    var planeList = new List<Plane>(pts.Count);
    foreach (Point3d pt in pts)
      planeList.Add(CreateCameraFacingPlane(pt, view));
    Planes = planeList;

    ManageIdle(AutoRefresh);
  }

  // Plane at pt with X = screen right, Y = screen up, normal toward the viewer.
  private Plane CreateCameraFacingPlane(Point3d pt, RhinoView view)
  {
    var vp = view.ActiveViewport;
    Vector3d n = vp.CameraDirection;
    n.Unitize();

    Vector3d x = Vector3d.CrossProduct(n, vp.CameraUp);
    if (!x.IsValid || x.IsTiny())
      x = Vector3d.CrossProduct(n, Vector3d.ZAxis);
    if (!x.IsValid || x.IsTiny())
      x = Vector3d.XAxis;
    x.Unitize();

    Vector3d y = Vector3d.CrossProduct(x, n);
    y.Unitize();

    return new Plane(pt, x, y);
  }

  // Compact signature of the active camera (view, location, direction, up)
  private static string CameraKey(RhinoView view)
  {
    var vp = view.ActiveViewport;
    Point3d c = vp.CameraLocation;
    Vector3d d = vp.CameraDirection;
    Vector3d u = vp.CameraUp;
    return vp.Id + "|" +
           c.X.ToString("R") + "," + c.Y.ToString("R") + "," + c.Z.ToString("R") + "|" +
           d.X.ToString("R") + "," + d.Y.ToString("R") + "," + d.Z.ToString("R") + "|" +
           u.X.ToString("R") + "," + u.Y.ToString("R") + "," + u.Z.ToString("R");
  }

  // Register / unregister RhinoApp.Idle for this component.
  private void ManageIdle(bool enable)
  {
    if (Component == null) return;
    Guid id = Component.InstanceGuid;
    IGH_Component comp = Component;

    if (enable)
    {
      if (IdleHandlers.ContainsKey(id)) return;

      EventHandler h = null;
      h = (sender, args) =>
      {
        try
        {
          // Self-detach if the component was deleted or its document closed
          if (comp.OnPingDocument() == null)
          {
            RhinoApp.Idle -= h;
            IdleHandlers.Remove(id);
            LastTick.Remove(id);
            LastCamera.Remove(id);
            return;
          }

          // Check at most ~10 times a second
          DateTime last;
          if (LastTick.TryGetValue(id, out last) && (DateTime.UtcNow - last).TotalMilliseconds < 100) return;
          LastTick[id] = DateTime.UtcNow;

          // Re-solve only when the camera actually changed
          RhinoView v = RhinoDoc.ActiveDoc != null ? RhinoDoc.ActiveDoc.Views.ActiveView : null;
          if (v == null) return;
          string key = CameraKey(v);
          string seen;
          if (LastCamera.TryGetValue(id, out seen) && seen == key) return;
          LastCamera[id] = key;

          comp.ExpireSolution(true);
        }
        catch { /* never let a background tick throw */ }
      };

      RhinoApp.Idle += h;
      IdleHandlers[id] = h;
      LastTick[id] = DateTime.UtcNow;
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

  // ---------------------------------------------------------------- metadata

  private void SetMetadata()
  {
    if (_metaSet) return;
    _metaSet = true;

    Component.Name = "Dynamic Plane Generator";
    Component.NickName = "CamPlane";
    Component.Description =
      "Planes at points that face the active viewport's camera (X = screen right, Y = screen up). " +
      "AutoRefresh keeps them facing you while you orbit. Rajeev Pulari, v2.1.";

    SetTip(Component.Params.Input, "Points",
      "Points to place planes at (list or tree). One plane per point, same order.");
    SetTip(Component.Params.Input, "AutoRefresh",
      "True = planes follow the camera as you orbit/pan/zoom (re-solves only when the camera moves). False = fixed.");
    SetTip(Component.Params.Output, "Planes",
      "Camera-facing planes: X = screen right, Y = screen up, normal toward the viewer.");
  }

  // Match pins by Name (the script variable), fall back to NickName.
  // Only NickName/Description are changed - never Name.
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
