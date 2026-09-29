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

**File:** `DynamicPlaneGenerator.cs`

Makes a plane at each point that **faces the active viewport's camera** - handy
for text tags or icons that should always face you. It can auto-refresh so the
planes keep facing the camera as you orbit.

| Input | Access | Meaning |
|---|---|---|
| `Points` | item / list | Point(s) to place planes at |
| `AutoRefresh` | item | True = keep re-solving so the planes track the camera as you orbit |

| Output | Meaning |
|---|---|
| `Planes` | One camera-facing plane per input point |

**Good to know**
- With `AutoRefresh = True` the component re-solves continuously (throttled to
  about 10 times a second). That uses CPU, so switch it off when you are not
  orbiting. The message on the component shows "Auto-Refreshing" or "Static".
- The plane updates for the **active** viewport; click into the view you want
  to face before relying on it.
- If no view is available, it falls back to a single World XY plane.
- Points can be a single value or a list; a single point now works correctly
  (an earlier version placed it at the world origin).
