# Bullbull - `layout` branch

C# script components for **Rhino 8 Grasshopper**: layout (page view) tools.

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
[`Tree`](https://github.com/pularirajeev77-bit/Bullbull/tree/Tree) &middot;
[`TitleBlock`](https://github.com/pularirajeev77-bit/Bullbull/tree/TitleBlock) &middot;
[`intersect`](https://github.com/pularirajeev77-bit/Bullbull/tree/intersect) &middot;
[`shape`](https://github.com/pularirajeev77-bit/Bullbull/tree/shape) &middot;
[`grid`](https://github.com/pularirajeev77-bit/Bullbull/tree/grid) &middot;
[`main`](https://github.com/pularirajeev77-bit/Bullbull/tree/main) (overview)

## How to use a script

1. In Grasshopper, place a **C# Script** component (Maths > Script).
2. Open its editor and replace everything with the contents of the `.cs` file.
3. The component takes its inputs and outputs from the `RunScript(...)` line.
   If they don't appear, add them by hand with the exact same names.

**Access types:** **item** = one value at a time, **list** = many values.

## Components

| Component | File | What it does |
|---|---|---|
| [LFT](#lft) | `LayoutFromTemplate.cs` | Creates new layouts by copying a template layout |

---

## LFT

**File:** `LayoutFromTemplate.cs` &middot; Component name: *LayoutFromTemplate*

**Creates one new layout per name**, each a copy of a reference layout: page size,
detail views (position, camera, scale, projection lock, display mode, layer) and
all page-space objects (title block, text, lines ...).

| Input | Access | Meaning |
|---|---|---|
| `Run` | item | Toggle - layouts are created while it is True |
| `Template` | item | Name of the reference layout to copy |
| `Names` | list | Names for the new layouts |

| Output | Meaning |
|---|---|
| `Report` | Created / skipped / failed per name |

**Good to know**
- Names that already exist (or are blank) are skipped, so re-running is safe.
- Set `Run` back to False when done, or it re-runs on every solve.
- The whole run is one **Undo** step.
- Not copied: per-detail layer visibility overrides and perspective lens length.
- Metadata (name, nickname, message, description) and pin tooltips are set by the
  script on first run.
- Not compiled here (no Rhino) - test in Grasshopper.
