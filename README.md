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
