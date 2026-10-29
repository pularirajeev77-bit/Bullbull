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
| [LFT](#lft) | `LayoutFromTemplate.cs` | Creates new layouts by copying a template layout - from this file or another Rhino file |

---

## LFT

**File:** `LayoutFromTemplate.cs` &middot; Component name: *Layout From Template* &middot; v2.0

**Creates one new layout per name**, each a copy of a template layout: page size,
detail views (position, view, scale, projection lock, display mode, layer) and
all page-space objects (title block, text, lines ...). The template can be a layout in
**this file** or in **another Rhino file**.

| Input | Access | Meaning |
|---|---|---|
| `Run` | item | Toggle - layouts are created while it is True |
| `Template` | item | Template layout name (case-insensitive) - or its number (`1` = first layout) |
| `Names` | list | Names for the new layouts |
| `TemplateFile` | item | Optional. Full path of another `.3dm` file to take the template from. Empty = this file |

| Output | Meaning |
|---|---|
| `Report` | Where the template came from, then created / skipped / failed per name |

### Template from another Rhino file

Set `TemplateFile` to the other file, e.g. `C:\Standards\A1-Sheets.3dm`, and `Template` to the
layout in that file:

1. On the first run the component imports that layout with Rhino's **ImportLayout** command -
   its title block, text, page objects and details come in as a normal layout of this file.
2. The new layouts in `Names` are then copied from it, exactly as for a local template.
3. On later runs the imported layout is already in the file, so it is **reused, not imported again**.

**Good to know about importing**
- Rhino's ImportLayout also brings **all layers** of the other file (a known Rhino behaviour),
  plus only the geometry placed **on** that layout - not the model geometry seen in its details.
- If a layout with the template's name already exists in this file, that one is used and nothing
  is imported. Rename or delete it to import a fresh copy.
- If the name is not accepted by ImportLayout, put the layout's **number** in `Template`
  (1 = first layout of that file); the report then names the layout that came in.
- The imported template stays in the file - delete it afterwards if you don't need it.

### Good to know
- Names that already exist (or are blank) are skipped, so re-running is safe.
- Set `Run` back to False when done, or it re-runs on every solve.
- Creating the layouts is one **Undo** step (an import is its own step before it).
- Details copy the template detail's **whole view** - camera, projection and zoom - so the scale is
  the same as the template's.
- Not copied: per-detail layer visibility overrides.
- Not compiled here (no Rhino) - test in Grasshopper, first on a copy of the file.

### Changelog

**v2.0** (2026-10-29)
- **New `TemplateFile`:** take the template layout from another `.3dm` file (imported once).
- `Template` also accepts the layout number.
- **Detail scale fix:** the scale was set with `SetScale(PageToModelRatio, ..., 1.0, ...)`, which passes
  the ratio as the *model* length and turns e.g. 1:100 into 100:1. Each detail now copies the template
  detail's whole view, so its scale (and perspective lens) match exactly.
- Component name *Layout From Template*; tooltips for every pin.

**v1.0** (2026-10-12) - first version.
