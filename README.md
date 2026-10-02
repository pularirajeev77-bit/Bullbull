# Bullbull - `SpaceFrame` branch

C# script components for **Rhino 8 Grasshopper**: space-frame / node tools.

Other branches: [`TEXT`](https://github.com/pularirajeev77-bit/Bullbull/tree/TEXT) &middot;
[`Vector`](https://github.com/pularirajeev77-bit/Bullbull/tree/Vector) &middot;
[`curves`](https://github.com/pularirajeev77-bit/Bullbull/tree/curves) &middot;
[`utility`](https://github.com/pularirajeev77-bit/Bullbull/tree/utility) &middot;
[`point`](https://github.com/pularirajeev77-bit/Bullbull/tree/point) &middot;
[`surface`](https://github.com/pularirajeev77-bit/Bullbull/tree/surface) &middot;
[`Plane`](https://github.com/pularirajeev77-bit/Bullbull/tree/Plane) &middot;
[`Layers`](https://github.com/pularirajeev77-bit/Bullbull/tree/Layers) &middot;
[`display`](https://github.com/pularirajeev77-bit/Bullbull/tree/display) &middot;
[`Creation`](https://github.com/pularirajeev77-bit/Bullbull/tree/Creation) &middot;
[`Tree`](https://github.com/pularirajeev77-bit/Bullbull/tree/Tree) &middot;
[`TitleBlock`](https://github.com/pularirajeev77-bit/Bullbull/tree/TitleBlock) &middot;
[`intersect`](https://github.com/pularirajeev77-bit/Bullbull/tree/intersect) &middot;
[`main`](https://github.com/pularirajeev77-bit/Bullbull/tree/main) (overview)

## How to use a script

1. In Grasshopper, place a **C# Script** component (Maths > Script).
2. Open its editor and replace everything with the contents of the `.cs` file.
3. The component takes its inputs and outputs from the `RunScript(...)` line.
   If they don't appear, add them by hand with the exact same names.

Access types are listed per component below.

## Components

| Component | File | What it does |
|---|---|---|
| [sharedNodes](#sharednodes) | `SharedNodes.cs` | Groups curves by the nodes they start or end at |
| [nodeSize](#nodesize) | `NodeSize.cs` | Sizes a node from the smallest angle between its members |
| [HardLook](#hardlook) | `HardwareLookup.cs` | Looks up bolt/sleeve/cone/thread sizes for a pipe diameter from Excel |

**Reference file:** [`HardLook.xlsx`](HardLook.xlsx) - the hardware table HardLook reads (see [HardLook](#hardlook)).

---

## sharedNodes

**File:** `SharedNodes.cs` &middot; Component name: *Group Curves Shared Nodes* &middot; v2.0

For each node point, collects every curve whose **start or end point** sits on it -
e.g. all members meeting at a space-frame joint. Points are matched by their
coordinates **rounded to 6 decimals**. Each output is a tree with **one branch per
node**, so branch `{i}` belongs to `points[i]`.

| Input | Access | Meaning |
|---|---|---|
| `curves` | list | Curves (frame members) to group |
| `points` | list | Node points |

| Output | Meaning |
|---|---|
| `lines` | Curves touching each node |
| `lin_index` | Index in `curves` of each of those curves |
| `pts` | The matching curve end point, once per touching curve |
| `pt_index` | Index in `points` of the node, once per touching curve |
| `unique_pts` | The node point once per branch (duplicates removed) |

**Good to know**
- Matching is exact to 6 decimals: a curve end that differs from the node by more
  than rounding noise (or sits right on a rounding boundary) is not matched.
- Nodes with no curves get no items in their branch.
- A closed curve whose start and end are on the same node is listed twice at that node.
- If `points` is empty the outputs stay empty.

---

## nodeSize

**File:** `NodeSize.cs` &middot; Component name: *Node Size Calculator* &middot; Message *Node Sizes v2.1*

Sizes a **space-frame node**. From the members meeting at one node it finds the
two that are closest together (smallest angle between them, measured towards each
member's midpoint) and works out how long/large the node must be so the members
clear each other:

`length = dia / sin(angle / 2) + thk`, then `radius` = `length` rounded **up** to a
multiple of `round`.

Feed it from sharedNodes: `lines` -> `lines` and `unique_pts` -> `unique_pts`.
`lines` is list access and `unique_pts` item access, so it runs once per node.

| Input | Access | Meaning |
|---|---|---|
| `lines` | list | Members touching this node |
| `unique_pts` | item | The node point |
| `thk` | item | Added wall thickness (0 or less uses 10) |
| `dia` | item | Member diameter to clear (0 or less uses 12) |
| `round` | item | Radius rounds up to a multiple of this (0 or less uses 1) |

| Output | Meaning |
|---|---|
| `angle` | Smallest angle between two members, degrees |
| `length` | Node length from the formula above |
| `radius` | `length` rounded up to `round` |
| `refpoint` | The node point |

**Good to know**
- With fewer than two usable members the size outputs stay empty.
- Two overlapping members (angle 0) give `length` and `radius` of 0.

---

## HardLook

**File:** `HardwareLookup.cs`

Reads a hardware table from an **Excel (.xlsx)** file and returns the bolt,
sleeve, cone and thread sizes for a given **pipe diameter**. It reads the file
directly (OLEDB), so Excel does not need to be open or even installed.

| Input | Access | Meaning |
|---|---|---|
| `ExcelPath` | item | Full path to the .xlsx table, e.g. your local copy of [`HardLook.xlsx`](HardLook.xlsx) |
| `PipeDiameter` | item | Pipe diameter to look up (rounded to a whole number) |

| Output | Meaning |
|---|---|
| `BoltDiameter` | Bolt diameter |
| `SleeveDiameter` | Sleeve diameter |
| `SleeveLength` | Sleeve length |
| `ConeDepth` | Cone depth |
| `ThreadLength` | Thread length |

### Reference file: `HardLook.xlsx`

[`HardLook.xlsx`](HardLook.xlsx) on this branch is the hardware table HardLook
reads. Download it, keep it somewhere on your PC, and connect its full path to
`ExcelPath` (e.g. with a File Path parameter). Edit or extend the rows in Excel -
HardLook picks up the changes the next time it runs.

`Sheet1` (all sizes in mm):

| PipeDiameter | BoltDiameter | SleeveDiameter | SleeveLength | ConeDepth | ThreadLength |
|---|---|---|---|---|---|
| 48 | 12 | 26 | 25 | 24 | 19 |
| 60 | 16 | 34 | 32 | 31 | 24 |
| 73 | 20 | 42 | 40 | 39 | 29 |
| 88 | 24 | 50 | 45 | 47 | 34 |
| 114 | 30 | 64 | 55 | 59 | 41 |
| 168 | 36 | 76 | 65 | 90 | 49 |
| 219 | 48 | 98 | 96 | 116 | 72 |

`PipeDiameter` is the tube outside diameter rounded to a whole number (e.g. a
48.3 tube matches the `48` row). A diameter not in the table returns `10` for every
output with a remark.

**Table layout** (if you make your own file) - first row holds these headers (any order):
`PipeDiameter, BoltDiameter, SleeveDiameter, SleeveLength, ConeDepth, ThreadLength`.
Uses `Sheet1`, or the first sheet if there is no `Sheet1`.

**Good to know**
- Needs the **Microsoft Access Database Engine (64-bit)** on the PC; without it
  you get a clear error. Rhino 8 loads the `System.Data.OleDb` package via the
  `#r "nuget: ..."` line at the top (first run needs internet).
- Not found / bad input -> every output is `10` and a message says why.
- Numbers are parsed from text safely (the old version could crash on values
  like `48.3` stored as text, and on comma-decimal PCs).
- Missing column names are reported by name.
- Not compiled here (no Rhino/Excel) - test in Grasshopper.
