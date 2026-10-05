# Bullbull - `intersect` branch

C# script components for **Rhino 8 Grasshopper**: intersection tools.

Other branches: [`TEXT`](https://github.com/pularirajeev77-bit/Bullbull/tree/TEXT) &middot;
[`Vector`](https://github.com/pularirajeev77-bit/Bullbull/tree/Vector) &middot;
[`curves`](https://github.com/pularirajeev77-bit/Bullbull/tree/curves) &middot;
[`utility`](https://github.com/pularirajeev77-bit/Bullbull/tree/utility) &middot;
[`point`](https://github.com/pularirajeev77-bit/Bullbull/tree/point) &middot;
[`surface`](https://github.com/pularirajeev77-bit/Bullbull/tree/surface) &middot;
[`Plane`](https://github.com/pularirajeev77-bit/Bullbull/tree/Plane) &middot;
[`Layers`](https://github.com/pularirajeev77-bit/Bullbull/tree/Layers) &middot;
[`display`](https://github.com/pularirajeev77-bit/Bullbull/tree/display) &middot;
[`SpaceFrame`](https://github.com/pularirajeev77-bit/Bullbull/tree/SpaceFrame) &middot;
[`Creation`](https://github.com/pularirajeev77-bit/Bullbull/tree/Creation) &middot;
[`Tree`](https://github.com/pularirajeev77-bit/Bullbull/tree/Tree) &middot;
[`TitleBlock`](https://github.com/pularirajeev77-bit/Bullbull/tree/TitleBlock) &middot;
[`main`](https://github.com/pularirajeev77-bit/Bullbull/tree/main) (overview)

## How to use a script

1. In Grasshopper, place a **C# Script** component (Maths > Script).
2. Open its editor and replace everything with the contents of the `.cs` file.
3. The component takes its inputs and outputs from the `RunScript(...)` line.
   If they don't appear, add them by hand with the exact same names.

## Components

| Component | File | What it does |
|---|---|---|
| [CrvPlaneInt](#crvplaneint) | `CurvePlaneIntersect.cs` | Splits curves into those that hit a plane and those that don't, with indices |
| [DirFilter](#dirfilter) | `CurveDirectionFilter.cs` | Splits curves by how well their direction matches a vector |
| [CoplanarFilter](#coplanarfilter) | `CoplanarCurveFilter.cs` | Splits curves into those lying in a plane and the rest, with indices |

---

## CrvPlaneInt

**File:** `CurvePlaneIntersect.cs` &middot; Component name: *Curve Plane Intersect*

Sorts a list of curves into the ones that **cross, touch or lie in** a reference
plane and the ones that are **entirely on one side** of it - e.g. find the
members crossing a floor level or a section plane. The plane is infinite.

| Input | Access | Meaning |
|---|---|---|
| `Curves` | list | Curves to test |
| `RefPlane` | item | Reference plane (missing = World XY) |
| `Tolerance` | item | Intersection tolerance (0 = model tolerance) |

| Output | Meaning |
|---|---|
| `IntersectingCurves` | Curves that cross, touch or lie in the plane |
| `NonIntersectingCurves` | Curves entirely on one side |
| `IntersectingIndices` | Their indices in `Curves` |
| `NonIntersectingIndices` | Their indices in `Curves` |

The component message shows `hits / misses`.

**Good to know**
- A curve **touching** the plane at one point, or lying **in** it, counts as intersecting.
- Null/invalid curves are in neither output (their index is skipped) - a warning
  now says how many.
- Messages show on the component (were `Print`, easy to miss). No open document no
  longer crashes the tolerance lookup.
- Metadata and tooltips are set once. Renamed from *Curve Plane Intersection Filter* /
  `CrvPlaneFilter` to *Curve Plane Intersect* / `CrvPlaneInt`.
- Not compiled here (no Rhino) - test in Grasshopper.

---

## DirFilter

**File:** `CurveDirectionFilter.cs` &middot; Component name: *Curve Direction Filter*

Sorts curves by how well their **overall direction** (start point -> end point)
matches a **target vector**, within an angle tolerance - e.g. pick out the
vertical mullions or the members running along X. With `Bidirectional`, curves
drawn the other way round match too.

| Input | Access | Meaning |
|---|---|---|
| `Curves` | list | Curves to test |
| `TargetVector` | item | Direction to match |
| `AngleTolerance` | item | Max deviation in **degrees** (0 = model angle tolerance) |
| `Bidirectional` | item | True = reversed (anti-parallel) curves also match |

| Output | Meaning |
|---|---|
| `SelectedCurves` | Curves within the tolerance |
| `RejectedCurves` | All other curves, incl. closed / zero-length ones |
| `SelectedIndices` | Their indices in `Curves` |
| `RejectedIndices` | Their indices in `Curves` |

The component message shows `in / out`.

**Good to know**
- Direction is start -> end, so a curved or zigzag curve is judged by its chord.
- **Tolerance 0 fix:** an unset tolerance meant "exact match only", which real
  geometry almost never meets, so everything was rejected. 0 now uses the model's
  angle tolerance.
- Closed and (nearly) zero-length curves have no direction: they are rejected and
  counted in a remark (before: only exactly-zero vectors were caught).
- `Tolerance` renamed to `AngleTolerance` to make the **degrees** unit clear.
- Messages show on the component (were `Print`); metadata and tooltips set once.
- Not compiled here (no Rhino) - test in Grasshopper.

---

## CoplanarFilter

**File:** `CoplanarCurveFilter.cs` &middot; Component name: *Coplanar Curve Filter*

Sorts curves into the ones lying **in** a reference plane (every part within the
distance tolerance) and the rest - e.g. pick the outlines on one floor level or
one facade plane. Compare with [CrvPlaneInt](#crvplaneint), which also accepts
curves that just **cross** the plane.

| Input | Access | Meaning |
|---|---|---|
| `Curves` | list | Curves to test |
| `RefPlane` | item | Reference plane (missing = World XY) |
| `Tolerance` | item | Max distance from the plane (0 = model tolerance) |

| Output | Meaning |
|---|---|
| `CoplanarCurves` | Curves lying entirely in the plane |
| `NonCoplanarCurves` | All other curves |
| `CoplanarIndices` | Their indices in `Curves` |
| `NonCoplanarIndices` | Their indices in `Curves` |

The component message shows `in / out`.

**Good to know**
- Null/invalid curves are in neither output - a warning now says how many.
- Messages show on the component (were `Print`). No open document no longer
  crashes the tolerance lookup.
- Metadata and tooltips set once; input/output names unchanged.
- Not compiled here (no Rhino) - test in Grasshopper.
