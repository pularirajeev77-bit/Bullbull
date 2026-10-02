# Bullbull - `surface` branch

C# script components for **Rhino 8 Grasshopper**: surface and Brep tools.

Other branches: [`TEXT`](https://github.com/pularirajeev77-bit/Bullbull/tree/TEXT) (text tools) &middot;
[`Vector`](https://github.com/pularirajeev77-bit/Bullbull/tree/Vector) (polyline frames) &middot;
[`curves`](https://github.com/pularirajeev77-bit/Bullbull/tree/curves) (curve tools) &middot;
[`utility`](https://github.com/pularirajeev77-bit/Bullbull/tree/utility) (general utilities) &middot;
[`point`](https://github.com/pularirajeev77-bit/Bullbull/tree/point) (point tools) &middot;
[`main`](https://github.com/pularirajeev77-bit/Bullbull/tree/main) (overview)

## How to use a script

1. In Grasshopper, place a **C# Script** component (Maths > Script).
2. Open its editor and replace everything with the contents of the `.cs` file.
3. The component takes its inputs and outputs from the `RunScript(...)` line.
   If they don't appear, add them by hand with the exact same names.

**Access types:** **item** = one value at a time &middot; **list** = the whole list at once &middot; **tree** = the whole data tree at once.

## Components

| Component | File | What it does |
|---|---|---|
| [EdgeAn](#edgean) | `EdgeAnalyzer.cs` | Sorts a Brep's edges: outer, inner, naked, interior, non-manifold |
| [DraftThick](#draftthick) | `DraftedThicken.cs` | Thickens a surface/polysurface/closed brep along its normals with a draft angle |
| [SrfExt](#srfext) | `SurfaceExtender.cs` | Extends surfaces by a distance on each side (N/E/S/W) |
| [SmoothGeo](#smoothgeo) | `SmoothGeo.cs` | Smooths meshes, curves and surfaces (Rhino Smooth) with axis / coordinate control |
| [BlendAnalyze](#blendanalyze) | `BlendEdgeAnalyzer.cs` | Blend surface between two brep edges (G0-G2) + its CVs, weights, Greville |

---

## EdgeAn

**File:** `EdgeAnalyzer.cs`

Analyzes the **edges** of a Brep (surface or polysurface) and sorts them into
groups - useful for finding open borders, holes, or bad (non-manifold) edges.

> **Set the `Breps` input to Tree access** (right-click the input > Tree). On
> Item access, Grasshopper runs the script once per Brep and you get one flat
> branch instead of a result per input branch.

| Input | Access | Meaning |
|---|---|---|
| `Breps` | tree | The Breps to analyze; one result branch per input branch |

| Output | Meaning |
|---|---|
| `Outer` | Exterior naked edges - the outside border |
| `Inner` | Interior naked edges - hole borders |
| `Naked` | All naked edges (on only one face), one curve per edge |
| `Interior` | Interior edges shared between two faces |
| `NonManifold` | Edges shared by 3+ faces (usually a modelling error) |
| `NakedIdx` | Edge index of each `Naked` curve (1:1) |
| `InteriorIdx` | Edge index of each `Interior` curve (1:1) |
| `NonManifoldIdx` | Edge index of each `NonManifold` curve (1:1) |

**Good to know**
- `Outer` + `Inner` together are the naked edges, but they come out **joined**
  into fewer curves, while `Naked` lists them **one curve per edge** alongside
  its `NakedIdx`. Use whichever suits the next step.
- `NakedIdx` / `InteriorIdx` / `NonManifoldIdx` are BrepEdge indices, for
  feeding back into other edge tools.
- A clean, closed solid has no naked edges; naked edges mean an open border or
  hole. Non-manifold edges usually point to a modelling problem.
- Accepts anything Grasshopper can turn into a Brep (surfaces, boxes,
  extrusions, SubD, ...).

---

## DraftThick

**File:** `DraftedThicken.cs`

Thickens a **surface, polysurface or closed brep** along its local normals and
gives the open edges a **draft angle** (walls lean inward or outward), returning
a solid. A closed brep becomes a hollow shell (outer + inner skin).

| Input | Access | Meaning |
|---|---|---|
| `Surface` | item | Surface, polysurface or closed brep to thicken |
| `Height` | item | Thickness along the local normal (negative = other side) |
| `Angle` | item | Draft angle in degrees (0 = walls normal to the surface) |
| `FlipSide` | item | False = draft inward, True = draft outward |
| `MiterLimit` | item | Max corner overshoot x draft offset (0 = off, try 2) |
| `AutoFit` | item | Reduce the draft automatically if the top folds over |
| `MergeFaces` | item | Merge coplanar faces in the result |
| `FastZero` | item | Use Rhino's own offset when `Angle` = 0 (open breps) |
| `WallsOnly` | item | Output only the side walls (no base / top cap) |

| Output | Meaning |
|---|---|
| `Solid` | Drafted solid, hollow solid for closed input, or walls only |

**Good to know**
- Closed breps ignore `Angle` / `FlipSide` (there are no open edges).
- The output was renamed from `Brep` to `Solid` so the Brep type can be used
  freely inside the script; the input `Breps` became `Surface` (it is one item).
- Warnings and remarks (folded top, join tolerance, invalid result) appear as
  runtime messages on the component.
- Not compiled here (no Rhino) - test in Grasshopper and report any odd cases.

---

## SrfExt

**File:** `SurfaceExtender.cs`

Extends one or more **surfaces** (or every face of a Brep) by a distance on each
side, keeping the surface smooth (curvature-continuous extension). Can also give
you the frame at the centre of each original surface.

| Input | Access | Meaning |
|---|---|---|
| `Surfaces` | list | Surfaces or Breps (each face is extended) |
| `North` | item | Extension at the v-max edge |
| `East` | item | Extension at the u-max edge |
| `South` | item | Extension at the v-min edge |
| `West` | item | Extension at the u-min edge |
| `CenterPlanes` | item | True = also output centre frames |

| Output | Meaning |
|---|---|
| `Extended` | Extended surfaces as Breps |
| `Planes` | Centre frames of the original surfaces (when `CenterPlanes` is True) |

**Good to know**
- North/East/South/West follow the surface's **UV directions**, not the world
  compass - use Flip / Swap UV on the input if the wrong side extends.
- If one side can't be extended (e.g. a closed/periodic direction), that side is
  left as is and a warning counts it. Before, the failed step returned nothing
  and the next one crashed, so **every** remaining surface was lost.
- Breps are handled face by face; the **untrimmed** underlying surface is
  extended, so trims are not kept.
- Negative distances are ignored with a warning (extend can't shrink).
- Wrapped Grasshopper inputs and extrusions are accepted; non-surfaces are
  counted in a warning. Errors show on the component (was `Print`).
- Renamed: `Srf, N, E, S, W, T` / `Surf, Pln` ->
  `Surfaces, North, East, South, West, CenterPlanes` / `Extended, Planes`.
- Not compiled here (no Rhino) - test in Grasshopper.

---

## SmoothGeo

**File:** `SmoothGeo.cs` &middot; Component name: *Universal Smoother*

Rhino's **Smooth** command as a component, for **Meshes, Curves, Surfaces** and
single-face **Breps / Extrusions**. Each step moves points towards the average of
their neighbours; you choose how far, how many steps, and along which axes of
which coordinate system.

| Input | Access | Meaning |
|---|---|---|
| `Geometry` | item | Mesh, Curve, Surface, single-face Brep or Extrusion |
| `SmoothFactor` | item | -1..1, how far points move per step (0 = 0.5) |
| `Steps` | item | Number of passes (0 = 1) |
| `SmoothX` / `SmoothY` / `SmoothZ` | item | Which directions may move |
| `FixBoundaries` | item | Keep open edges / curve ends fixed |
| `CoordSystem` | item | `0` World, `1` CPlane (uses `SmoothPlane`), `2` Object |
| `SmoothPlane` | item | Plane for `CoordSystem = 1` (missing = World XY) |
| `VertexIndices` | list | **Mesh only** - smooth just these vertices (empty = all) |

| Output | Meaning |
|---|---|
| `Result` | Smoothed geometry |
| `Info` | What was done (steps completed, coordinate system) |

**Good to know**
- **CPlane fix:** the plane was only passed to meshes; curves and surfaces were
  smoothed in CPlane mode **without** the plane, so `CoordSystem = 1` had no effect
  on them. It is now used for all types.
- **Mesh smooth result checked:** a failed mesh smooth (e.g. an out-of-range vertex
  index) used to pass silently. Bad indices are now dropped with a warning, and a
  step that fails stops the loop and is reported (`steps=done/asked`).
- **Trimmed faces:** a trimmed single-face Brep is smoothed as its *untrimmed*
  surface - you now get a warning about it.
- Extrusions are accepted; all of X/Y/Z off gives a warning; unsupported types name
  the type. Multi-face Breps are still not supported (deconstruct them first).
- Renamed to full words: `geometry, smoothFactor, numSteps, xSmooth, ySmooth,
  zSmooth, fixBoundaries, coordSystem, plane, vertexIndices` / `result, info` ->
  `Geometry, SmoothFactor, Steps, SmoothX, SmoothY, SmoothZ, FixBoundaries,
  CoordSystem, SmoothPlane, VertexIndices` / `Result, Info`. (`plane` became
  `SmoothPlane` so it can't clash with the `Plane` type.)
- Not compiled here (no Rhino) - test in Grasshopper.

---

## BlendAnalyze

**File:** `BlendEdgeAnalyzer.cs` &middot; Component name: *Brep Advanced Blender*

Builds a **blend surface between an edge of Brep A and an edge of Brep B** (like
Rhino's *BlendSrf*) and reads out its **NURBS structure**: control points,
weights and Greville parameters - in Grasshopper's native order (U outer, V
inner), so they line up with *Surface CP* style outputs.

| Input | Access | Meaning |
|---|---|---|
| `BrepA` / `BrepB` | item | The two breps |
| `EdgeIndexA` / `EdgeIndexB` | item | Edge to blend from / to |
| `FlipA` / `FlipB` | item | Flip the blend direction on that side |
| `ContinuityA` / `ContinuityB` | item | `0` G0 position, `1` G1 tangency, `2` G2 curvature |

| Output | Meaning |
|---|---|
| `BlendBrep` | The blend surface(s) |
| `ControlPoints` | Control points, U outer / V inner |
| `Weights` | Matching weights |
| `Greville` | Greville parameters as points `{u, v, 0}` |
| `UCount` / `VCount` | Control point counts |
| `Report` | Degrees, CV grid, continuity actually used |

**Good to know**
- **G3/G4 are not available here:** RhinoCommon's `BlendContinuity` only has
  Position, Tangency and Curvature (G0-G2). The old code cast 3 and 4 to
  non-existent enum values, and its report claimed "G3/G4 Optimized" just because
  there were 5+ control points. Continuity is now clamped to 0-2 with a remark, and
  the report and component message show what was really built.
- Edge indices are checked first (an out-of-range index gave a raw exception); an
  edge without faces is caught; interior (non-naked) edges get a remark.
- The report also says whether the surface is rational and if the blend came back
  in several pieces (the analysis uses the first).
- Renamed to full words: `brepA, edgeIndexA, flipA, continuityA` (and B) /
  `blendBrep, points, weights, greville, uCount, vCount, report` ->
  `BrepA, EdgeIndexA, FlipA, ContinuityA` (and B) /
  `BlendBrep, ControlPoints, Weights, Greville, UCount, VCount, Report`.
- Not compiled here (no Rhino) - test in Grasshopper.
