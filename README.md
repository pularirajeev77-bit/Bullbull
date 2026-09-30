# Bullbull - `point` branch

C# script components for **Rhino 8 Grasshopper**: point tools.

Other branches: [`TEXT`](https://github.com/pularirajeev77-bit/Bullbull/tree/TEXT) (text tools) &middot;
[`Vector`](https://github.com/pularirajeev77-bit/Bullbull/tree/Vector) (polyline frames) &middot;
[`curves`](https://github.com/pularirajeev77-bit/Bullbull/tree/curves) (curve tools) &middot;
[`utility`](https://github.com/pularirajeev77-bit/Bullbull/tree/utility) (general utilities) &middot;
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
| [CullDupPt](#cullduppt) | `CullDuplicatePoints.cs` | Removes duplicate points within a tolerance |
| [FarPts](#farpts) | `FarthestPair.cs` | Finds the two points that are farthest apart |
| [ClosePts](#closepts) | `ClosestPair.cs` | Finds the two points that are closest together |
| [SideSort](#sidesort) | `SideSorter.cs` | Sorts points into left and right of a curve |
| [PtOnCrv](#ptoncrv) | `PointOnCurve.cs` | Tests which points lie on curves, per branch |
| [RadSort](#radsort) | `RadialSort.cs` | Sorts points counter-clockwise around a plane |
| [WeightSort](#weightsort) | `WeightedSort.cs` | Sorts points by a weighted key of X, Y, Z |
| [UVSort](#uvsort) | `SurfaceUVSort.cs` | Sorts points row by row by their UV on a surface |

---

## CullDupPt

**File:** `CullDuplicatePoints.cs`

Removes **duplicate points** from a list. Two points closer together than
`Tolerance` are treated as the same, and only the first is kept.

| Input | Access | Meaning |
|---|---|---|
| `Points` | list | The points to de-duplicate |
| `Tolerance` | item | Two points closer than this count as one. 0 or less uses the model tolerance |

| Output | Meaning |
|---|---|
| `UniquePts` | The points with duplicates removed |

**Good to know**
- This is a real **distance** test, so points a hair apart still merge. The
  original rounded each coordinate to 6 decimals and compared text, which
  missed near-duplicates that sat across a rounding boundary and ignored the
  model's units.
- The first point of each duplicate group is the one kept.
- Invalid points are dropped.
- No input gives an empty list.

---

## FarPts

**File:** `FarthestPair.cs`

Finds the **two points that are farthest apart** in a list, and the distance
between them - useful for the overall span or bounding size of a point set.

| Input | Access | Meaning |
|---|---|---|
| `Points` | list | The points to search (need at least 2) |

| Output | Meaning |
|---|---|
| `PtA` | One end of the farthest pair |
| `PtB` | The other end |
| `MaxDist` | Distance between `PtA` and `PtB` |

**Good to know**
- Fewer than 2 points returns unset points and a distance of 0.
- It compares every pair, so it is instant for normal lists but slows down on
  very large sets (a few thousand points and up).

---

## ClosePts

**File:** `ClosestPair.cs`

Finds the **two points that are closest together** in a list, and the distance
between them - useful for spotting the tightest spacing or near-collisions.

| Input | Access | Meaning |
|---|---|---|
| `Points` | list | The points to search (need at least 2) |

| Output | Meaning |
|---|---|
| `PtA` | One of the two closest points |
| `PtB` | The other |
| `MinDist` | Distance between `PtA` and `PtB` |

**Good to know**
- Fewer than 2 points returns unset points and a distance of 0.
- It compares every pair, so it is instant for normal lists but slows down on
  very large sets (a few thousand points and up).

---

## SideSort

**File:** `SideSorter.cs`

Splits a set of points into those on the **left** and **right** of a curve.
Each point is judged against the nearest spot on the curve, using a plane's
normal to define which way is up.

| Input | Access | Meaning |
|---|---|---|
| `Points` | list | The points to sort |
| `Crv` | item | The dividing curve. Left/Right follow the curve's direction |
| `Pln` | item | Plane whose normal defines "up" for the test. Invalid = World XY |

| Output | Meaning |
|---|---|
| `LeftPts` | Points on the left |
| `RightPts` | Points on the right (points exactly on the curve go here) |
| `LeftIdx` | Original indices of the left points |
| `RightIdx` | Original indices of the right points |

**Good to know**
- "Left" and "Right" depend on the **curve's direction** - flip the curve to
  swap the two sides.
- The `Pln` normal sets which way is up; with World XY, left/right are as seen
  from above. Points sitting exactly on the curve go to `RightPts`.
- Use `LeftIdx` / `RightIdx` to pull matching data from other lists.

---

## PtOnCrv

**File:** `PointOnCurve.cs`

For each **branch of curves**, tests which of the points lie **on** a curve
(within a tolerance). Handy for filtering points that sit on given edges.

| Input | Access | Meaning |
|---|---|---|
| `Crv` | tree | Curves to test against. Each branch is tested on its own |
| `Pts` | list | The points to test (the same list is used for every branch) |
| `Tol` | item | A point counts as "on" if it is within this distance. 0 or less uses the model tolerance |

| Output | Meaning |
|---|---|
| `OnCrv` | Per branch: True/False for each point - is it on any curve in that branch? |
| `OnIdx` | Per branch: indices of the points that ARE on a curve |
| `OffIdx` | Per branch: indices of the points that are NOT on any curve |

**Good to know**
- The same point list is compared against every curve branch, so you get one
  set of results per branch (matching the curve tree's paths).
- A point is "on" if it lies within `Tol` of any curve in that branch.
- `OnIdx` / `OffIdx` are positions in the input `Pts` list, for pulling
  matching data from other lists.

---

## RadSort

**File:** `RadialSort.cs`

Sorts a list of points **counter-clockwise by angle** around a plane - useful
for ordering scattered points into a clean loop or fan.

| Input | Access | Meaning |
|---|---|---|
| `Pts` | list | The points to sort |
| `Pln` | item | Plane to sort around: origin = centre, X axis = angle 0, normal = CCW direction. Invalid = World XY |

| Output | Meaning |
|---|---|
| `SortedPts` | The points ordered counter-clockwise, starting from the plane's X axis |

**Good to know**
- Angle is measured in the plane, so points are flattened onto it first - the
  height above/below the plane does not affect the order.
- Sorting starts at the plane's X axis and goes counter-clockwise (as seen
  looking down the plane's normal). Rotate the plane to change the start.
- A point exactly at the plane origin has no direction; it just sorts to angle 0.

---

## WeightSort

**File:** `WeightedSort.cs`

Sorts points by a single **weighted key** built from their coordinates, so you
can bias the order toward one axis.

Key = `X_Mult * X^2 + Y_Mult * Y^2 + Z_Mult * Z^2`

| Input | Access | Meaning |
|---|---|---|
| `Points` | list | The points to sort |
| `X_Mult` | item | Weight on X (used as `X_Mult * X^2`) |
| `Y_Mult` | item | Weight on Y (used as `Y_Mult * Y^2`) |
| `Z_Mult` | item | Weight on Z (used as `Z_Mult * Z^2`) |

| Output | Meaning |
|---|---|
| `SortedPts` | The points ordered by key, smallest first |
| `Indices` | Original index of each sorted point |
| `Keys` | The key value of each point, in sorted order |

**Good to know**
- Each term is **squared**, so a coordinate's sign is lost: `x = -5` and
  `x = 5` weigh the same. This orders points by weighted distance from the
  origin, not along a signed direction.
- For a plain signed sort along a direction, sort by a dot product with that
  direction instead.
- `Indices` lets you reorder other lists the same way.

---

## UVSort

**File:** `SurfaceUVSort.cs`

Sorts points **row by row using their UV position on a surface** - the natural
order for panel nodes, grid points or fixings on a curved facade. Points whose
primary parameter is within `Tolerance` form one row; rows run along the primary
axis and points inside each row along the other axis.

| Input | Access | Meaning |
|---|---|---|
| `Points` | list | Points on or near the surface |
| `Surface` | item | Target surface |
| `SortUFirst` | item | True = rows by U (then V), False = rows by V (then U) |
| `Tolerance` | item | Row grouping tolerance, **surface parameter units** (0 = 0.001) |

| Output | Meaning |
|---|---|
| `SortedPoints` | Points in sorted order |
| `UVParams` | Their `(u, v)` on the surface |
| `OriginalIndices` | Index of each sorted point in the input |

**Good to know**
- **Row split fix:** the old version rounded parameters to a grid of `Tolerance`,
  so two points almost on the same row but either side of a rounding boundary
  (e.g. `u = 0.0049` and `0.0051`) landed in different rows. Rows are now built
  by gap: a new row starts only when the jump exceeds `Tolerance`.
- `Tolerance` is in the surface's **parameter** units, which depend on how the
  surface is parameterised (often its real size in model units, sometimes 0-1).
  Reparameterize the surface to 0-1 if you want a size-independent tolerance.
- Points that cannot be projected are reported in a warning (before they
  vanished silently, leaving `OriginalIndices` shorter than the input).
- Added the missing `using Grasshopper.Kernel;` (needed for the script base
  class); messages show on the component instead of `Print`; the message shows
  the row count.
- Renamed `Srf` -> `Surface`. Metadata and tooltips set once.
- Not compiled here (no Rhino) - test in Grasshopper.
