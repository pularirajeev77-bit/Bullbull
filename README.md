# Bullbull - `Creation` branch

C# script components for **Rhino 8 Grasshopper**: tools that create objects in the Rhino document.

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
[`main`](https://github.com/pularirajeev77-bit/Bullbull/tree/main) (overview)

## How to use a script

1. In Grasshopper, place a **C# Script** component (Maths > Script).
2. Open its editor and replace everything with the contents of the `.cs` file.
3. The component takes its inputs and outputs from the `RunScript(...)` line.
   If they don't appear, add them by hand with the exact same names.

## Components

| Component | File | What it does |
|---|---|---|
| [Bake](#bake) | `BakePro.cs` | Bakes geometry with name, layer, colour, print width, isocurves and grouping |

---

## Bake

**File:** `BakePro.cs`

Bakes Grasshopper geometry into the Rhino document with attributes set per
object. Each attribute list **cycles**, so a single value applies to everything
and a list gives one value per object.

| Input | Access | Meaning |
|---|---|---|
| `Geometry` | list | Geometry to bake (curves, breps, meshes, points, lines, circles, arcs, rectangles, boxes, spheres) |
| `Names` | list | Object names (optional) |
| `Layers` | list | Layer per object, nested with `Parent::Child`; created if missing (optional) |
| `Colors` | list | Object colour, set "by object" (optional) |
| `PrintWidths` | list | Print width in mm, set "by object" (optional) |
| `WireDensity` | list | Isocurve density (-1 hides, 0 edges only, 1 default) (optional) |
| `Bake` | item | Bakes once each time it goes False -> True (use a **Button**) |
| `Group` | item | True = group everything baked in that press |

| Output | Meaning |
|---|---|
| `ObjectIds` | IDs of the objects baked by the last press |

**Good to know**
- **Bakes once per press.** The old version baked again on *every* recompute while
  `activate` was True, piling up duplicates (and empty groups) whenever anything
  upstream changed.
- `pWidths` and `wiresEach` were inputs but never used - they now set print width
  and isocurve density.
- Nested layers (`A::B::C`) now work; before, `Layer.IsValidName` rejected the
  `::` and the layer was silently skipped. Missing layers are created black.
- Each press is one **Undo** step (Ctrl+Z removes the whole bake) and the
  viewport redraws.
- Objects inside nested lists now report their IDs too; unsupported types are
  skipped with a warning.
- Inputs/outputs renamed from `objs, names, layers, colors, pWidths, wiresEach,
  activate, groupListTgthr` / `resultID`.
- Not compiled here (no Rhino) - test in Grasshopper.
