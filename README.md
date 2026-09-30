# Bullbull - `surface` branch

C# script components for **Rhino 8 Grasshopper**: surface and Brep tools.

Other branches: [`TEXT`](https://github.com/pularirajeev77-bit/Bullbull/tree/TEXT) (text tools) &middot;
[`Vector`](https://github.com/pularirajeev77-bit/Bullbull/tree/Vector) (polyline frames) &middot;
[`curves`](https://github.com/pularirajeev77-bit/Bullbull/tree/curves) (curve tools) &middot;
[`utility`](https://github.com/pularirajeev77-bit/Bullbull/tree/utility) (general utilities) &middot;
[`point`](https://github.com/pularirajeev77-bit/Bullbull/tree/point) (point tools) &middot;
[`main`](https://github.com/pularirajeev77-bit/Bullbull/tree/main) (overview)

## How to use a script

1. In Grasshopper, place a **C# Script** component (Maths > Script).
2. Open its editor and replace everything with the contents of the `.cs` file.
3. The component takes its inputs and outputs from the `RunScript(...)` line.
   If they don't appear, add them by hand with the exact same names.

**Access types:** **item** = one value at a time &middot; **list** = the whole list at once &middot; **tree** = the whole data tree at once.

## Components

| Component | File | What it does |
|---|---|---|
| [EdgeAn](#edgean) | `EdgeAnalyzer.cs` | Sorts a Brep's edges: outer, inner, naked, interior, non-manifold |
| [DraftThick](#draftthick) | `DraftedThicken.cs` | Thickens a surface/polysurface/closed brep along its normals with a draft angle |

---

## EdgeAn

**File:** `EdgeAnalyzer.cs`

Analyzes the **edges** of a Brep (surface or polysurface) and sorts them into
groups - useful for finding open borders, holes, or bad (non-manifold) edges.

> **Set the `Breps` input to Tree access** (right-click the input > Tree). On
> Item access, Grasshopper runs the script once per Brep and you get one flat
> branch instead of a result per input branch.

| Input | Access | Meaning |
|---|---|---|
| `Breps` | tree | The Breps to analyze; one result branch per input branch |

| Output | Meaning |
|---|---|
| `Outer` | Exterior naked edges - the outside border |
| `Inner` | Interior naked edges - hole borders |
| `Naked` | All naked edges (on only one face), one curve per edge |
| `Interior` | Interior edges shared between two faces |
| `NonManifold` | Edges shared by 3+ faces (usually a modelling error) |
| `NakedIdx` | Edge index of each `Naked` curve (1:1) |
| `InteriorIdx` | Edge index of each `Interior` curve (1:1) |
| `NonManifoldIdx` | Edge index of each `NonManifold` curve (1:1) |

**Good to know**
- `Outer` + `Inner` together are the naked edges, but they come out **joined**
  into fewer curves, while `Naked` lists them **one curve per edge** alongside
  its `NakedIdx`. Use whichever suits the next step.
- `NakedIdx` / `InteriorIdx` / `NonManifoldIdx` are BrepEdge indices, for
  feeding back into other edge tools.
- A clean, closed solid has no naked edges; naked edges mean an open border or
  hole. Non-manifold edges usually point to a modelling problem.
- Accepts anything Grasshopper can turn into a Brep (surfaces, boxes,
  extrusions, SubD, ...).

---

## DraftThick

**File:** `DraftedThicken.cs`

Thickens a **surface, polysurface or closed brep** along its local normals and
gives the open edges a **draft angle** (walls lean inward or outward), returning
a solid. A closed brep becomes a hollow shell (outer + inner skin).

| Input | Access | Meaning |
|---|---|---|
| `Surface` | item | Surface, polysurface or closed brep to thicken |
| `Height` | item | Thickness along the local normal (negative = other side) |
| `Angle` | item | Draft angle in degrees (0 = walls normal to the surface) |
| `FlipSide` | item | False = draft inward, True = draft outward |
| `MiterLimit` | item | Max corner overshoot x draft offset (0 = off, try 2) |
| `AutoFit` | item | Reduce the draft automatically if the top folds over |
| `MergeFaces` | item | Merge coplanar faces in the result |
| `FastZero` | item | Use Rhino's own offset when `Angle` = 0 (open breps) |
| `WallsOnly` | item | Output only the side walls (no base / top cap) |

| Output | Meaning |
|---|---|
| `Solid` | Drafted solid, hollow solid for closed input, or walls only |

**Good to know**
- Closed breps ignore `Angle` / `FlipSide` (there are no open edges).
- The output was renamed from `Brep` to `Solid` so the Brep type can be used
  freely inside the script; the input `Breps` became `Surface` (it is one item).
- Warnings and remarks (folded top, join tolerance, invalid result) appear as
  runtime messages on the component.
- Not compiled here (no Rhino) - test in Grasshopper and report any odd cases.
