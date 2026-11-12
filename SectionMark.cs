// =====================================================================
//  SECTION MARK  -  Grasshopper C# Script component (plug and play)
// ---------------------------------------------------------------------
//  Name        : SectionMark
//  Nickname    : SecMark
//  Category    : Extra / Drawing
//  Description : Draws a plan section mark (line, two arrow flags and a
//                label) flat on the input plane. Line runs along the
//                plane Y axis, arrows face the plane X vector.
//                The label is placed at the line end with the lower
//                world Y (near side) and always grows away from the line,
//                centred left/right, so it stays outside the object.
//                FlipMark = true moves the label to the opposite end.
//                Also outputs a clip plane built from the section plane.
//
//  INPUTS  (rename the params exactly like this)
//    SectionPlane : Plane   (Item)  - required, placement of the mark
//    MarkScale    : double  (Item)  - optional, 2D scale of the mark, default 1
//    TextHeight   : double  (Item)  - optional, label text height, default 1
//    LabelText    : string  (Item)  - optional, label, default "SEC-A"
//    FlipMark     : bool    (Item)  - optional, true = label on the other side, default false
//
//  OUTPUTS
//    Mark         : Curve   (List)  - section line + 2 arrow flags
//    Label        : TextEntity (Item) - label, bakeable
//    ClipPlane    : Plane   (Item)  - Origin = O, X = -Y, Y = -Z
//
//  Author  : Rajeev Pulari
//  Version : 2.2 (2026-11-12)
//    - Moved from the temp repo (Drafting/) to Bullbull, branch Annotation.
//    - Name, nickname and description are set once, not every solve.
//    - No crash without an active Rhino document: the label falls back to a
//      default dimension style.
//    - NaN / infinite MarkScale and TextHeight fall back to the defaults.
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
		Plane SectionPlane,
		double MarkScale,
		double TextHeight,
		string LabelText,
		bool FlipMark,
		ref object Mark,
		ref object Label,
		ref object ClipPlane)
  {
    SetPinTips();   // pin tooltips (set once, matched by name)

    Component.Message  = "Section Mark 2.2";

    // ---- defaults / validation ----
    Plane pl = SectionPlane;
    if (!pl.IsValid)
    {
      Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "SectionPlane invalid - using World XY.");
      pl = Plane.WorldXY;
    }
    MarkScale  = OrDefault(MarkScale, 1.0);
    TextHeight = OrDefault(TextHeight, 1.0);
    if (string.IsNullOrEmpty(LabelText)) LabelText = "SEC-A";

    // ---- build ----
    Mark = BuildMark(pl, MarkScale);
    Label = BuildLabel(pl, MarkScale, TextHeight, LabelText, FlipMark);
    ClipPlane = BuildClipPlane(pl);
  }
  #endregion

  #region Additional methods
  // 0, negative, NaN or infinite -> default
  private static double OrDefault(double v, double def)
  {
    return (v > 0 && !double.IsNaN(v) && !double.IsInfinity(v)) ? v : def;
  }

  private const double BaseLength = 10.0;   // line length at scale 1
  private const double BaseArrow  = 1.0;    // arrow flag size at scale 1
  private const double FlagBack   = 0.35;   // flag return inset (x arrow size)
  private const double TextGap    = 0.3;    // text gap (x arrow size)
  private const double YTol       = 1e-6;   // tolerance for "same Y"

  // Clip plane: Deconstruct Plane -> Reverse Y, Reverse Z -> Construct Plane (O, X, Y)
  private Plane BuildClipPlane(Plane pl)
  {
    return new Plane(pl.Origin, -pl.YAxis, -pl.ZAxis);
  }

  // Section line + arrow flags, flat on the plane, scaled uniformly in 2D.
  private List<Curve> BuildMark(Plane pl, double scale)
  {
    double len = BaseLength * scale;
    double arw = BaseArrow * scale;

    Vector3d lineDir = pl.YAxis;   // line direction
    Vector3d viewDir = pl.XAxis;   // arrows face this way

    Point3d start = pl.Origin - lineDir * (len / 2.0);
    Point3d end   = pl.Origin + lineDir * (len / 2.0);

    var crvs = new List<Curve>();
    crvs.Add(new LineCurve(start, end));

    Point3d[] ends = { start, end };
    Vector3d[] outward = { -lineDir, lineDir };
    for (int i = 0; i < 2; i++)
    {
      Point3d e    = ends[i];
      Point3d tip  = e + viewDir * arw;
      Point3d back = e - outward[i] * (arw * FlagBack);
      crvs.Add(new PolylineCurve(new Point3d[] { e, tip, back, e }));
    }
    return crvs;
  }

  // Label flat on the plane, beyond the line end with the lower world Y
  // (or the opposite end when flip is true), growing away from the line
  // and centred left/right so it never sits on the object.
  private TextEntity BuildLabel(Plane pl, double scale, double textHeight, string text, bool flip)
  {
    double len = BaseLength * scale;
    double arw = BaseArrow * scale;

    // default: start end (-Y). Switch to the other end if it is nearer (lower world Y).
    bool atPlusY = false;
    Point3d startEnd = pl.Origin - pl.YAxis * (len / 2.0);
    Point3d endEnd   = pl.Origin + pl.YAxis * (len / 2.0);
    if (endEnd.Y < startEnd.Y - YTol) atPlusY = true;

    // user override: flip to the opposite side
    if (flip) atPlusY = !atPlusY;

    Vector3d outward = atPlusY ? pl.YAxis : -pl.YAxis;
    Point3d labelEnd = pl.Origin + outward * (len / 2.0);
    Plane tp = new Plane(labelEnd + outward * (arw * TextGap), pl.XAxis, pl.YAxis);

    RhinoDoc doc = RhinoDoc.ActiveDoc;
    DimensionStyle ds = (doc != null) ? doc.DimStyles.Current : new DimensionStyle();
    TextEntity te = TextEntity.Create(text, tp, ds, false, 0, 0);
    if (te == null) return null;
    te.TextHeight = textHeight;
    te.TextHorizontalAlignment = TextHorizontalAlignment.Center;
    // text grows away from the line: Bottom anchor when outward is +Y, Top anchor when -Y
    te.TextVerticalAlignment = atPlusY ? TextVerticalAlignment.Bottom : TextVerticalAlignment.Top;
    return te;
  }
  #endregion

  // ---------------------------------------------------------------- pin tooltips
  private bool _pinTipsSet = false;

  private void SetPinTips()
  {
    if (_pinTipsSet || Component == null) return;
    _pinTipsSet = true;
    Component.Name        = "SectionMark";
    Component.NickName    = "SecMark";
    Component.Description = "Plan section mark: section line, two arrow flags and a label flat on a plane, " +
                            "plus its clip plane. Rajeev Pulari, v2.2.";
    TipPin(Component.Params.Input, "SectionPlane", "Where the section mark goes (required). The line runs along the plane Y axis, arrows face its X axis.");
    TipPin(Component.Params.Input, "MarkScale", "2D scale of the mark. Default 1 (line length 10).");
    TipPin(Component.Params.Input, "TextHeight", "Label text height. Default 1.");
    TipPin(Component.Params.Input, "LabelText", "Label text. Default SEC-A.");
    TipPin(Component.Params.Input, "FlipMark", "True = put the label at the other end of the line.");
    TipPin(Component.Params.Output, "Mark", "Section line + 2 arrow flags (curves).");
    TipPin(Component.Params.Output, "Label", "The label as a text entity (bakeable).");
    TipPin(Component.Params.Output, "ClipPlane", "Clip plane built from the section plane: origin O, X = -Y, Y = -Z.");
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
