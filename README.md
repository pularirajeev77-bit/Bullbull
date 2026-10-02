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
| [CrvPlaneFilter](#crvplanefilter) | `CurvePlaneFilter.cs` | Splits curves into those that hit a plane and those that don't, with indices |

---

## CrvPlaneFilter

**File:** `CurvePlaneFilter.cs`

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
- Metadata and tooltips are set once.
- Not compiled here (no Rhino) - test in Grasshopper.
