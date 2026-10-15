# Bullbull - `subd` branch

C# script components for **Rhino 8 Grasshopper**: SubD tools.

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
[`grid`](https://github.com/pularirajeev77-bit/Bullbull/tree/grid) &middot;
[`main`](https://github.com/pularirajeev77-bit/Bullbull/tree/main) (overview)

## How to use a script

1. In Grasshopper, place a **C# Script** component (Maths > Script).
2. Open its editor and replace everything with the contents of the `.cs` file.
3. The component takes its inputs and outputs from the `RunScript(...)` line.
   If they don't appear, add them by hand with the exact same names.

## Components

| Component | File | What it does |
|---|---|---|
| [SubDCrease](#subdcrease) | `SubDEdgeCrease.cs` | Creases or smooths chosen SubD edges by edge index |

---

## SubDCrease

**File:** `SubDEdgeCrease.cs` &middot; Component name: *SubD Edge Crease Manager* &middot; v2.1

Creases or smooths chosen **edges of SubD objects**, picked by edge index. Crease edges give sharp
lines on the SubD's limit surface; smooth edges blend into the surrounding faces. Works on
**copies** - the Rhino / upstream SubDs are never changed.

| Input | Access | Meaning |
|---|---|---|
| `SubDs` | tree | SubD objects to modify |
| `EdgeIndices` | tree | Edge indices to change, in Rhino's edge order (`0` = first edge) |
| `CreaseValues` | tree | One per index: **> 0 = Crease**, **<= 0 = Smooth**. A shorter list reuses its last value; empty = all Crease |

| Output | Meaning |
|---|---|
| `CreasedSubDs` | Copies of the SubDs with the new edge tags (same tree as `SubDs`) |
| `ChangedEdges` | Edge indices actually changed, one branch per SubD (`{path;item}`) |

The message shows how many edges were creased and smoothed.

**Branch matching:** each `SubDs` branch uses the `EdgeIndices` / `CreaseValues` branch with the
same path. If there is none, branch `{0}` is used - so one flat list of indices applies to every SubD.

**Good to know**
- Edge indices follow **Rhino's edge order** (the order Grasshopper's SubD edge components list
  the edges in), starting at `0`. They are **not** Rhino's internal edge IDs.
- **Boundary (naked) edges** are always creased in a SubD - asking to smooth one is skipped with a
  remark.
- Values are **on / off** (crease or smooth), not a partial crease weight.
- After re-tagging, vertex tags and sector coefficients are updated and the evaluation cache is
  cleared, so the limit surface and display rebuild correctly.
- Indices outside the SubD's edge range are skipped with a warning.

**v2.1 fixes**
- **Wrong edges:** edges were looked up with `Edges.Find(index)`, which searches by edge **ID**,
  not by position. IDs don't start at 0, so index 0 matched nothing, other indices hit a different
  edge, and the last edge was refused. Edges are now taken by position.
- **Vertex tags** were not updated after the edge tags changed - new creases could evaluate wrongly.
- **Boundary edges** set to smooth gave an invalid SubD - now kept creased.
- Unconnected `EdgeIndices` / `CreaseValues` crashed the component - now handled.
- "Continuous" weights were only ever on/off - the input is now named and documented that way.
- Renamed `SubD_Geometries, Edge_Indices, Edge_Weights` / `Modified_SubD` ->
  `SubDs, EdgeIndices, CreaseValues` / `CreasedSubDs`; new `ChangedEdges` output; metadata, tooltips.
- Not compiled here (no Rhino) - test in Grasshopper.
