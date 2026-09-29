# Bullbull - `Layers` branch

C# script components for **Rhino 8 Grasshopper**: layer tools.

Other branches: [`TEXT`](https://github.com/pularirajeev77-bit/Bullbull/tree/TEXT) (text tools) &middot;
[`Vector`](https://github.com/pularirajeev77-bit/Bullbull/tree/Vector) (polyline frames) &middot;
[`curves`](https://github.com/pularirajeev77-bit/Bullbull/tree/curves) (curve tools) &middot;
[`utility`](https://github.com/pularirajeev77-bit/Bullbull/tree/utility) (general utilities) &middot;
[`point`](https://github.com/pularirajeev77-bit/Bullbull/tree/point) (point tools) &middot;
[`surface`](https://github.com/pularirajeev77-bit/Bullbull/tree/surface) (surface tools) &middot;
[`Plane`](https://github.com/pularirajeev77-bit/Bullbull/tree/Plane) (plane tools) &middot;
[`main`](https://github.com/pularirajeev77-bit/Bullbull/tree/main) (overview)

## How to use a script

1. In Grasshopper, place a **C# Script** component (Maths > Script).
2. Open its editor and replace everything with the contents of the `.cs` file.
3. The component takes its inputs and outputs from the `RunScript(...)` line.
   If they don't appear, add them by hand with the exact same names.

**Access types:** **item** = one value at a time.

## Components

| Component | File | What it does |
|---|---|---|
| [CurrLyr](#currlyr) | `CurrentLayer.cs` | Sets the current layer, creating it if needed |
| [LBS](#lbs) | `LayerBranchStyle.cs` | Creates and styles layer trees (colour + linetype) |

---

## CurrLyr

**File:** `CurrentLayer.cs`

Sets the **current (active) Rhino layer** by name. If the layer does not exist
it is created first, so new geometry lands on the layer you want.

| Input | Access | Meaning |
|---|---|---|
| `LayerName` | item | Layer to make current. Use `Parent::Child` for a sub-layer |
| `Run` | item | Set True to apply; False does nothing |

| Output | Meaning |
|---|---|
| `Msg` | What happened: which layer was set, whether it was created, or an error |

**Good to know**
- **Nested layers work:** `Frame::Steel` finds or creates the `Steel` sub-layer
  under `Frame`, creating `Frame` too if needed. (An earlier version made one
  flat layer literally named `Frame::Steel`.)
- New layers are created white; change their colour in Rhino afterwards.
- It changes the Rhino document, not Grasshopper, and only when `Run` is True.
- Runs on the active Rhino document; errors (no document, bad name) are
  reported in `Msg` and as a warning on the component.

---

## LBS

**File:** `LayerBranchStyle.cs`

Creates a **tree of layers** (nested with `::`) and sets each one's **colour**
and **linetype** in one go. A style given for a parent path cascades down to its
child layers, and a more specific path wins over a more general one.

| Input | Access | Meaning |
|---|---|---|
| `Run` | item | Set True to apply; False does nothing |
| `RootNames` | tree | Layer paths to create/style. Use `Parent::Child` for nesting |
| `Colors` | tree | Layer colours, matched per branch and cycled if shorter (default black) |
| `LineTypes` | tree | Linetype names, cycled if shorter |

| Output | Meaning |
|---|---|
| `Result` | True for each layer created or modified |
| `Info` | Log of what was created and changed |

**Linetype names accepted:** `Continuous` (also `cont`/`default`), `Hidden`,
`Dashed`, `Dot`/`Dotted`, `Center`, or any linetype already in the document
(a close partial match is used if an exact one isn't found).

**Good to know**
- **Cascading:** a colour set on `Frame` applies to `Frame::Steel` too, unless
  `Frame::Steel` has its own entry - the deepest matching path wins.
- Nested layers are created as real sub-layers; parents made on the way are
  black, only the leaf gets the given colour.
- It changes the Rhino document (not Grasshopper) and only when `Run` is True;
  the view refreshes automatically.
- With no active Rhino document it reports an error instead of failing.
