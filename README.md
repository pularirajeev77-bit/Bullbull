# Bullbull - `display` branch

C# script components for **Rhino 8 Grasshopper**: viewport display tools.

Other branches: [`TEXT`](https://github.com/pularirajeev77-bit/Bullbull/tree/TEXT) (text tools) &middot;
[`Vector`](https://github.com/pularirajeev77-bit/Bullbull/tree/Vector) (polyline frames) &middot;
[`curves`](https://github.com/pularirajeev77-bit/Bullbull/tree/curves) (curve tools) &middot;
[`utility`](https://github.com/pularirajeev77-bit/Bullbull/tree/utility) (general utilities) &middot;
[`point`](https://github.com/pularirajeev77-bit/Bullbull/tree/point) (point tools) &middot;
[`surface`](https://github.com/pularirajeev77-bit/Bullbull/tree/surface) (surface tools) &middot;
[`Plane`](https://github.com/pularirajeev77-bit/Bullbull/tree/Plane) (plane tools) &middot;
[`Layers`](https://github.com/pularirajeev77-bit/Bullbull/tree/Layers) (layer tools) &middot;
[`main`](https://github.com/pularirajeev77-bit/Bullbull/tree/main) (overview)

## How to use a script

1. In Grasshopper, place a **C# Script** component (Maths > Script).
2. Open its editor and replace everything with the contents of the `.cs` file.
3. The component takes its inputs and outputs from the `RunScript(...)` line.
   If they don't appear, add them by hand with the exact same names.

**Access types:** **item** = one value at a time &middot; **list** = the whole list at once.

## Components

| Component | File | What it does |
|---|---|---|
| [Zebra](#zebra) | `ZebraAnalysis.cs` | Zebra-stripe surface analysis in the viewport |
| [GoldEmap](#goldemap) | `BrushedGoldEmap.cs` | Brushed-gold reflection map on geometry |
| [CrvProp](#crvprop) | `CurveDetails.cs` | Reports a curve's length, domain, type and more |
| [DynSplit](#dynsplit) | `DynamicSectionSplit.cs` | Live section: slide/rotate a cutting plane, get section curves and the kept side |

---

## Zebra

**File:** `ZebraAnalysis.cs`

Shades geometry with a striped **zebra environment map** in the viewport, so you
can read surface curvature and check continuity (G0/G1/G2) between faces - the
classic zebra analysis, driven from Grasshopper. It is **display only**: no
Grasshopper outputs, and it deliberately draws no wires or mesh edges.

| Input | Access | Meaning |
|---|---|---|
| `Geometry` | list | Breps or Meshes to analyze |
| `Horizontal` | item | True = horizontal stripes, False = vertical |
| `StripeCount` | item | Number of stripe pairs (finer = more stripes). 0 or less uses 20 |

*No outputs - the result is drawn straight into the viewport.*

**Good to know**
- Smooth, evenly flowing stripes mean smooth curvature; kinks or sudden jumps
  in the stripes reveal creases or tangency breaks between surfaces.
- Breps are meshed at high quality for the analysis; only Breps and Meshes are
  used (other geometry is skipped with a warning).
- Each component writes its own texture, so several Zebra components can run at
  once without clashing (an earlier version shared one texture file).
- Turn stripes horizontal or vertical to check curvature in different directions.

---

## GoldEmap

**File:** `BrushedGoldEmap.cs`

Shades geometry with Rhino's native **brushed-gold environment map**, so
reflections streak across the surface and reveal curvature and continuity - a
quick "is this smooth?" check, and it just looks good for presentation. It is
**display only**: no outputs, and it draws no wires or edges.

| Input | Access | Meaning |
|---|---|---|
| `Geometry` | list | Breps or Meshes to shade |

*No outputs - the result is drawn straight into the viewport.*

**Good to know**
- Uses Rhino 8's own `brushed_gold.jpg`. It looks in the English install folder
  first, then searches the other language folders, so non-English Rhino works
  too. If the file isn't found it warns and does nothing.
- Breps are meshed at high quality; only Breps and Meshes are used (others are
  skipped with a warning).
- Smooth, continuous reflection streaks mean smooth surfaces; broken or kinked
  streaks show creases or tangency breaks.

---

## CrvProp

**File:** `CurveDetails.cs`

Reads out the **properties of a curve** - length, domain, key points, NURBS
structure and type - as text/values you can panel out or use downstream.

| Input | Access | Meaning |
|---|---|---|
| `Curve` | item | The curve to inspect |

| Output | Meaning |
|---|---|
| `Length` | Length of the curve |
| `Domain` | Parameter domain (start..end) |
| `KeyPoints` | Key points: start, middle, end |
| `Structure` | NURBS structure: degree and span count |
| `Info` | Flags: closed, periodic, planar |
| `Type` | Object type and .NET class name |

**Good to know**
- Planarity is checked at the model tolerance (with a fallback when no document
  is open, so it no longer crashes headless).
- This is a data/reporting tool rather than a viewport display; it lives here on
  the `display` branch by request, but fits the `curves` branch topically - say
  the word to move it.

---

## DynSplit

**File:** `DynamicSectionSplit.cs` &middot; Component name: *Dynamic Section Split*

A **live section tool**. A cutting plane slides over the geometry's bounding box
and rotates about that point; you get the **section curves** and the geometry
**kept on one side** of the plane (capped where possible, so it reads as a solid
cut). Drive `Location` with an **MD Slider** and `Rotation` with sliders to scrub
through a model.

| Input | Access | Meaning |
|---|---|---|
| `Geometry` | list | Breps, Extrusions, Surfaces or Meshes |
| `Location` | item | X, Y from 0 to 1 across the bounding box (Z ignored - plane at box centre height) |
| `Rotation` | item | Rotation about world X, Y, Z in multiples of pi (`0.5` = 90 deg). `0,0,0` = horizontal plane |
| `KeepSide` | item | `0` = keep the side behind the plane normal, `1` = in front (even / odd) |

| Output | Meaning |
|---|---|
| `Sections` | Section curves |
| `SplitGeometry` | Kept part of each object; the whole object if the plane misses it |
| `CutPlane` | The cutting plane |

**Good to know**
- **Side, not index:** `Brep.Split` / `Mesh.Split` return the pieces in no fixed
  order, so `KeepIndex` could flip sides as the plane moved, and with more than two
  pieces it kept just one of them. Pieces are now chosen by which **side** of the
  plane they are on, and every piece on that side is kept.
- **Capped:** kept Brep pieces are capped where planar, so the cut shows as solid
  (before they were open surfaces).
- **Cutter size** follows the geometry (the fixed +-100 000 cutter missed very large
  models).
- Extrusions and Surfaces are accepted (before they were "unsupported"); unsupported
  objects give one warning with a count instead of one per object.
- Empty/invalid geometry and no open document are handled without crashing.
- Renamed `Angle, KeepIndex` / `Section, SplitGeo` -> `Rotation, KeepSide` /
  `Sections, SplitGeometry`; metadata and tooltips set once.
- Not compiled here (no Rhino) - test in Grasshopper.
