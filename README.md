# Bullbull — `Vector` branch

C# script components for **Rhino 8 Grasshopper**: vectors and frames on polylines.

Other branches: [`TEXT`](https://github.com/pularirajeev77-bit/Bullbull/tree/TEXT) (text tools) ·
[`curves`](https://github.com/pularirajeev77-bit/Bullbull/tree/curves) (curve division, lines from points) ·
[`utility`](https://github.com/pularirajeev77-bit/Bullbull/tree/utility) (weight/area/volume) ·
[`main`](https://github.com/pularirajeev77-bit/Bullbull/tree/main) (overview)

## How to use a script

1. In Grasshopper, place a **C# Script** component (Maths → Script).
2. Open its editor and replace everything with the contents of the `.cs` file.
3. The component takes its inputs and outputs from the `RunScript(...)` line.
   If they don't appear, add them by hand with the exact same names.

**Access types** used below:
**item** = one value at a time (Grasshopper repeats the script for every item
and keeps your list/tree shape) · **list** = the whole list at once ·
**tree** = the whole data tree at once.

## Components

| Component | File | What it does |
|---|---|---|
| [Bisect Frame](#bisect-frame) | `BisectFrame.cs` | Point, bisector vector and plane at every polyline vertex |

---

## Bisect Frame

**File:** `BisectFrame.cs`

For every vertex of a polyline, gives the **point**, the **bisector** (the
direction that splits the corner angle in half) and a **plane** built from it.
Useful for mitred joints, placing profiles or orienting objects at corners.

| Input | Access | Meaning |
|---|---|---|
| `Crv` | item | A polyline (a list works; you get one branch per curve) |

| Output | Meaning |
|---|---|
| `Pt` | The vertex points |
| `Vec` | The bisector vector at each vertex (length 1) |
| `Pln` | A plane at each vertex |

**How the directions are chosen**
- **Corner:** the bisector points into the inside of the turn.
- **Straight vertex** (no corner) and **ends of an open polyline:** the vector
  is square to the segment, on its left side (looking down from above).
- **Up direction:** worked out from the polyline itself — the normal of the
  plane it lies in, pointing to the +Z side. For a straight line it is Z.
- **Plane axes:** X = the bisector, Y = the up direction, Z = along the path.

**Good to know**
- Closed polylines: every vertex, including the start, is treated as a corner.
- Curves that are not polylines (arcs, NURBS) give empty output.
- Because the bisector points into the inside of each turn, it swaps sides on
  a zigzag, and the plane's Z then points backward at right-hand turns.
