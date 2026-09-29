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
