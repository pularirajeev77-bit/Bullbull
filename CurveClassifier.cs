/*
  Author: Rajeev Pulari + ChatGPT
  Rhino 8 | Grasshopper C#
  Version: 2025.11.08
  Component: CurveClassifier_Smart
  Description:
    Classifies and groups curves by type (Line, Polyline, Arc, Circle,
    Ellipse, EllipticalArc, PolyCurve, Other).
    Only activates outputs for detected curve types.
    Always returns a Type output with classification names.
*/

using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Geometry;
using Grasshopper;
using Grasshopper.Kernel.Data;

public class Script_Instance : GH_ScriptInstance
{
  // Crv is tree access, so item, list and tree input all work and the
  // input's branch paths are kept. (As "object Crv", a list arrived as
  // List<object> and a tree as DataTree<object>, which matched neither
  // IList<Curve> nor DataTree<Curve> -- so list/tree input fell through
  // to "Invalid Input".)
  private void RunScript(
		DataTree<Curve> Crv,
		ref object Type,
		ref object Lines,
		ref object Polylines,
		ref object Arcs,
		ref object Circles,
		ref object Ellipses,
		ref object EllipticalArcs,
		ref object PolyCurves,
		ref object Others)
  {
    SetPinTips();   // pin tooltips (set once, matched by name)

    this.Component.Message = "Curve Classifier v2.0";
    this.Component.NickName = "CrvClass";

    // --- Prepare grouped trees ---
    var lines     = new DataTree<object>();
    var polylines = new DataTree<object>();
    var arcs      = new DataTree<object>();
    var circles   = new DataTree<object>();
    var ellipses  = new DataTree<object>();
    var ellipArcs = new DataTree<object>();
    var polycurves= new DataTree<object>();
    var others    = new DataTree<object>();
    var typeTree  = new DataTree<object>();

    // Classification tolerance: the model tolerance. The parameterless
    // TryGetArc/TryGetEllipse use a near-zero tolerance, so arcs and
    // circles from imports or other software were often missed.
    double tol = RhinoDoc.ActiveDoc != null
      ? RhinoDoc.ActiveDoc.ModelAbsoluteTolerance
      : 0.001;

    // --- Classification ---
    if (Crv != null)
    {
      foreach (GH_Path path in Crv.Paths)
      {
        foreach (Curve crv in Crv.Branch(path))
        {
          if (crv == null)
          {
            // Keep Type aligned item-for-item with the input
            typeTree.Add(null, path);
            continue;
          }

          string type = Classify(crv, tol);
          typeTree.Add(type, path);

          switch (type)
          {
            case "Line":           lines.Add(crv, path); break;
            case "Polyline":       polylines.Add(crv, path); break;
            case "Arc":            arcs.Add(crv, path); break;
            case "Circle":         circles.Add(crv, path); break;
            case "Ellipse":        ellipses.Add(crv, path); break;
            case "EllipticalArc":  ellipArcs.Add(crv, path); break;
            case "PolyCurve":      polycurves.Add(crv, path); break;
            default:               others.Add(crv, path); break;
          }
        }
      }
    }

    // --- Assign outputs only if populated ---
    Type          = typeTree.BranchCount > 0 ? typeTree : null;
    Lines         = lines.BranchCount > 0 ? lines : null;
    Polylines     = polylines.BranchCount > 0 ? polylines : null;
    Arcs          = arcs.BranchCount > 0 ? arcs : null;
    Circles       = circles.BranchCount > 0 ? circles : null;
    Ellipses      = ellipses.BranchCount > 0 ? ellipses : null;
    EllipticalArcs= ellipArcs.BranchCount > 0 ? ellipArcs : null;
    PolyCurves    = polycurves.BranchCount > 0 ? polycurves : null;
    Others        = others.BranchCount > 0 ? others : null;
  }

  // --------------------------------------------------------------------------
  // Detection works on the shape, not on the curve's object type. The old
  // checks only looked at specific types (e.g. polyline only as
  // PolylineCurve/PolyCurve), so a degree-1 NURBS polyline -- common from
  // imports and many Grasshopper operations -- came out as "Other", and a
  // 2-point PolylineCurve was called "Polyline" instead of "Line".
  private string Classify(Curve crv, double tol)
  {
    // Straight curve of any kind (LineCurve, 2-point polyline, straight NURBS)
    if (crv.IsLinear(tol)) return "Line";

    Polyline pl;
    if (crv.TryGetPolyline(out pl) && pl.Count > 2) return "Polyline";

    Circle circle;
    if (crv.IsClosed && crv.TryGetCircle(out circle, tol)) return "Circle";

    Arc arc;
    if (crv.TryGetArc(out arc, tol)) return arc.IsCircle ? "Circle" : "Arc";

    Ellipse ellipse;
    if (crv.TryGetEllipse(out ellipse, tol)) return crv.IsClosed ? "Ellipse" : "EllipticalArc";

    if (crv is PolyCurve) return "PolyCurve";

    return "Other";
  }

  // ---------------------------------------------------------------- pin tooltips
  private bool _pinTipsSet = false;

  private void SetPinTips()
  {
    if (_pinTipsSet || Component == null) return;
    _pinTipsSet = true;
    TipPin(Component.Params.Input, "Crv", "The curves (a single curve or a list works too)");
    TipPin(Component.Params.Output, "Type", "The type name of every curve, in input order.");
    TipPin(Component.Params.Output, "Lines", "Straight curves.");
    TipPin(Component.Params.Output, "Polylines", "Polylines with 3 or more points.");
    TipPin(Component.Params.Output, "Arcs", "Open arcs.");
    TipPin(Component.Params.Output, "Circles", "Full circles.");
    TipPin(Component.Params.Output, "Ellipses", "Closed ellipses.");
    TipPin(Component.Params.Output, "EllipticalArcs", "Open parts of an ellipse.");
    TipPin(Component.Params.Output, "PolyCurves", "Joined curves that are none of the other types.");
    TipPin(Component.Params.Output, "Others", "Everything else (e.g. free-form NURBS)");
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
