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

---

## sharedNodes

**File:** `SharedNodes.cs`

For each node you supply, finds every curve whose start or end point touches it
(matched to 6 decimals) - e.g. all members meeting at a space-frame joint. Each
output is a tree with **one branch per node**, so branch `{i}` belongs to
`Points[i]`.

| Input | Access | Meaning |
|---|---|---|
| `Curves` | list | Curves (frame members) to group |
| `Points` | list | Nodes to test |

| Output | Meaning |
|---|---|
| `NodeCurves` | Curves touching each node |
| `CurveIndices` | Index in `Curves` of each of those curves |
| `NodePoints` | The node point, repeated once per touching curve |
| `NodeIndices` | Index in `Points` of the node, repeated per curve |
| `UniqueNodes` | One point per node that has at least one curve |

**Good to know**
- Nodes with no curves get an empty branch, so branches always line up with `Points`.
- A closed curve (start = end) is counted once at its node (before it was listed twice).
- Empty/missing inputs now give a warning instead of silently returning nothing.
- Inputs/outputs were renamed from `curves, points, lines, lin_index, pts, pt_index, unique_pts`.
- Not compiled here (no Rhino) - test in Grasshopper.

---

## nodeSize

**File:** `NodeSize.cs`

Sizes a **space-frame node**. It takes the members meeting at one node, finds the
two that are closest together (smallest angle), and from that angle works out how
long/large the node must be so the members clear each other.

`Length = Diameter / sin(Angle / 2) + Thickness`, then `Radius` is `Length`
rounded **up** to a multiple of `Rounding`.

Feed it from `sharedNodes`: `NodeCurves` -> `Curves` and `UniqueNodes` -> `Node`.
`Curves` is a list and `Node` an item, so the component runs once per node.

| Input | Access | Meaning |
|---|---|---|
| `Curves` | list | Members touching this node |
| `Node` | item | The node point |
| `Thickness` | item | Added wall thickness (0 or less uses 10) |
| `Diameter` | item | Member diameter to clear (0 or less uses 12) |
| `Rounding` | item | Radius rounds up to a multiple of this (0 or less uses 1) |

| Output | Meaning |
|---|---|
| `Angle` | Smallest angle between two members, degrees |
| `Length` | Node length from the formula above |
| `Radius` | `Length` rounded up to `Rounding` |
| `RefPoint` | The node point |

**Good to know**
- With fewer than two usable members, or two overlapping members (angle ~0), the
  size outputs stay empty and a message explains why.
- Inputs/outputs were renamed from `lines, unique_pts, thk, dia, round` and
  `angle, length, radius, refpoint`.
- Not compiled here (no Rhino) - test in Grasshopper.
