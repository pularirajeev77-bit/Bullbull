#region Metadata
/*
  Author      : rajeev pulari / Gemini
  Platform    : Rhino 8 | Grasshopper C#
  Version     : 2026.10.02
  Component   : DViewNameEditor
  NickName    : DVNE
  Message     : Detail View Name Editor v2.1
  Description : Lists the detail views on each layout in reading order
                (rows top -> bottom, left -> right inside a row) and renames them
                with DetailNames in that order. The same DetailNames are used for
                every layout. Without DetailNames the existing names are kept.

  Inputs:
    Run         : bool         (Item) - True = read (and rename) the details
    Layouts     : List<string> (List) - layout page names
    DetailNames : List<string> (List) - new names, in reading order (optional)

  Outputs (path {i} = Layouts[i], items in reading order):
    Details    : the detail view objects
    DetailIds  : their object ids (text)
    Rectangles : detail frame on the page (null if it has no geometry)
    Centers    : frame centre on the page (null if it has no geometry)
    Names      : the name each detail has now
*/
#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Rhino;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

public class Script_Instance : GH_ScriptInstance
{
    // Run : item | Layouts, DetailNames : list
    private void RunScript(
        bool Run,
        List<string> Layouts,
        List<string> DetailNames,
        ref object Details,
        ref object DetailIds,
        ref object Rectangles,
        ref object Centers,
        ref object Names)
    {
        // Metadata + pin tooltips (once)
        if (Component != null && Component.Name != "DViewNameEditor")
        {
            Component.Name = "DViewNameEditor";
            Component.NickName = "DVNE";
            Component.Message = "Detail View Name Editor v2.1";
            Component.Description = "Lists detail views per layout in reading order (top->bottom, left->right) and renames them.";

            var pi = Component.Params.Input;
            SetTip(pi, "Run", "True = read the details and apply DetailNames.");
            SetTip(pi, "Layouts", "Layout page names. List access.");
            SetTip(pi, "DetailNames", "New detail names in reading order (top->bottom, left->right). Used for every layout. Empty = keep names. List access.");
            var po = Component.Params.Output;
            SetTip(po, "Details", "Detail view objects, path {layout}, in reading order.");
            SetTip(po, "DetailIds", "Detail object ids (text).");
            SetTip(po, "Rectangles", "Detail frame on the page (null if none).");
            SetTip(po, "Centers", "Frame centre on the page (null if none).");
            SetTip(po, "Names", "The name each detail has now.");
        }

        if (!Run)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Run = False - skipped.");
            return;
        }
        if (Layouts == null || Layouts.Count == 0)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No layout names provided.");
            return;
        }

        RhinoDoc doc = RhinoDocument ?? RhinoDoc.ActiveDoc;
        if (doc == null)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No active Rhino document.");
            return;
        }

        var treeViews = new DataTree<object>();
        var treeIds = new DataTree<string>();
        var treeRect = new DataTree<object>();    // object, so a missing frame can be null
        var treeCent = new DataTree<object>();
        var treeNames = new DataTree<string>();

        RhinoPageView[] pages = doc.Views.GetPageViews();
        var missing = new List<string>();
        int renamed = 0;

        uint undo = doc.BeginUndoRecord("Detail View Name Editor");
        try
        {
            for (int i = 0; i < Layouts.Count; i++)
            {
                GH_Path path = new GH_Path(i);
                treeViews.EnsurePath(path); treeIds.EnsurePath(path); treeRect.EnsurePath(path);
                treeCent.EnsurePath(path); treeNames.EnsurePath(path);

                string lname = (Layouts[i] ?? "").Trim();
                RhinoPageView layout = pages.FirstOrDefault(p => string.Equals(p.PageName, lname, StringComparison.OrdinalIgnoreCase));
                if (layout == null) { missing.Add(lname); continue; }

                DetailViewObject[] details = layout.GetDetailViews();
                if (details == null || details.Length == 0) continue;

                // Frame box and centre of every detail
                var items = new List<DetailItem>();
                foreach (DetailViewObject d in details)
                {
                    var item = new DetailItem { Detail = d, BBox = BoundingBox.Empty, Center = Point3d.Origin };
                    if (d.DetailGeometry != null)
                    {
                        BoundingBox bb = d.DetailGeometry.GetBoundingBox(true);
                        if (bb.IsValid)
                        {
                            item.BBox = bb;
                            item.Center = new Point3d((bb.Min.X + bb.Max.X) / 2.0, (bb.Min.Y + bb.Max.Y) / 2.0, 0);
                        }
                    }
                    items.Add(item);
                }

                List<DetailItem> ordered = ReadingOrder(items);

                for (int j = 0; j < ordered.Count; j++)
                {
                    DetailItem item = ordered[j];
                    DetailViewObject detail = item.Detail;

                    treeViews.Add(detail, path);
                    treeIds.Add(detail.Id.ToString(), path);
                    if (item.BBox.IsValid)
                    {
                        treeRect.Add(new Rectangle3d(Plane.WorldXY, item.BBox.Min, item.BBox.Max), path);
                        treeCent.Add(item.Center, path);
                    }
                    else
                    {
                        treeRect.Add(null, path);
                        treeCent.Add(null, path);
                    }

                    string current = detail.Attributes.Name ?? "";
                    string wanted = (DetailNames != null && j < DetailNames.Count && DetailNames[j] != null) ? DetailNames[j] : current;

                    if (!string.Equals(current, wanted, StringComparison.Ordinal))
                    {
                        ObjectAttributes attr = detail.Attributes.Duplicate();
                        attr.Name = wanted;
                        if (doc.Objects.ModifyAttributes(detail, attr, true)) { renamed++; current = wanted; }
                    }
                    treeNames.Add(current, path);
                }

                if (DetailNames != null && DetailNames.Count > 0 && DetailNames.Count != ordered.Count)
                    Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                        "'" + lname + "' has " + ordered.Count + " details but " + DetailNames.Count + " names were given.");
            }
        }
        finally
        {
            doc.EndUndoRecord(undo);
        }

        if (missing.Count > 0)
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Layout(s) not found: " + string.Join(", ", missing));
        Component.Message = "Detail View Name Editor v2.1 | " + renamed + " renamed";

        Details = treeViews;
        DetailIds = treeIds;
        Rectangles = treeRect;
        Centers = treeCent;
        Names = treeNames;
        doc.Views.Redraw();
    }

    // <Custom additional code>
    private class DetailItem
    {
        public DetailViewObject Detail;
        public BoundingBox BBox;
        public Point3d Center;
    }

    // Rows top -> bottom, left -> right. Details whose centres differ in Y by less than
    // a quarter of the smallest detail height are one row - so a row whose details are
    // a hair out of line is no longer read in the wrong order (plain Y-sort did that).
    private static List<DetailItem> ReadingOrder(List<DetailItem> items)
    {
        double minH = items.Where(t => t.BBox.IsValid).Select(t => t.BBox.Max.Y - t.BBox.Min.Y)
                           .Where(h => h > 0).DefaultIfEmpty(0).Min();
        double rowTol = minH > 0 ? minH * 0.25 : 1e-6;

        var byY = items.OrderByDescending(t => t.Center.Y).ToList();
        var result = new List<DetailItem>();
        var row = new List<DetailItem>();
        double rowY = double.NaN;

        foreach (DetailItem t in byY)
        {
            if (row.Count > 0 && Math.Abs(rowY - t.Center.Y) > rowTol)
            {
                result.AddRange(row.OrderBy(r => r.Center.X));
                row.Clear();
            }
            if (row.Count == 0) rowY = t.Center.Y;
            row.Add(t);
        }
        result.AddRange(row.OrderBy(r => r.Center.X));
        return result;
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
