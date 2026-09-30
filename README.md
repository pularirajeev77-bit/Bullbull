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
| [TreeSwap](#treeswap) | `TreeSwap.cs` | Swaps the first two path indices ({A;B} -> {B;A}) |
| [TreeSwap+](#treeswap-1) | `TreeSwapPrefix.cs` | Swaps indices for 2+ names, prefixes {0} for 1 name |

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

---

## TreeSwap

**File:** `TreeSwap.cs`

Swaps the **first two indices of every branch path**: `{A;B;...}` becomes
`{B;A;...}` - e.g. turn rows into columns, or regroup "per panel / per layer"
into "per layer / per panel". Deeper indices are left alone. A flat tree `{i}`
becomes `{0;i}`.

| Input | Access | Meaning |
|---|---|---|
| `Data` | tree | Tree to swap |

| Output | Meaning |
|---|---|
| `SwappedTree` | Tree with the first two path indices swapped |

Example: `{0;0} a` &middot; `{0;1} b` &middot; `{1;0} c` &middot; `{1;1} d`
becomes `{0;0} a` &middot; `{0;1} c` &middot; `{1;0} b` &middot; `{1;1} d`.

**Good to know**
- Output branches are **sorted**, so everything with the same new first index
  sits together (the old version kept the input order, which interleaved them).
- Empty branches are kept.
- Mixed-depth input (e.g. `{1}` next to `{1;0}`) can map two branches onto the
  same path; they are merged and a warning tells you how many.
- An empty input now gives a warning instead of a silent empty tree.
- Output renamed from `outTree` to `SwappedTree`; the component name typo
  ("Swaping") is fixed.
- Not compiled here (no Rhino) - test in Grasshopper.

---

## TreeSwap+

**File:** `TreeSwapPrefix.cs`

A **count-driven** version of TreeSwap. The number of items in `Names` decides
what happens to `Data`, so a definition keeps the same tree shape whether it is
fed one item (e.g. one panel) or many:

| `Names` count | Result |
|---|---|
| 2 or more | Swap the first two indices: `{A;B;...}` -> `{B;A;...}` (flat `{A}` -> `{0;A}`) |
| 1 | Prefix every path with 0: `{A;B}` -> `{0;A;B}` |
| 0 | No output |

| Input | Access | Meaning |
|---|---|---|
| `Names` | list | Only the count is used |
| `Data` | tree | Tree to restructure |

| Output | Meaning |
|---|---|
| `SwappedTree` | Restructured tree, branches sorted |

**Good to know**
- The message under the component shows which mode ran.
- Output branches are sorted and empty branches kept (same as TreeSwap).
  Mixed-depth input that collides is merged with a warning.
- An unconnected `Names` counts as empty (no output) instead of a generic message.
- Renamed: `names, data` / `outTree` -> `Names, Data` / `SwappedTree`. The
  component is named **TreeSwap+** so it can't be confused with TreeSwap
  (both used the same name and nickname before).
- Not compiled here (no Rhino) - test in Grasshopper.
