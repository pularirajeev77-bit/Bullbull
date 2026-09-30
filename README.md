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

Set both inputs to **list** access.

## Components

| Component | File | What it does |
|---|---|---|
| [sharedNodes](#sharednodes) | `SharedNodes.cs` | Groups curves by the nodes they start or end at |

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
