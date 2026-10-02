#region Metadata
/*
  Author      : Rajeev Pulari + Gemini
  Platform    : Rhino 8 | Grasshopper C#
  Version     : 2026.10.02
  Component   : Named Views Creator
  NickName    : ViewGen
  Message     : View v2.1
  Description : Zooms the active viewport to fit the given objects and saves that
                as a Rhino Named View (replacing a view of the same name). Clear
                deletes ALL named views in the document.

  Inputs:
    Objects : List<object> (List) - geometry, Rhino object ids or referenced objects
    Name    : string       (Item) - named view to create / update
    Run     : bool         (Item) - True = zoom and save the named view
    Clear   : bool         (Item) - True = delete ALL named views (takes priority)

  Outputs:
    Result  : string - what happened
    NameOut : string - the saved view name (null if nothing was saved)
*/
#endregion

using System;
using System.Collections;
using System.Collections.Generic;

using Rhino;
using Rhino.Geometry;
using Rhino.DocObjects;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

public class Script_Instance : GH_ScriptInstance
{
  // Objects : list access | Name, Run, Clear : item access
  private void RunScript(
    List<object> Objects,
    string Name,
    bool Run,
    bool Clear,
    ref object Result,
    ref object NameOut)
  {
    // Metadata + pin tooltips (once)
    if (Component != null && Component.Name != "Named Views Creator")
    {
      Component.Name = "Named Views Creator";
      Component.NickName = "ViewGen";
      Component.Message = "View v2.1";
      Component.Description = "Zooms the active viewport to the objects and saves it as a Named View. Clear deletes ALL named views.";

      var pi = Component.Params.Input;
      SetTip(pi, "Objects", "Geometry, Rhino object ids or referenced objects to frame. List access.");
      SetTip(pi, "Name", "Named view to create (an existing view with this name is replaced).");
      SetTip(pi, "Run", "True = zoom the active viewport to the objects and save the named view.");
      SetTip(pi, "Clear", "True = delete ALL named views in the document. Takes priority over Run.");
      SetTip(Component.Params.Output, "Result", "What happened.");
      SetTip(Component.Params.Output, "NameOut", "The saved named view name.");
    }

    Result = "Ready.";
    NameOut = null;

    RhinoDoc doc = RhinoDocument ?? RhinoDoc.ActiveDoc;
    if (doc == null)
    {
      Result = "No active Rhino document.";
      Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No active Rhino document.");
      return;
    }

    // 1. Clear ALL named views
    if (Clear)
    {
      int count = doc.NamedViews.Count;
      for (int i = count - 1; i >= 0; i--) doc.NamedViews.Delete(i);
      Result = count > 0 ? "Cleared " + count + " named view(s)." : "No named views to clear.";
      return;
    }

    if (!Run) { Result = "Set Run = True to execute."; return; }

    if (string.IsNullOrWhiteSpace(Name))
    {
      Result = "Provide a valid Name.";
      Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Provide a valid Name.");
      return;
    }
    string viewName = Name.Trim();

    if (Objects == null || Objects.Count == 0)
    {
      Result = "No geometry provided.";
      Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No geometry provided.");
      return;
    }

    // 2. Combined bounding box of ALL objects (the old item-access input ran once
    //    per object, so the saved view only framed the LAST object)
    BoundingBox allBox = BoundingBox.Empty;
    int skipped = 0;
    foreach (object o in Objects)
    {
      BoundingBox bb = BoxOf(doc, o);
      if (bb.IsValid) allBox.Union(bb);
      else if (o != null) skipped++;
    }

    if (!allBox.IsValid)
    {
      Result = "No valid bounding box found.";
      Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No valid bounding box found.");
      return;
    }
    if (skipped > 0)
      Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, skipped + " item(s) had no geometry and were ignored.");

    // 3. Zoom the active viewport and save it
    var view = doc.Views.ActiveView;
    if (view == null)
    {
      Result = "No active viewport found.";
      Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No active viewport found.");
      return;
    }

    view.ActiveViewport.ZoomBoundingBox(allBox);
    view.Redraw();

    int existing = doc.NamedViews.FindByName(viewName);
    if (existing >= 0) doc.NamedViews.Delete(existing);

    int index = doc.NamedViews.Add(viewName, view.ActiveViewport.Id);
    if (index < 0)
    {
      Result = "Could not save named view '" + viewName + "'.";
      Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Could not save the named view.");
      return;
    }

    NameOut = viewName;
    Result = "Named view '" + viewName + "' " + (existing >= 0 ? "updated" : "created") +
             " from " + view.ActiveViewport.Name + ".";
  }

  // <Custom additional code>
  // Bounding box of geometry, a Grasshopper goo, or a Rhino object id
  private static BoundingBox BoxOf(RhinoDoc doc, object o)
  {
    if (o == null) return BoundingBox.Empty;

    var geoGoo = o as IGH_GeometricGoo;
    if (geoGoo != null)
    {
      // referenced Rhino objects: use the document object
      if (geoGoo.IsReferencedGeometry)
      {
        RhinoObject ro = doc.Objects.FindId(geoGoo.ReferenceID);
        if (ro != null) return ro.Geometry.GetBoundingBox(true);
      }
      return geoGoo.Boundingbox;
    }

    var goo = o as IGH_Goo;
    if (goo != null) o = goo.ScriptVariable();   // GH_Guid, GH_Point ...

    if (o is Guid)
    {
      RhinoObject ro = doc.Objects.FindId((Guid)o);
      return (ro != null) ? ro.Geometry.GetBoundingBox(true) : BoundingBox.Empty;
    }
    if (o is GeometryBase) return ((GeometryBase)o).GetBoundingBox(true);
    if (o is Point3d) return new BoundingBox(new[] { (Point3d)o });
    return BoundingBox.Empty;
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
