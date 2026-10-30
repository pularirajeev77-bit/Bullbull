using System;
using System.Collections.Generic;
using System.Linq;
using Rhino;
using Rhino.Geometry;
using Rhino.DocObjects;
using Grasshopper;
using Grasshopper.Kernel;

public class Script_Instance : GH_ScriptInstance
{
  // AutoDimension - automatic dimensioning for Rhino / Grasshopper.
  // Dimensions curves, lines, polylines and arcs: segment lengths, angles, corner chains,
  // overall width/height, hole diameters, hole counts and object-name tags.
  //   Curves     (in, tree): geometry to dimension; one branch = one drawing. Circles are treated as holes.
  //   Bake       (in, bool): true writes the dimensions into the Rhino model, false previews only.
  //   Dimensions (out)     : the annotation objects, one branch per drawing.
  //   Report     (out)     : a text summary per drawing.
  private void RunScript(
		DataTree<object> Curves,
		object Bake,
		ref object Dimensions,
		ref object Report)
  {
    SetMeta();
    var doc = RhinoDoc.ActiveDoc;
    if (doc == null) { Report = "No Rhino document."; return; }
    var bkv = Bake is Grasshopper.Kernel.Types.IGH_Goo bg ? bg.ScriptVariable() : Bake;
    bool bake = bkv is bool bb && bb;
    // every branch is one drawing; a later branch keeps clear of the dimensions of the ones before it
    var dimsT = new DataTree<AnnotationBase>(); var repT = new DataTree<string>(); var allMade = new List<Guid>();
    uint undo = bake ? doc.BeginUndoRecord("Dimension auto") : 0;
    try
    {
      if (Curves != null) for (int bi = 0; bi < Curves.BranchCount; bi++) {
        var made = new List<Guid>(); string rep;
        try { rep = DimOne(Curves.Branch(bi), bake, doc, made); } catch (Exception ex) { rep = "Error: " + ex.Message; }
        foreach (var id in made.Distinct()) { var ob = doc.Objects.FindId(id); if (ob?.Geometry is AnnotationBase ab) dimsT.Add((AnnotationBase)ab.Duplicate(), Curves.Path(bi)); }
        allMade.AddRange(made); repT.Add(rep, Curves.Path(bi));
      }
    }
    finally
    {
      if (!bake) foreach (var id in allMade.Distinct()) { var ob = doc.Objects.FindId(id); if (ob != null) doc.Objects.Purge(ob); }
      if (undo != 0) doc.EndUndoRecord(undo);
      doc.Views.Redraw();
    }
    Dimensions = dimsT; Report = repT;
  }

  // component name, nickname, message, description and the tooltips of every input / output
  private void SetMeta()
  {
    // Set Component Metadata for Rhino 8
    Component.Name = "AutoDimension 1.0";
    Component.NickName = "AutoDim";
    Component.Message = "Auto Dimensioning";
    Component.Description = "Automatically dimensions curves, lines, polylines and arcs: segment lengths, angles, corner chains, overall size, hole diameters/counts and name tags. Each input branch is dimensioned as a separate drawing.";

    // the nicknames of the parameters are the variable names in the code, so only the tooltips are set here
    void P(List<IGH_Param> list, string nick, string desc)
    {
      var p = list.FirstOrDefault(q => q.NickName == nick);
      if (p != null) p.Description = desc;
    }
    P(Component.Params.Input, "Curves",
      "Geometry to dimension: curves, lines, polylines or arcs (closed circles are treated as holes). Supply a data tree: every branch is one drawing, and later branches keep clear of the dimensions of earlier ones.");
    P(Component.Params.Input, "Bake",
      "Boolean. True writes the dimensions into the Rhino model on layers Drawing::Dimension / Text / Marks / Tag (one Ctrl+Z undoes it). False (default) only previews them.");
    P(Component.Params.Output, "Dimensions",
      "The generated annotation objects (linear, angular, radial dimensions, text and leaders), one branch per input drawing.");
    P(Component.Params.Output, "Report",
      "Text summary per drawing: annotations added, shapes and holes found, name tags, overlaps and anything already dimensioned.");
  }

