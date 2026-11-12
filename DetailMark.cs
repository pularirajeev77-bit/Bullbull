// =====================================================================
//  DETAIL MARK  -  Grasshopper C# Script component (plug and play)
// ---------------------------------------------------------------------
//  Name        : DetailMark
//  Nickname    : DetMark
//  Category    : Extra / Drawing
//  Description : Draws a detail mark like the SectionMark family:
//                a circle standing perpendicular to the detail plane
//                (circle normal = plane X, the view direction), a tail
//                leaving the circle edge along the plane -Y axis, an arrow
//                flag at the tail end facing plane X, and a label beyond
//                the flag. FlipMark = true sends the tail the other way.
//                Also outputs a clip plane built from the detail plane.
//
//  INPUTS  (rename the params exactly like this)
//    DetailPlane  : Plane   (Item)  - required, placement of the mark
//    Radius       : double  (Item)  - optional, circle radius, default 1
//    MarkScale    : double  (Item)  - optional, flag size (arrow = 1 x MarkScale), default 1
//    TextHeight   : double  (Item)  - optional, label text height, default 1
//    LabelText    : string  (Item)  - optional, label, default "DET-1"
//    FlipMark     : bool    (Item)  - optional, true = tail on the other side, default false
//
//  OUTPUTS
//    Mark         : Curve   (List)  - circle + tail polyline with flag
//    Label        : TextEntity (Item) - label, bakeable
//    ClipPlane    : Plane   (Item)  - Origin = O, X = -Y, Y = -Z
//
//  Author  : Rajeev Pulari
//  Version : 1.2 (2026-11-12)
//    - Metadata and pin tooltips are set once (matched by pin Name), not every solve.
//    - No crash without an active Rhino document: the label falls back to a default
//      dimension style.
//    - NaN / infinite Radius, MarkScale and TextHeight fall back to the defaults.
//    - Label and Mark get the same flag size (MarkScale) and tail direction.
//  Version 1.1 - first version in Bullbull.
// =====================================================================

#region Usings
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Rhino;
using Rhino.Geometry;
using Rhino.DocObjects;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
#endregion

public class Script_Instance : GH_ScriptInstance
{
  #region Runscript
  private void RunScript(
		Plane DetailPlane,
		double Radius,
		double MarkScale,
		double TextHeight,
		string LabelText,
		bool FlipMark,
		ref object Mark,
		ref object Label,
		ref object ClipPlane)
  {
    SetMetadata();
    Component.Message = "Detail Mark 1.2";

    // ---- defaults / validation ----
    Plane pl = DetailPlane;
    if (!pl.IsValid)
    {
      Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "DetailPlane invalid - using World XY.");
      pl = Plane.WorldXY;
    }
    Radius     = OrDefault(Radius, 1.0);
    MarkScale  = OrDefault(MarkScale, 1.0);
    TextHeight = OrDefault(TextHeight, 1.0);
    if (string.IsNullOrEmpty(LabelText)) LabelText = "DET-1";

