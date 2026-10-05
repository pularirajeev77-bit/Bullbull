# Bullbull - `grid` branch

C# script components for **Rhino 8 Grasshopper**: grid tools.

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
[`intersect`](https://github.com/pularirajeev77-bit/Bullbull/tree/intersect) &middot;
[`shape`](https://github.com/pularirajeev77-bit/Bullbull/tree/shape) &middot;
[`main`](https://github.com/pularirajeev77-bit/Bullbull/tree/main) (overview)

## How to use a script

1. In Grasshopper, place a **C# Script** component (Maths > Script).
2. Open its editor and replace everything with the contents of the `.cs` file.
3. The component takes its inputs and outputs from the `RunScript(...)` line.
   If they don't appear, add them by hand with the exact same names.

## Components

| Component | File | What it does |
|---|---|---|
| [GridBoundList](#gridboundlist) | `GridInBoundary.cs` | U/V grid of lines inside a closed boundary, with repeating spacing lists, plus grid nodes |
| [CrvIntGrid](#crvintgrid) | `CurveIntersectGrid.cs` | Intersects curves in two directions into an ordered point grid (like PanelingTools ptIntersect), with a gap tolerance |

---

## GridBoundList

**File:** `GridInBoundary.cs` &middot; Component name: *Grid In Boundary List* &middot; v2.1

Builds a **U/V grid of lines inside a closed planar boundary**. Each direction has its own
**repeating list of spacings**, measured outward from a start point, and its own direction -
so the grid can be skewed. Outputs the trimmed U and V lines and every grid node.

| Input | Access | Meaning |
|---|---|---|
| `Boundary` | item | Closed planar curve |
| `USpacing` | list | Spacing between U lines, repeated (e.g. `1000, 1500` -> 1000, 1500, 1000, ...) |
| `VSpacing` | list | Spacing between V lines, repeated |
| `UDirection` | item | Direction the **U lines run** |
| `VDirection` | item | Direction the **V lines run**. Empty = perpendicular to U |
| `StartPoint` | item | Grid origin - one U line and one V line pass through it |

| Output | Meaning |
|---|---|
| `UCurves` | U lines trimmed to the boundary, **in order** across the grid |
| `VCurves` | V lines trimmed to the boundary, in order across the grid |
| `IntersectionPoints` | Unique grid nodes: U x V crossings + line ends on the boundary |

The message shows the counts: `U n | V n | Pts n`.

**How spacing works:** U lines run along `UDirection` and are spaced along its in-plane
perpendicular; V lines likewise. The spacing list repeats outward from `StartPoint` in **both**
directions (the negative side uses the same sequence mirrored).

**Good to know**
- `StartPoint` and both directions are **projected onto the boundary's plane**, so a point or
  vector slightly off the plane still gives a flat grid.
- A line crossing a concave boundary is split into several inside pieces - each piece is one
  curve in the output.
- Lines running exactly along a straight boundary edge are kept (they are on the boundary).
- More than 5000 lines in one direction is capped with a warning (spacing too small).

**v2.1 fixes**
- **Off-plane start point:** the grid was built in a parallel plane, never crossed the boundary,
  and whole untrimmed lines came out. The start point is now projected onto the boundary plane.
- **Tilted directions:** a direction with a Z (out-of-plane) part tilted the grid; directions are
  now projected into the plane.
- **Line order:** lines came out `0, +1, +2, ..., -1, -2`; now sorted across the grid, so
  list indices match positions.
- **Edge overlaps:** a grid line lying along a straight boundary edge used only one end of the
  overlap; both ends are used now.
- **Parallel U/V:** V parallel to U gave duplicate lines - now a warning.
- **Runaway spacing:** tiny spacing could create millions of lines / hang - capped.
- **Non-planar boundary** fell back to World XY silently - now a warning.
- Renamed `USpace, VSpace, uDirVect, vDirVect` / `IntersectPts` ->
  `USpacing, VSpacing, UDirection, VDirection` / `IntersectionPoints`; metadata set once; tooltips.
- Not compiled here (no Rhino) - the station logic was checked separately; test in Grasshopper.

---

## CrvIntGrid

**File:** `CurveIntersectGrid.cs` &middot; Component name: *Curve Intersect Grid* &middot; v1.0

Works like PanelingTools **ptIntersect**: give it curves running in **two directions**, get an
**ordered point grid** back - one branch per row, items in column order - ready for paneling
(PanelingTools cells, *Surface From Points*, your own panel scripts, ...).

**The addition - `Tolerance`:** curves that don't quite touch still make a node when they come
within `Tolerance` of each other: a curve that stops short, two curves at slightly different
heights, drawing gaps. The node is placed in the middle of the gap. ptIntersect only finds
exact intersections.

| Input | Access | Meaning |
|---|---|---|
| `Curves` | list | All grid curves, both directions - split automatically by direction |
| `Tolerance` | item | Largest gap that still counts as an intersection. **0 / empty = model tolerance** (exact only) |
| `SwapDirections` | item | True = swap rows and columns |

| Output | Meaning |
|---|---|
| `Grid` | Points, tree `{row}`, items in column order. **null** where a row and column don't meet |
| `RowCurves` | Row-direction curves, sorted across the grid |
| `ColCurves` | Column-direction curves, sorted across the grid |
| `NodeType` | Same tree as `Grid`: `0` exact, `1` gap closed within tolerance, `-1` no node |

The message shows `rows x cols`, plus how many nodes were near misses and how many are missing.

**How it works**
1. **Split:** the longest curve sets direction A; every curve within 45 deg of it (either way
   round) is family A, the rest family B. A = rows, B = columns (`SwapDirections` flips this).
2. **Sort:** rows are ordered across the grid by their midpoints, columns the same - so
   `{0}` is the first row and item 0 the first column, whichever way the curves were drawn.
3. **Nodes:** for every row x column pair: an exact intersection if there is one; otherwise
   the closest points between the two curves, accepted if their gap is within `Tolerance`.

**Good to know**
- Every branch has the same number of items (`null` for a missing node), so columns stay
  aligned. Use `NodeType` (`-1`) or *Clean Tree* if you want the nulls gone.
- If a pair crosses more than once, the crossing nearest the row curve's start is used (remark).
- Keep `Tolerance` well below the grid spacing - a tolerance as big as a bay would join curves
  that are meant to be separate.
- Curves running diagonally at about 45 deg to the longest curve can land in either family -
  split them yourself and merge them in order if needed.
- Not compiled here (no Rhino) - test in Grasshopper.

**Use with GridBoundList:** merge its `UCurves` and `VCurves` into `Curves` to get the grid nodes as an ordered point tree.
