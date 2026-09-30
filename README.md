# Bullbull - `Tree` branch

C# script components for **Rhino 8 Grasshopper**: data-tree tools.

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
[`main`](https://github.com/pularirajeev77-bit/Bullbull/tree/main) (overview)

## How to use a script

1. In Grasshopper, place a **C# Script** component (Maths > Script).
2. Open its editor and replace everything with the contents of the `.cs` file.
3. The component takes its inputs and outputs from the `RunScript(...)` line.
   If they don't appear, add them by hand with the exact same names.

## Components

| Component | File | What it does |
|---|---|---|
| [CreateTree](#createtree) | `CreateTree.cs` | Sorts a flat list into branches by index (like Elefront Create Tree) |

---

## CreateTree

**File:** `CreateTree.cs`

Turns a flat list into a **data tree**: item `i` of `Data` goes into branch
`{BranchIndex[i]}`. Same idea as Elefront's *Create Tree* - handy for grouping
items by a computed key (level, panel number, type id...).

| Input | Access | Meaning |
|---|---|---|
| `Data` | list | Items to sort into branches |
| `BranchIndex` | list | Branch number per item (cycles if shorter than `Data`) |

| Output | Meaning |
|---|---|
| `Tree` | The tree, branches in ascending order |

Example: `Data = a, b, c, d` and `BranchIndex = 2, 0, 2, 1` gives
`{0}: b` &middot; `{1}: d` &middot; `{2}: a, c`.

**Good to know**
- Branches come out sorted (`{0}, {1}, {2}...`) whatever order the indices arrive in.
- Null items are kept (the old version dropped them, which shifted item counts).
- Negative indices are skipped with a warning (Grasshopper paths can't be negative).
- A remark shows if `BranchIndex` and `Data` have different lengths.
- Inputs/outputs renamed from `Data, Index` / `DataOut`.
- Not compiled here (no Rhino) - test in Grasshopper.