    // ---- build ----
    Mark = BuildMark(pl, Radius, MarkScale, FlipMark);
    Label = BuildLabel(pl, Radius, MarkScale, TextHeight, LabelText, FlipMark);
    ClipPlane = BuildClipPlane(pl);
  }
  #endregion

  #region Additional methods
  // 0, negative, NaN or infinite -> default
  private static double OrDefault(double v, double def)
  {
    return (v > 0 && !double.IsNaN(v) && !double.IsInfinity(v)) ? v : def;
  }

  private const double TailRatio = 1.333;   // tail length = Radius x TailRatio (75 -> 100)
  private const double BaseArrow = 1.0;     // flag size at MarkScale 1
  private const double FlagBack  = 0.35;    // flag return inset (x flag size)
  private const double TextGap   = 0.3;     // text gap after the flag (x flag size)

  // Clip plane: Deconstruct Plane -> Reverse Y, Reverse Z -> Construct Plane (O, X, Y)
  private Plane BuildClipPlane(Plane pl)
  {
    return new Plane(pl.Origin, -pl.YAxis, -pl.ZAxis);
  }

  // default tail direction is plane -Y (matches the drawn reference); FlipMark reverses it
  private Vector3d TailDir(Plane pl, bool flip) { return flip ? pl.YAxis : -pl.YAxis; }

  // Circle perpendicular to the plane (normal = plane X), tail along -Y with an arrow flag facing X.
  private List<Curve> BuildMark(Plane pl, double radius, double scale, bool flip)
  {
    var crvs = new List<Curve>();

    // circle: lies in the (Y, Z) plane of the detail plane, centre on the origin
    Plane cp = new Plane(pl.Origin, pl.YAxis, pl.ZAxis);
    crvs.Add(new ArcCurve(new Circle(cp, radius)));

    // tail + flag as one polyline: edge -> end -> tip -> back
    Vector3d dir = TailDir(pl, flip);
    double arw = BaseArrow * scale;
    Point3d e0   = pl.Origin + dir * radius;
    Point3d e    = e0 + dir * (radius * TailRatio);
    Point3d tip  = e + pl.XAxis * arw;
    Point3d back = e - dir * (arw * FlagBack);
    crvs.Add(new PolylineCurve(new Point3d[] { e0, e, tip, back }));
    return crvs;
  }

  // Label just beyond the flag, baseline along the tail, readable from the view (plane X) side.
  private TextEntity BuildLabel(Plane pl, double radius, double scale, double textHeight, string text, bool flip)
  {
    Vector3d dir = TailDir(pl, flip);
    double arw = BaseArrow * scale;
    Point3d e = pl.Origin + dir * (radius + radius * TailRatio);
    Plane tp = new Plane(e + dir * (arw * TextGap), pl.YAxis, pl.ZAxis);

    RhinoDoc doc = RhinoDoc.ActiveDoc;
    DimensionStyle ds = (doc != null) ? doc.DimStyles.Current : new DimensionStyle();
    TextEntity te = TextEntity.Create(text, tp, ds, false, 0, 0);
    if (te == null) return null;
    te.TextHeight = textHeight;
    // text runs along +Y; the tail end is on the -Y side by default, so anchor Right there
    te.TextHorizontalAlignment = flip ? TextHorizontalAlignment.Left : TextHorizontalAlignment.Right;
    te.TextVerticalAlignment = TextVerticalAlignment.Middle;
    return te;
  }
  #endregion

  // ---------------------------------------------------------------- metadata + tooltips
  private bool _metaSet = false;

  private void SetMetadata()
  {
    if (_metaSet || Component == null) return;
    _metaSet = true;

    Component.Name        = "DetailMark";
    Component.NickName    = "DetMark";
    Component.Description = "Detail mark like the SectionMark family: a circle standing on the detail plane, " +
                            "a tail with an arrow flag and a label, plus a clip plane. Rajeev Pulari, v1.2.";

    TipPin(Component.Params.Input, "DetailPlane", "Placement of the mark. Plane X = view direction (circle normal), tail runs along plane -Y. Required (World XY if invalid).");
    TipPin(Component.Params.Input, "Radius", "Circle radius. Tail length = Radius x 1.333. Default 1.");
    TipPin(Component.Params.Input, "MarkScale", "Size of the arrow flag at the tail end (flag = 1 x MarkScale). Default 1.");
    TipPin(Component.Params.Input, "TextHeight", "Label text height. Default 1.");
    TipPin(Component.Params.Input, "LabelText", "Label written beyond the flag. Default DET-1.");
    TipPin(Component.Params.Input, "FlipMark", "True = tail, flag and label go to the other side (plane +Y). Default false.");

    TipPin(Component.Params.Output, "Mark", "Mark curves: the circle and the tail polyline with its flag.");
    TipPin(Component.Params.Output, "Label", "Label text entity - bake it or feed a text/annotation bake component.");
    TipPin(Component.Params.Output, "ClipPlane", "Clip plane from the detail plane: origin = plane origin, X = -Y, Y = -Z.");
  }

  // Match pins by Name (the script variable), fall back to NickName.
  // Only NickName/Description are changed - never Name.
  private void TipPin(IList<IGH_Param> ps, string name, string tip)
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