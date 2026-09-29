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