  // one branch: the curves of one drawing
  private string DimOne(List<object> C, bool bake, RhinoDoc doc, List<Guid> made)
  {
    string Report = "";
    {
      // the curves are joined for measuring only - the model is not changed
      var inC = new List<Curve>();
      foreach (var x0 in C ?? new List<object>()) { var xv = x0 is Grasshopper.Kernel.Types.IGH_Goo gx ? gx.ScriptVariable() : x0;
        if (xv is Curve cx) inC.Add(cx); else if (xv is Line lx) inC.Add(new LineCurve(lx)); else if (xv is Polyline plx) inC.Add(new PolylineCurve(plx)); else if (xv is Arc arx) inC.Add(new ArcCurve(arx)); else if (xv is Circle cix) inC.Add(new ArcCurve(cix)); }
      if (inC.Count == 0) return "Connect one or more curves to Curves.";
      var xy = Plane.WorldXY; double atol = doc.ModelAbsoluteTolerance;
      bool IsCircle(Curve c, out Circle ci) { ci = Circle.Unset; return c.IsClosed && c.TryGetCircle(out ci, atol * 10); }
      var holes = new List<Circle>(); var shapes = new List<Curve>();
      void AddCircle(Circle ci) { if (!holes.Any(h => h.Center.DistanceTo(ci.Center) < atol * 10 && Math.Abs(h.Radius - ci.Radius) < atol * 10)) holes.Add(ci); }
      { var part = new List<Curve>(); foreach (var c0 in inC) { var c = c0.DuplicateCurve(); if (IsCircle(c, out Circle ci)) AddCircle(ci); else part.Add(c); }
        if (part.Count > 1) { var j = Curve.JoinCurves(part, atol * 2); if (j != null && j.Length > 0) part = j.ToList(); }
        shapes.AddRange(part); }
      var main = shapes.OrderByDescending(c => c.GetBoundingBox(true).Diagonal.Length).FirstOrDefault();
      bool InMain(Point3d q) => main != null && main.IsClosed && main.Contains(q, xy, atol) == PointContainment.Inside;
      // holes: input circles, plus circles inside a closed main outline
      if (main != null && main.IsClosed) foreach (var o in doc.Objects.GetObjectList(new ObjectEnumeratorSettings { ObjectTypeFilter = ObjectType.Curve })) { var c = o.Geometry as Curve; if (c != null && IsCircle(c, out Circle ci) && InMain(ci.Center)) AddCircle(ci); }
      // names to tag: the holes that were found in the model
      var pickedIds = new HashSet<Guid>();
      var named = new List<(string nm, Curve cv)>();
      foreach (var o in doc.Objects.GetObjectList(new ObjectEnumeratorSettings { ObjectTypeFilter = ObjectType.Curve })) { if (pickedIds.Contains(o.Id)) continue; var c = o.Geometry as Curve; if (c != null && IsCircle(c, out Circle ci) && holes.Any(h => h.Center.DistanceTo(ci.Center) < atol * 10 && Math.Abs(h.Radius - ci.Radius) < atol * 10)) named.Add((o.Name, c)); }
      var bbAll = BoundingBox.Empty; foreach (var c in shapes) bbAll.Union(c.GetBoundingBox(true)); foreach (var h in holes) bbAll.Union(h.BoundingBox);
      double size = Math.Max(bbAll.Max.X - bbAll.Min.X, bbAll.Max.Y - bbAll.Min.Y); if (size <= 0) size = 1;
      var st = doc.DimStyles.Current;
      double k = Math.Round(size / 150.0, 1); if (k <= 0) k = 0.1;
      var ov = st.Duplicate(); ov.DimensionScale = k; ov.AngleResolution = 2; ov.DimTextLocation = DimensionStyle.TextLocation.AboveDimLine;
      // all leaders (name tags, hole counts): the same style with a landing line, 15 mm long in the model
      double landing = 15.0;
      var ovL = ov.Duplicate(); ovL.LeaderHasLanding = true; ovL.LeaderLandingLength = landing / k;
      DimensionStyle StyleOf(AnnotationBase a) => a is Leader ? ovL : ov;
      double u = st.TextHeight * k; double tol = size * 1e-6; double mtol = Math.Max(atol, tol);
      // a length that would show as 0 at the style's precision is not dimensioned (e.g. a 0.09 sliver between two lines)
      double minShow = 0.5 * Math.Pow(10, -Math.Max(0, ov.LengthResolution));
      double sideOff = 6 * u, gap = 2.4 * u;
      // --- one dimension per thing: every annotation the tool makes carries a key; a key that is already there is skipped ---
      double qg = Math.Max(atol * 10, 0.01);
      long Q(double v) => (long)Math.Round((v + 0.0031415) / qg);
      string sid = "0", mainSid = "0";
      // linear: same direction + same span along it, on the same shape
      string KLin(Point3d a, Point3d b, Vector3d dir, string scope) { if (!dir.Unitize()) return null; if (Math.Abs(dir.X) < 1e-6) dir = Vector3d.YAxis; else if (Math.Abs(dir.Y) < 1e-6) dir = Vector3d.XAxis; else if (dir.X < 0) dir = -dir; double pa = new Vector3d(a) * dir, pb = new Vector3d(b) * dir; return $"LIN|{scope}|{Math.Round(Math.Atan2(dir.Y, dir.X) * 9000 / Math.PI)}|{Q(Math.Min(pa, pb))}|{Q(Math.Max(pa, pb))}"; }
      string KPt(string kind, Point3d p) => $"{kind}|{Q(p.X)}|{Q(p.Y)}";
      string KRad(Point3d c, double r) => $"RAD|{Q(c.X)}|{Q(c.Y)}|{Q(r)}";
      var keys = new List<string>(); int skipped = 0;
      bool Has(string key) { if (key == null) return false; if (keys.Contains(key)) return true; if (key.StartsWith("LIN|")) { int i2 = key.IndexOf('|', 4); return i2 > 0 && keys.Contains("LIN|*" + key.Substring(i2)); } return false; }
      // layers: Drawing > Dimension (linear, angle), Text (corner letters, notes), Marks (hole diameters, hole counts, arc radii), Tag (name leaders)
      int LayerAt(string path, string name, int parent) { int i = doc.Layers.FindByFullPath(path, -1); if (i >= 0) return i; var nl = new Layer { Name = name }; if (parent >= 0) nl.ParentLayerId = doc.Layers[parent].Id; i = doc.Layers.Add(nl); return i >= 0 ? i : doc.Layers.CurrentLayerIndex; }
      int layRoot = LayerAt("Drawing", "Drawing", -1);
      int layDim = LayerAt("Drawing::Dimension", "Dimension", layRoot), layText = LayerAt("Drawing::Text", "Text", layRoot), layMarks = LayerAt("Drawing::Marks", "Marks", layRoot), layTag = LayerAt("Drawing::Tag", "Tag", layRoot);
      var markLeaders = new HashSet<AnnotationBase>();
      var obsLines = new List<Curve>(shapes); foreach (var h in holes) obsLines.Add(new ArcCurve(h));
      var obsRects = new List<Curve>();
      // the dimension lines already placed: a new dimension line may not run parallel and overlapping close to one of these
      var obsDim = new List<Line>();
      // --- clash checking: the real text box of every dimension vs every line, arc and other text box ---
      Curve Rect(Point3d c, Vector3d xd, double w, double h) { xd.Unitize(); var pl = new Plane(c, xd, Vector3d.CrossProduct(Vector3d.ZAxis, xd)); return new PolylineCurve(new[] { pl.PointAt(-w / 2, -h / 2), pl.PointAt(w / 2, -h / 2), pl.PointAt(w / 2, h / 2), pl.PointAt(-w / 2, h / 2), pl.PointAt(-w / 2, -h / 2) }); }
      double TW(string s) => s.Length * 0.62 * u;
      string Shown(Dimension d) => d is AngularDimension ? (d.NumericValue * 180 / Math.PI).ToString("F2") + "°" : d is RadialDimension rd ? (rd.IsDiameterDimension ? "Ø" : "R") + d.NumericValue.ToString("F" + Math.Max(0, ov.LengthResolution)) : d.NumericValue.ToString("F" + Math.Max(0, ov.LengthResolution));
      double Sc(Dimension d) { double s = 0; try { s = d.DimensionScale; } catch { } return s > 0 ? s : k; }
      DimensionStyle DS(Dimension d) { try { var s = d.DimensionStyle; if (s != null) return s; } catch { } return ov; }
      // text width and height as Rhino draws it (style text, decimals and scale); estimated while a dimension is not in the model yet
      void TSize(Dimension d, double sc, out double w, out double h) { w = 0; h = 0; Point3d[] cs = null; bool ok = false;
        try { if (d is LinearDimension d1) ok = d1.GetTextRectangle(out cs); else if (d is RadialDimension d2) ok = d2.GetTextRectangle(out cs); else if (d is AngularDimension d3) ok = d3.GetTextRectangle(out cs); } catch { }
        if (ok && cs != null && cs.Length >= 4) { w = (cs.Max(q => q.X) - cs.Min(q => q.X)) * sc; h = (cs.Max(q => q.Y) - cs.Min(q => q.Y)) * sc; }
        double th = d.TextHeight * sc; if (th <= 0) th = u; if (w < th * 0.5) w = Shown(d).Length * 0.68 * th; if (h < th * 0.5) h = th * 1.05; }
      // lines = dimension/extension/leader lines, rects = text boxes with a margin, raw = text boxes without (own-line check), dls = the dimension lines of a linear dimension
      (List<Curve> lines, List<Curve> rects, List<Curve> raw, List<Line> dls) Parts(GeometryBase g) {
        var lines = new List<Curve>(); var rects = new List<Curve>(); var raw = new List<Curve>(); var dls = new List<Line>();
        void Box(Point3d c0, Vector3d ax, double w0, double h0, double pd) { if (w0 <= 0 || h0 <= 0 || ax.IsTiny()) return; rects.Add(Rect(c0, ax, w0 + 2 * pd, h0 + 2 * pd)); raw.Add(Rect(c0, ax, w0 * 0.96, h0 * 0.8)); }
        if (g is Dimension d) {
          double sc = Sc(d), th = d.TextHeight * sc; if (th <= 0) th = u; double pd = 0.25 * th;
          TSize(d, sc, out double w, out double h);
          GeometryBase[] ex = null; try { ex = d.Explode(); } catch { }
          TextEntity te = null; var own = new List<Curve>();
          if (ex != null) foreach (var e0 in ex) { if (e0 is LineCurve || e0 is ArcCurve) { var c0 = (Curve)e0; if (c0.GetLength() > mtol) own.Add(c0); } else if (e0 is TextEntity t0 && te == null) te = t0; }
          lines.AddRange(own);
          // the dimension line itself: the exploded line that runs parallel to the arrow-to-arrow direction
          if (d is LinearDimension ldx && ldx.Get3dPoints(out Point3d a1x, out Point3d a2x, out Point3d a3x, out Point3d a4x, out Point3d a5x, out Point3d a6x)) {
            var dvx = a4x - a3x;
            if (dvx.Unitize()) foreach (var oc0 in own) if (oc0 is LineCurve olc) { var vv = olc.PointAtEnd - olc.PointAtStart; if (vv.Unitize() && Math.Abs(vv.X * dvx.Y - vv.Y * dvx.X) < 1e-3) dls.Add(olc.Line); }
          }
          var tp = d.Plane.PointAt(d.TextPosition.X, d.TextPosition.Y);
          if (d is RadialDimension rd0) {
            // radial text is drawn AFTER the leader's dogleg, not on the knee
            var dl = new List<Line>(); try { if (rd0.GetDisplayLines(DS(d), sc, out IEnumerable<Line> ls0) && ls0 != null) dl = ls0.Where(q0 => q0.IsValid && q0.Length > mtol).ToList(); } catch { }
            var dir = d.Plane.XAxis; if ((tp - d.Plane.Origin) * dir < 0) dir = -dir; var end = tp + dir * th;
            foreach (var q2 in dl) if (q2.From.DistanceTo(tp) < th * 0.02 + mtol) { var dv0 = q2.To - q2.From; if (dv0.Unitize()) { dir = dv0; end = q2.To; } break; }
            double gp = 0.35 * th; Box(end + dir * ((gp + w) / 2), dir, gp + w, h, pd);
          }
          else if (te != null) Box(te.Plane.Origin, te.Plane.XAxis, w, h, pd);
          else {
            // not in the model yet: text sits above the dimension line / arc, reading left to right or upward
            Vector3d ax1; LineCurve dlc = null;
            if (d is AngularDimension) ax1 = Vector3d.CrossProduct(Vector3d.ZAxis, tp - d.Plane.Origin);
            else { dlc = own.OfType<LineCurve>().OrderBy(q3 => q3.Line.DistanceTo(tp, false)).FirstOrDefault(); ax1 = dlc != null ? dlc.PointAtEnd - dlc.PointAtStart : d.Plane.XAxis; }
            if (!ax1.Unitize()) ax1 = Vector3d.XAxis;
            if (ax1.X < -1e-6 || (Math.Abs(ax1.X) <= 1e-6 && ax1.Y < 0)) ax1 = -ax1;
            double tg = 1; try { tg = DS(d).TextGap; } catch { }
            var ctr = tp;
            if (d is LinearDimension lin0 && lin0.Get3dPoints(out Point3d g1, out Point3d g2, out Point3d g3, out Point3d g4, out Point3d g5, out Point3d g6)) { var mid = (g3 + g4) / 2; double len = g3.DistanceTo(g4); var px = d.Plane.XAxis; px.Unitize(); double off0 = (tp - mid) * px;
              // a text that does not fit between its extension lines: Rhino moves it out past the second one
              if (Math.Abs(off0) < 0.02 * th + mtol) { bool arrIn = len >= 2.1 * th, disp = len < w + 0.3 * th; double along = len / 2 + (arrIn ? 0.6 : 1.6) * th + w / 2;
                // once it is in the model, the dimension's own extents say exactly where the text went
                if (Math.Abs(px.X) < 1e-6 || Math.Abs(px.Y) < 1e-6) { var gb = d.GetBoundingBox(true); if (gb.IsValid) { double far = gb.GetCorners().Max(q4 => (q4 - mid) * px); if (far > len / 2 + 0.3 * th + w * 0.5) { disp = true; along = far - w / 2; } } }
                if (disp) { ctr = mid + px * along; lines.Add(new LineCurve(mid - px * (len / 2 + (arrIn ? 0 : 1.3 * th)), mid + px * (along + w / 2))); } }
              else if (Math.Abs(off0) > len / 2) lines.Add(new LineCurve(mid, tp + px * (Math.Sign(off0) * w / 2))); }
            Box(ctr + Vector3d.CrossProduct(Vector3d.ZAxis, ax1) * (tg * sc + h / 2), ax1, w, h, pd);
          }
        }
        else if (g is Leader ld0) {
          // leader: the line, its landing, and the text drawn level after the landing (to the right, or to the left when the line runs left)
          Point3d[] p3 = null; try { p3 = ld0.Points3D; } catch { }
          if (p3 != null && p3.Length >= 2) {
            lines.Add(new PolylineCurve(p3));
            double sc = ld0.DimensionScale > 0 ? ld0.DimensionScale : k, th = ld0.TextHeight * sc; if (th <= 0) th = u;
            var e1 = p3[p3.Length - 1]; double sg = e1.X - p3[p3.Length - 2].X >= -mtol ? 1 : -1;
            double land = 0; try { if (ld0.LeaderHasLanding) land = ld0.LeaderLandingLength * sc; } catch { }
            var e2 = e1 + Vector3d.XAxis * (sg * land); if (land > mtol) lines.Add(new LineCurve(e1, e2));
            double w = (ld0.PlainText ?? "").Length * 0.66 * th + 0.35 * th; var bbl = ld0.GetBoundingBox(true);
            if (bbl.IsValid) { double wr = (sg > 0 ? bbl.Max.X - e1.X : e1.X - bbl.Min.X) - land; if (wr > th) w = wr; }
            Box(e2 + Vector3d.XAxis * (sg * w / 2), Vector3d.XAxis, w, th * 1.05, 0.25 * th);
          }
        }
        else if (g is AnnotationBase ab) {
          var bb0 = ab.GetBoundingBox(true); double bw = bb0.IsValid ? bb0.Max.X - bb0.Min.X : 0, bh0 = bb0.IsValid ? bb0.Max.Y - bb0.Min.Y : 0;
          var t1 = g as TextEntity; double ew = t1 != null ? TW(t1.PlainText) : 0;
          if (bw > 0 && bh0 > 0 && bw >= ew * 0.5) Box(bb0.Center, Vector3d.XAxis, bw, bh0, 0.25 * u);
          else if (t1 != null) { var c2 = t1.Justification == TextJustification.TopLeft ? t1.Plane.PointAt(ew / 2, -u / 2) : t1.Plane.Origin; Box(c2, t1.Plane.XAxis, ew, u, 0.25 * u); }
        }
        return (lines, rects, raw, dls);
      }
      bool X(Curve a, Curve b) => Rhino.Geometry.Intersect.Intersection.CurveCurve(a, b, tol, tol).Count > 0;
      bool In(Curve rect, Point3d p) => rect.Contains(p, xy, tol) == PointContainment.Inside;
      int Hits(GeometryBase g) {
        var (lines, rects, raw, dls) = Parts(g); int h = 0;
        foreach (var r in rects) {
          foreach (var l in obsLines) if (X(r, l) || In(r, l.PointAtStart)) h++;
          foreach (var o in obsRects) if (X(r, o) || In(r, o.PointAtStart) || In(o, r.PointAtStart)) h++; }
        foreach (var r in raw) foreach (var l in lines) if (X(r, l)) h++;
        foreach (var l in lines) foreach (var o in obsRects) if (X(l, o) || In(o, l.PointAtStart)) h++;
        // two parallel dimension lines that overlap along their length and sit closer than 1.5 text heights
        foreach (var a in dls) foreach (var o in obsDim) {
          var da = a.Direction; var dob = o.Direction; if (!da.Unitize() || !dob.Unitize()) continue;
          if (Math.Abs(da.X * dob.Y - da.Y * dob.X) > 1e-3) continue;
          var rel = o.From - a.From; double perp = Math.Abs(da.X * rel.Y - da.Y * rel.X);
          double s1 = rel * da, s2 = (o.To - a.From) * da;
          double lo = Math.Max(0, Math.Min(s1, s2)), hi = Math.Min(a.Length, Math.Max(s1, s2));
          if (hi - lo > 0.1 * u && perp < 1.5 * u) h++; }
        return h;
      }
      void Keep(GeometryBase g) { var (lines, rects, raw, dls) = Parts(g); obsLines.AddRange(lines); obsRects.AddRange(rects); obsDim.AddRange(dls); }
      // what is already in the drawing must be avoided too: other curves and existing annotations
      foreach (var o in doc.Objects.GetObjectList(new ObjectEnumeratorSettings { ObjectTypeFilter = ObjectType.Curve })) { if (pickedIds.Contains(o.Id)) continue; var c = o.Geometry as Curve; if (c == null) continue; if (IsCircle(c, out Circle ci) && holes.Any(h => h.Center.DistanceTo(ci.Center) < atol * 10 && Math.Abs(h.Radius - ci.Radius) < atol * 10)) continue; obsLines.Add(c); }
      // ... and what an existing annotation already measures is not measured again (its key, or read from its geometry)
      foreach (var o in doc.Objects.GetObjectList(new ObjectEnumeratorSettings { ObjectTypeFilter = ObjectType.Annotation })) { try { Keep(o.Geometry); } catch { }
        var ks = o.Attributes.GetUserString("relievo_dim");
        if (!string.IsNullOrEmpty(ks)) keys.Add(ks);
        else try {
          if (o.Geometry is LinearDimension xl) { if (xl.Get3dPoints(out Point3d x1, out Point3d x2, out Point3d x3, out Point3d x4, out Point3d x5, out Point3d x6)) { var kk = KLin(x1, x2, x4 - x3, "*"); if (kk != null) keys.Add(kk); } }
          else if (o.Geometry is AngularDimension xa) keys.Add(KPt("ANG", xa.Plane.Origin));
          else if (o.Geometry is RadialDimension xrd) keys.Add(KRad(xrd.Plane.Origin, xrd.IsDiameterDimension ? xrd.NumericValue / 2 : xrd.NumericValue));
        } catch { } }
      Guid AddTo(AnnotationBase a, string key) { a.SetOverrideDimStyle(StyleOf(a)); var oa = new ObjectAttributes { LayerIndex = a is Leader ? (markLeaders.Contains(a) ? layMarks : layTag) : a is TextEntity ? layText : a is RadialDimension ? layMarks : layDim }; if (key != null) oa.SetUserString("relievo_dim", key); return doc.Objects.Add(a, oa); }
      // each candidate that looks clear is put in the model and checked again as Rhino really draws it;
      // the first one with no clash stays (else the one with the fewest). A thing that is already dimensioned is skipped.
      int unresolved = 0;
      void Best(IEnumerable<AnnotationBase> cands, string key = null) { if (Has(key)) { skipped++; return; }
        // a linear dimension that would read 0 at the style's precision is never placed (e.g. a 0.19 chain between two close holes)
        cands = cands.Where(q => !(q is LinearDimension dz && dz.NumericValue < minShow)).ToList();
        Guid bestId = Guid.Empty; int bh = int.MaxValue; var later = new List<(int h, AnnotationBase c)>();
        foreach (var c in cands) { if (c == null) continue; c.SetOverrideDimStyle(StyleOf(c)); int h = Hits(c);
          if (h > 0) { later.Add((h, c)); continue; }
          var id = AddTo(c, key); if (id == Guid.Empty) continue; int hr = Hits(doc.Objects.FindId(id).Geometry);
          if (hr < bh) { if (bestId != Guid.Empty) doc.Objects.Delete(bestId, true); bestId = id; bh = hr; } else doc.Objects.Delete(id, true);
          if (bh == 0) break; }
        // nothing clear yet: the guess can be wrong (Rhino moves a text that does not fit between its extension lines), so the rest is tried for real too
        if (bh != 0) foreach (var lc in later.OrderBy(q => q.h).Take(60)) { var id = AddTo(lc.c, key); if (id == Guid.Empty) continue; int hr = Hits(doc.Objects.FindId(id).Geometry);
          if (hr < bh) { if (bestId != Guid.Empty) doc.Objects.Delete(bestId, true); bestId = id; bh = hr; } else doc.Objects.Delete(id, true);
          if (bh == 0) break; }
        if (bestId != Guid.Empty) { if (bh > 0) unresolved++; made.Add(bestId); Keep(doc.Objects.FindId(bestId).Geometry); if (key != null) keys.Add(key); } }
      // try a whole layout; if anything in it clashes, take it back and try the next one (the one with the fewest clashes stays)
      (int m0, int l0, int r0, int u0, int k0, int s0, int d0) Mark() => (made.Count, obsLines.Count, obsRects.Count, unresolved, keys.Count, skipped, obsDim.Count);
      void Back((int m0, int l0, int r0, int u0, int k0, int s0, int d0) s) { for (int i = made.Count - 1; i >= s.m0; i--) doc.Objects.Delete(made[i], true); made.RemoveRange(s.m0, made.Count - s.m0); obsLines.RemoveRange(s.l0, obsLines.Count - s.l0); obsRects.RemoveRange(s.r0, obsRects.Count - s.r0); obsDim.RemoveRange(s.d0, obsDim.Count - s.d0); keys.RemoveRange(s.k0, keys.Count - s.k0); unresolved = s.u0; skipped = s.s0; }
      void Pick(params Action[] opts) { int bestOpt = -1, fewest = int.MaxValue; for (int i = 0; i < opts.Length; i++) { var s = Mark(); opts[i](); int bad = unresolved - s.u0; if (bad == 0) return; if (bad < fewest) { fewest = bad; bestOpt = i; } Back(s); } if (bestOpt >= 0) opts[bestOpt](); }
      Vector3d Rot(Vector3d v, double ang) { var w = v; w.Rotate(ang, Vector3d.ZAxis); return w; }
      TextEntity Txt(string s, Point3d at, TextJustification j) { var te = TextEntity.Create(s, new Plane(at, Vector3d.XAxis, Vector3d.YAxis), st, false, 0, 0); if (te != null) { te.SetOverrideDimStyle(ov); te.Justification = j; } return te; }
      string Lab(int i) => i < 26 ? ((char)('A' + i)).ToString() : "P" + (i + 1);
      LinearDimension H(Point3d a, Point3d b, double y) => LinearDimension.Create(AnnotationType.Rotated, st, xy, Vector3d.XAxis, a, b, new Point3d((a.X + b.X) / 2, y, 0), 0);
      LinearDimension V(Point3d a, Point3d b, double x) => LinearDimension.Create(AnnotationType.Rotated, st, xy, Vector3d.XAxis, a, b, new Point3d(x, (a.Y + b.Y) / 2, 0), Math.PI / 2);
      List<Point3d> Uniq(IEnumerable<Point3d> pts, Func<Point3d, double> key) { var r = new List<Point3d>(); foreach (var p in pts.OrderBy(key)) if (r.Count == 0 || key(p) - key(r[r.Count - 1]) > mtol) r.Add(p); return r; }
      // more rows than before: a dimension that is crossed by the extension lines of others can move further out
      var rows = new[] { 0.0, 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0, 9.0, 10.0, 11.0, 12.0 };
      // the shape being dimensioned
      Curve cur = null; bool closedCur = false; Point3d cen = Point3d.Origin;
      bool Inside(Point3d q) => closedCur && cur.Contains(q, xy, tol) == PointContainment.Inside;
      LinearDimension Al(Point3d a, Point3d b, double off) { var x = b - a; if (x.Length < mtol) return null; x.Unitize(); var y = Vector3d.CrossProduct(Vector3d.ZAxis, x); var mm = (a + b) / 2; if (closedCur ? Inside(mm + y * u * 0.1) : (mm - cen) * y < 0) y = -y; return LinearDimension.Create(AnnotationType.Aligned, st, new Plane(a, x, y), x, a, b, mm + y * off, 0); }
      // the same, with the text slid along the dimension line (out of the way of a crossing extension line)
      LinearDimension Sh(LinearDimension d, double shift) { if (d == null) return null; try { var t0 = d.TextPosition; d.TextPosition = new Point2d(t0.X + shift, t0.Y); } catch { return null; } return d; }
      LinearDimension AlS(Point3d a, Point3d b, double off, double shift) => Sh(Al(a, b, off), shift);
      // a row of candidates for a chain dimension: text centred, then slid inside its own span (clear of the extension lines of other rows), then put out past either end
      IEnumerable<AnnotationBase> RowC(Func<double, LinearDimension> mk, double len) {
        foreach (var f in rows) yield return mk(f);
        if (len > 3.2 * TW("0000.00")) foreach (var f in rows.Take(7)) foreach (var sh in new[] { 0.22, -0.22, 0.34, -0.34 }) yield return Sh(mk(f), sh * len);
        foreach (var f in rows.Take(5)) foreach (var s3 in new[] { -1.0, 1.0 }) yield return Sh(mk(f), s3 * (len / 2 + 3.2 * u)); }
      BoundingBox mainRing = bbAll; Point3d mL = bbAll.Min, mR = bbAll.Max, mB = bbAll.Min, mT = bbAll.Max; string diagNote = null;
      // holes: X/Y position chains, sx: +1 top / -1 bottom, sy: -1 left / +1 right
      void HoleChains(double sx, double sy) {
        var hc = holes.Select(h => h.Center).ToList();
        var fx = main != null ? new[] { mL, mR }.Concat(hc) : hc; var fy = main != null ? new[] { mB, mT }.Concat(hc) : hc;
        var hx = Uniq(fx, p => p.X); var hy = Uniq(fy, p => p.Y);
        double y0 = sx > 0 ? mainRing.Max.Y + gap : mainRing.Min.Y - gap; for (int i = 0; i < hx.Count - 1; i++) Best(RowC(f => H(hx[i], hx[i + 1], y0 + sx * f * gap), Math.Abs(hx[i + 1].X - hx[i].X)), KLin(hx[i], hx[i + 1], Vector3d.XAxis, sid));
        double x0 = sy < 0 ? mainRing.Min.X - gap : mainRing.Max.X + gap; for (int i = 0; i < hy.Count - 1; i++) Best(RowC(f => V(hy[i], hy[i + 1], x0 + sy * f * gap), Math.Abs(hy[i + 1].Y - hy[i].Y)), KLin(hy[i], hy[i + 1], Vector3d.YAxis, sid));
      }
      void Do(Curve cIn) {
        // a closed shape that is not exactly flat is measured on its flat (XY) copy, so it still counts as closed
        bool isMain0 = cIn == main; var c = cIn.IsPlanar() ? cIn : (Curve.ProjectToPlane(cIn, xy) ?? cIn);
        cur = c; closedCur = c.IsClosed; var cbb = c.GetBoundingBox(true); cen = cbb.Center; sid = $"{Q(cen.X)}_{Q(cen.Y)}";
        var segs = new List<Curve>();
        if (c.TryGetPolyline(out Polyline pl)) { for (int i = 0; i < pl.SegmentCount; i++) segs.Add(new LineCurve(pl.SegmentAt(i))); }
        else { var d = c.DuplicateSegments(); if (d != null && d.Length > 0) segs.AddRange(d); else segs.Add(c); }
        int m = segs.Count; bool allLinear = segs.All(s => s.IsLinear(mtol));
        // 1. every segment: line = true length, arc = radius, free curve = length note
        void Segs() { foreach (var s in segs) {
          var a = s.PointAtStart; var b = s.PointAtEnd;
          if (s.IsLinear(mtol)) { double len = a.DistanceTo(b); if (len < minShow) continue; var cl = new List<AnnotationBase>();
            foreach (var f in new[] { 1.0, 1.4, 1.8, 2.4, 3.2, 4.2 }) cl.Add(Al(a, b, sideOff * f));
            if (len > 3.2 * TW("0000.00")) foreach (var f in new[] { 1.0, 1.8 }) foreach (var sh in new[] { 0.22, -0.22, 0.34, -0.34 }) cl.Add(AlS(a, b, sideOff * f, sh * len));
            foreach (var f in new[] { 1.0, 1.8, 3.2 }) foreach (var s2 in new[] { -1.0, 1.0 }) cl.Add(AlS(a, b, sideOff * f, s2 * (len / 2 + 3.2 * u)));
            foreach (var f in new[] { -1.0, -1.6, -2.4 }) cl.Add(Al(a, b, sideOff * f));
            Best(cl, KLin(a, b, b - a, sid)); }
          else if (s.TryGetArc(out Arc arc, mtol)) { var mid = arc.MidPoint; var dv = mid - arc.Center; dv.Unitize(); var cands = new List<AnnotationBase>(); foreach (var lf in new[] { 3.0, 5.0, 7.0, 9.0, 12.0 }) foreach (var sg in new[] { 1.0, -1.0 }) cands.Add(RadialDimension.Create(st, AnnotationType.Radius, xy, arc.Center, mid, mid + dv * sg * lf * u)); Best(cands, KRad(arc.Center, arc.Radius)); }
          else { s.NormalizedLengthParameter(0.5, out double tm); var mp = s.PointAt(tm); var nv = Vector3d.CrossProduct(Vector3d.ZAxis, s.TangentAt(tm)); nv.Unitize(); if (closedCur ? Inside(mp + nv * u * 0.1) : (mp - cen) * nv < 0) nv = -nv; var txt = $"L = {s.GetLength():F2}"; Best(new[] { 2.0, 3.5, 5.0, 7.0, 10.0, 14.0 }.SelectMany(f => new[] { f, -f }).Select(f => (AnnotationBase)Txt(txt, mp + nv * f * u, TextJustification.MiddleCenter)), KPt("LEN", mp)); }
        } }
        // first pass only measures how far the segment dimensions reach (the ring), then it is taken back
        var s0 = Mark(); Segs(); var ring = cbb; for (int i = s0.m0; i < made.Count; i++) ring.Union(doc.Objects.FindId(made[i]).Geometry.GetBoundingBox(true)); Back(s0);
        // 2. corner position chains + overall, outside the ring
        var vpts = segs.Select(s => s.PointAtStart).ToList(); if (!closedCur) vpts.Add(segs[m - 1].PointAtEnd);
        var ext = new List<Point3d>(vpts);
        foreach (var dvec in new[] { Vector3d.XAxis, Vector3d.YAxis }) { try { var ps = c.ExtremeParameters(dvec); if (ps != null) foreach (var t in ps) ext.Add(c.PointAt(t)); } catch { } }
        var L = ext.OrderBy(q => q.X).First(); var R = ext.OrderBy(q => q.X).Last(); var B = ext.OrderBy(q => q.Y).First(); var T = ext.OrderBy(q => q.Y).Last();
        bool Clear(Point3d p, Vector3d dirv) { var seg = new LineCurve(p + dirv * u * 0.05, p + dirv * size * 3); return Rhino.Geometry.Intersect.Intersection.CurveCurve(seg, c, tol, tol).Count == 0; }
        bool single = m == 1 && allLinear; var hUsed = new List<Point3d>(); var vUsed = new List<Point3d>();
        void HRows(double sg) { var px = Uniq(vpts.Where(p => Clear(p, Vector3d.YAxis * sg)), p => p.X); hUsed = px.Count > 2 ? px : new List<Point3d> { L, R }; double y0 = sg < 0 ? ring.Min.Y - gap : ring.Max.Y + gap;
          if (px.Count > 2) for (int i = 0; i < px.Count - 1; i++) Best(RowC(f => H(px[i], px[i + 1], y0 + sg * f * gap), Math.Abs(px[i + 1].X - px[i].X)), KLin(px[i], px[i + 1], Vector3d.XAxis, sid));
          double yo = px.Count > 2 ? y0 + sg * gap : y0;
          if (R.X - L.X > mtol && !(single && T.Y - B.Y < mtol)) Best(rows.Select(f => (AnnotationBase)H(L, R, yo + sg * f * gap)), KLin(L, R, Vector3d.XAxis, sid)); }
        void VRows(double sg) { var py = Uniq(vpts.Where(p => Clear(p, Vector3d.XAxis * sg)), p => p.Y); vUsed = py.Count > 2 ? py : new List<Point3d> { B, T }; double x0 = sg > 0 ? ring.Max.X + gap : ring.Min.X - gap;
          if (py.Count > 2) for (int i = 0; i < py.Count - 1; i++) Best(RowC(f => V(py[i], py[i + 1], x0 + sg * f * gap), Math.Abs(py[i + 1].Y - py[i].Y)), KLin(py[i], py[i + 1], Vector3d.YAxis, sid));
          double xo = py.Count > 2 ? x0 + sg * gap : x0;
          if (T.Y - B.Y > mtol && !(single && R.X - L.X < mtol)) Best(rows.Select(f => (AnnotationBase)V(B, T, xo + sg * f * gap)), KLin(B, T, Vector3d.YAxis, sid)); }
        // 2b. the other two sides (top / left when the chains are bottom / right): the corners the first chains missed -
        // a chain end to end through the ones in clear view (with the overall when it has 3+ pieces), and one dimension from the start end to each corner hidden behind the shape
        void OppRows(bool horiz, double sg) {
          var used = horiz ? hUsed : vUsed; Func<Point3d, double> key = horiz ? (p => p.X) : (p => p.Y);
          var dirv = horiz ? Vector3d.YAxis * sg : Vector3d.XAxis * sg; var ax = horiz ? Vector3d.XAxis : Vector3d.YAxis;
          var miss = vpts.Where(p => !used.Any(q => Math.Abs(key(q) - key(p)) < mtol)).ToList(); if (miss.Count == 0) return;
          var p0 = horiz ? L : B; var p1 = horiz ? R : T;
          var pc = Uniq(new[] { p0, p1 }.Concat(miss.Where(p => Clear(p, dirv))), key);
          var hid = Uniq(miss.Where(p => !pc.Any(q => Math.Abs(key(q) - key(p)) < mtol)), key);
          double s0 = horiz ? (sg < 0 ? ring.Min.Y - gap : ring.Max.Y + gap) : (sg > 0 ? ring.Max.X + gap : ring.Min.X - gap);
          LinearDimension DL(Point3d a, Point3d b, double at) => horiz ? H(a, b, at) : V(a, b, at);
          if (pc.Count > 2) for (int i = 0; i < pc.Count - 1; i++) { var a = pc[i]; var b = pc[i + 1]; Best(RowC(f => DL(a, b, s0 + sg * f * gap), Math.Abs(key(b) - key(a))), KLin(a, b, ax, sid)); }
          if (pc.Count > 3) Best(rows.Select(f => (AnnotationBase)DL(p0, p1, s0 + sg * (f + 1) * gap)), KLin(p0, p1, ax, sid + "o"));
          foreach (var p in hid) Best(rows.Select(f => (AnnotationBase)DL(p0, p, s0 + sg * f * gap)), KLin(p0, p, ax, sid));
        }
        bool isMain = isMain0; if (isMain) { mainRing = ring; mL = L; mR = R; mB = B; mT = T; mainSid = sid; }
        // 3. angle at every bend (inside for closed shapes), corner labels on a closed 4-sided outline
        bool labels = isMain && closedCur && m == 4 && allLinear;
        void Angles() { for (int i = 0; i < m; i++) {
          if (!closedCur && i == 0) continue;
          var prev = segs[(i - 1 + m) % m]; var next = segs[i]; var p = next.PointAtStart;
          var u1 = -prev.TangentAtEnd; var u2 = next.TangentAtStart;
          double A = Vector3d.VectorAngle(u1, u2); if (A > Math.PI - 0.005 || A < 0.005) continue;
          double sgn = Math.Sign(Vector3d.CrossProduct(u1, u2).Z); if (sgn == 0) continue;
          double minEdge = Math.Min(prev.GetLength(), next.GetLength());
          var dir = Rot(u1, sgn * A / 2); if (closedCur && !Inside(p + dir * u * 0.5)) { var t = u1; u1 = u2; u2 = t; A = 2 * Math.PI - A; sgn = -sgn; dir = -dir; }
          double r0 = Math.Max(Math.Max(0.15 * minEdge, 4 * u), TW("000.00°") * 1.3 / Math.Max(A, 0.05));
          // many radii: a bigger arc is flatter, so it no longer cuts through its own text
          var rs = new List<double>(); foreach (var f in new[] { 1.0, 1.3, 1.6, 1.9, 2.2, 2.6, 3.0, 3.5, 4.0, 4.6, 5.3, 6.0, 0.8, 0.65 }) { double rr = Math.Min(r0 * f, 0.45 * minEdge); if (!rs.Any(q => Math.Abs(q - rr) < mtol)) rs.Add(rr); }
          // a right angle (90 / 270) is not dimensioned - square corners are understood
          if (Math.Abs(Math.Min(A, 2 * Math.PI - A) - Math.PI / 2) > 0.0002) Best(rs.Select(rq => (AnnotationBase)AngularDimension.Create(st, xy, Vector3d.XAxis, p, p + u1 * rq, p + u2 * rq, p + dir * rq)), KPt("ANG", p));
          if (labels) Best(new[] { 1.6, 2.4, 3.4, 4.5, 6.0, 8.0 }.Select(f => (AnnotationBase)Txt(Lab(i), p - dir * f * u, TextJustification.MiddleCenter)), KPt("LAB", p));
        }
        if (labels) diagNote = $"DIAGONALS: A-C = {vpts[0].DistanceTo(vpts[2]):F2}   B-D = {vpts[1].DistanceTo(vpts[3]):F2}"; }
        // the long chains go in first, then the segment dimensions and angles find room around their extension lines;
        // chains: bottom + right, holes: top + left - other sides are tried when something would overlap
        bool withHoles = isMain && holes.Count > 0;
        void Layout(double hs, double vs, double hxs, double hys) { HRows(hs); VRows(vs); OppRows(true, -hs); OppRows(false, -vs); if (withHoles) HoleChains(hxs, hys); Segs(); Angles(); }
        var cornerSides = new[] { (-1.0, 1.0), (1.0, 1.0), (-1.0, -1.0), (1.0, -1.0) };
        var holeSides = withHoles ? new[] { (1.0, -1.0), (1.0, 1.0), (-1.0, -1.0), (-1.0, 1.0) } : new[] { (1.0, -1.0) };
        var opts = new List<Action>();
        foreach (var csd in cornerSides) foreach (var hsd in holeSides) opts.Add(() => Layout(csd.Item1, csd.Item2, hsd.Item1, hsd.Item2));
        Pick(opts.ToArray());
      }
      foreach (var c in shapes) Do(c);
      // 4. holes: a diameter on a hole that is the only one of its size; holes of the same size are counted and get ONE leader
      if (holes.Count > 0) {
        if (main == null) { mainRing = BoundingBox.Empty; foreach (var h in holes) mainRing.Union(h.BoundingBox); sid = $"{Q(mainRing.Center.X)}_{Q(mainRing.Center.Y)}"; Pick(() => HoleChains(1, -1), () => HoleChains(1, 1), () => HoleChains(-1, -1), () => HoleChains(-1, 1)); }
        var degs = new[] { 45, 135, 315, 225, 60, 120, 300, 240, 30, 150, 330, 210, 90, 270, 0, 180 };
        string DiaTxt(Circle hh) => (2 * hh.Radius).ToString("F" + Math.Max(0, ov.LengthResolution));
        foreach (var hg in holes.GroupBy(DiaTxt)) { var hs = hg.ToList(); var cands = new List<AnnotationBase>();
          if (hs.Count == 1) { var h1 = hs[0];
            foreach (var lf in new[] { 3.0, 5.0, 7.0, 9.0, 12.0 }) foreach (var deg in degs) { var dv = Rot(Vector3d.XAxis, RhinoMath.ToRadians(deg)); cands.Add(RadialDimension.Create(st, AnnotationType.Diameter, xy, h1.Center, h1.Center + dv * h1.Radius, h1.Center + dv * (h1.Radius + lf * u))); }
            Best(cands, KRad(h1.Center, h1.Radius)); }
          else { var h1 = hs.OrderByDescending(hh => hh.Center.Y).ThenBy(hh => hh.Center.X).First(); string cnt = $"{hs.Count} HOLES @ Ø{hg.Key}";
            foreach (var lf in new[] { 4.0, 6.0, 8.0, 11.0 }) foreach (var deg in degs) { var dv = Rot(Vector3d.XAxis, RhinoMath.ToRadians(deg)); var pt = h1.Center + dv * h1.Radius; var ldr = Leader.Create(cnt, xy, st, new[] { pt, pt + dv * lf * u }); if (ldr != null) { markLeaders.Add(ldr); cands.Add(ldr); } }
            Best(cands, $"CNT|{Q(h1.Center.X)}|{Q(h1.Center.Y)}|{cnt}"); }
        }
      }
      // 5. name tags: one leader (with landing line) per object name - a name found on several curves is tagged once, curves without a name get none
      int tags = 0; var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach (var (nm, cv) in named) {
        if (string.IsNullOrWhiteSpace(nm) || !seen.Add(nm.Trim())) continue;
        var cands = new List<AnnotationBase>(); bool circ = IsCircle(cv, out Circle cc0);
        foreach (var tn in new[] { 0.5, 0.35, 0.65, 0.2, 0.8 }) {
          Point3d pt; Vector3d n0;
          if (circ) { n0 = Rot(Vector3d.XAxis, 2 * Math.PI * tn + Math.PI * 0.75); pt = cc0.Center + n0 * cc0.Radius; }
          else { pt = cv.PointAtNormalizedLength(tn); if (!cv.ClosestPoint(pt, out double t9)) continue; n0 = Vector3d.CrossProduct(Vector3d.ZAxis, cv.TangentAt(t9)); if (!n0.Unitize()) continue; if (!InMain(pt + n0 * u * 0.2)) n0 = -n0; }
          foreach (var sd in circ ? new[] { 1.0 } : new[] { 1.0, -1.0 }) foreach (var lf in new[] { 4.0, 6.0, 8.0, 11.0 }) foreach (var da in new[] { 0.0, 0.5, -0.5, 0.9, -0.9 }) { var dv = Rot(n0 * sd, da); cands.Add(Leader.Create(nm.Trim(), xy, st, new[] { pt, pt + dv * lf * u })); }
        }
        int before = made.Count; var bc = cv.GetBoundingBox(true).Center; Best(cands, $"TAG|{nm.Trim()}|{Q(bc.X)}|{Q(bc.Y)}"); if (made.Count > before) tags++;
      }
      // 6. diagonals as a note (no dimension line through the corner angles)
      if (diagNote != null) { var all = bbAll; foreach (var id in made) all.Union(doc.Objects.FindId(id).Geometry.GetBoundingBox(true)); Best(new[] { 1.5, 3.0, 4.5, 6.0, 8.0, 11.0 }.Select(f => (AnnotationBase)Txt(diagNote, new Point3d(all.Min.X, all.Min.Y - f * u, 0), TextJustification.TopLeft)), "DIAG|" + mainSid); }
      // 7. final clean-up of the whole layout: every dimension text that still lies on a line or on another text is slid along its own
      // dimension line (anywhere between and past the extension lines) or to the other side of it, to the nearest free spot
      var dimIds = made.Where(g => doc.Objects.FindId(g)?.Geometry is Dimension).Distinct().ToList();
      int nudged = 0, stuckN = 0;
      if (dimIds.Count > 1) {
        var dimSet = new HashSet<Guid>(dimIds);
        var obstacles = new List<Curve>();
        foreach (var o in doc.Objects.GetObjectList(new ObjectEnumeratorSettings { ObjectTypeFilter = ObjectType.Curve })) if (!dimSet.Contains(o.Id) && o.Geometry is Curve oc) obstacles.Add(oc);
        PolylineCurve TBox(Dimension d) { Point3d[] cs = null; bool ok = false; try { if (d is LinearDimension l) ok = l.GetTextRectangle(out cs); else if (d is AngularDimension a) ok = a.GetTextRectangle(out cs); } catch { } if (!ok || cs == null) return null; double sc = d.DimensionScale; var tp = d.TextPosition; var w = cs.Select(p => d.Plane.PointAt(tp.X + p.X * sc, tp.Y + p.Y * sc)).ToList(); w.Add(w[0]); return new PolylineCurve(w); }
        // the lines and the text box of every dimension are measured once and kept (a moved text is measured again)
        var cacheL = new Dictionary<Guid, List<Curve>>(); var cacheB = new Dictionary<Guid, PolylineCurve>();
        void Refresh(Guid id) { var d = (Dimension)doc.Objects.FindId(id).Geometry; var ls = new List<Curve>(); foreach (var e in d.Explode() ?? new GeometryBase[0]) if (e is Curve ec && ec.GetLength() > mtol) ls.Add(ec); cacheL[id] = ls; cacheB[id] = TBox(d); }
        foreach (var id in dimIds) Refresh(id);
        bool ClashBox(Guid self, PolylineCurve r) { if (r == null) return false; foreach (var c in obstacles) if (X(r, c)) return true; foreach (var o in dimIds) { if (o == self) continue; foreach (var ec in cacheL[o]) if (X(r, ec)) return true; var orr = cacheB[o]; if (orr != null && X(r, orr)) return true; } return false; }
        for (int pass = 0; pass < 4; pass++) { bool ch = false;
          foreach (var id in dimIds) {
            var b0 = cacheB[id]; if (b0 == null || !ClashBox(id, b0)) continue;
            var d = (Dimension)doc.Objects.FindId(id).Geometry; var orig = d.TextPosition; double sc = d.DimensionScale; Point3d[] cs = null;
            try { if (d is LinearDimension l0) l0.GetTextRectangle(out cs); else if (d is AngularDimension a0) a0.GetTextRectangle(out cs); } catch { }
            if (cs == null) continue;
            double w = (cs.Max(p => p.X) - cs.Min(p => p.X)) * sc, h = (cs.Max(p => p.Y) - cs.Min(p => p.Y)) * sc; double len = 0;
            if (d is LinearDimension l1 && l1.Get3dPoints(out Point3d q1, out Point3d q2, out Point3d q3, out Point3d q4, out Point3d q5, out Point3d q6)) len = q3.DistanceTo(q4);
            int nst = (int)Math.Min(40, Math.Ceiling((len / 2 + 1.5 * w) / (0.5 * w)));
            var offs = new List<(double dx, double dy)>();
            foreach (var dy in new[] { 0.0, 1.5, -1.5, 3.0, -3.0 }) for (int i = -nst; i <= nst; i++) { if (i == 0 && dy == 0) continue; offs.Add((i * 0.5, dy)); }
            bool done = false; var pl0 = d.Plane;
            foreach (var cand in offs.OrderBy(q => Math.Abs(q.dx) + 2 * Math.Abs(q.dy))) {
              var tv = pl0.XAxis * (cand.dx * w) + pl0.YAxis * (cand.dy * h);
              var bx = (PolylineCurve)b0.DuplicateCurve(); bx.Translate(tv);
              if (ClashBox(id, bx)) continue;
              var nd = (Dimension)d.Duplicate(); nd.TextPosition = new Point2d(orig.X + cand.dx * w, orig.Y + cand.dy * h);
              doc.Objects.Replace(id, (GeometryBase)nd, true); Refresh(id); done = true; break; }
            if (done) { nudged++; ch = true; }
          }
          if (!ch) break; }
        stuckN = dimIds.Count(id => ClashBox(id, cacheB[id]));
      }
      Report = $"Added {made.Count} annotations for {shapes.Count} shape(s) and {holes.Count} hole(s), {tags} name tag(s) (text {u:F0} mm, leader landing {landing:0.##} mm) on layers Drawing::Dimension / Text / Marks / Tag" + (unresolved == 0 ? ", no overlaps." : $"; {unresolved} could not be placed fully clear - check them.") + (nudged > 0 ? $" {nudged} text(s) moved off lines and other text." : "") + (stuckN > 0 ? $" {stuckN} text(s) still clash - check them." : "") + (skipped > 0 ? $" {skipped} already dimensioned - not repeated." : "") + (bake ? " Baked." : " Preview only - set Bake to true to put them in the model.");
    }
    return Report;
  }

  private readonly List<AnnotationBase> _prev = new List<AnnotationBase>();
  private BoundingBox _box = BoundingBox.Empty;
  // the Dims output is previewed by Grasshopper itself (selection colour, Preview on/off)
}
