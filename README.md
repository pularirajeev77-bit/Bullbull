# Bullbull - `Plane` branch

C# script components for **Rhino 8 Grasshopper**: plane tools.

Other branches: [`TEXT`](https://github.com/pularirajeev77-bit/Bullbull/tree/TEXT) (text tools) &middot;
[`Vector`](https://github.com/pularirajeev77-bit/Bullbull/tree/Vector) (polyline frames) &middot;
[`curves`](https://github.com/pularirajeev77-bit/Bullbull/tree/curves) (curve tools) &middot;
[`utility`](https://github.com/pularirajeev77-bit/Bullbull/tree/utility) (general utilities) &middot;
[`point`](https://github.com/pularirajeev77-bit/Bullbull/tree/point) (point tools) &middot;
[`surface`](https://github.com/pularirajeev77-bit/Bullbull/tree/surface) (surface tools) &middot;
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
| [CamPlane](#camplane) | `DynamicPlaneGenerator.cs` | Planes at points that face the camera |

---

## CamPlane

**File:** `DynamicPlaneGenerator.cs` &middot; Component name: *Dynamic Plane Generator* &middot; v2.1

Makes a plane at each point that **faces the active viewport's camera** - X = screen right,
Y = screen up, normal toward you. Handy for text tags, icons or symbols that should always
face you. With `AutoRefresh` on, the planes follow the camera as you orbit, pan or zoom.

| Input | Access | Meaning |
|---|---|---|
| `Points` | list | Points to place planes at (list or tree - tree structure is kept) |
| `AutoRefresh` | item | True = planes follow the camera; False = fixed until the next solve |

| Output | Meaning |
|---|---|
| `Planes` | One camera-facing plane per point, same order as `Points` |

The message shows **Auto-Refreshing** or **Static**.

**Good to know**
- `AutoRefresh` re-solves only when the camera **actually moves** (checked about 10 times a
  second), so leaving it on costs almost nothing while you are not navigating.
- The planes follow the **active** viewport - click into the view you want before relying on it.
- No points, or no active view, gives a warning and no output.
- A deleted component unhooks itself, so it cannot keep re-solving in the background.

**v2.1 fixes**
- **Pin names and tooltips:** they were set by pin position. Rhino 8 script components can have
  an extra `out` pin first, so the `Planes` tooltip landed on the wrong pin; it also overwrote
  each pin's script name. Pins are now found by name, and only the visible name and tooltip are set.
- **Idle CPU:** auto-refresh re-solved 10 times a second even with a still camera; now only on
  camera change.
- **Empty input:** gave one plane at the world origin silently; now a warning.
- `Points` is now a typed list (`List<Point3d>`), so lists and trees work without hand conversion.
- Not compiled here (no Rhino) - test in Grasshopper.
