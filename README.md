# Bullbull - `TitleBlock` branch

C# script components for **Rhino 8 Grasshopper**: layout / title-block tools.

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
[`main`](https://github.com/pularirajeev77-bit/Bullbull/tree/main) (overview)

## How to use a script

1. In Grasshopper, place a **C# Script** component (Maths > Script).
2. Open its editor and replace everything with the contents of the `.cs` file.
3. The component takes its inputs and outputs from the `RunScript(...)` line.
   If they don't appear, add them by hand with the exact same names.

## Components

| Component | File | What it does |
|---|---|---|
| [BlockAttExtract](#blockattextract) | `BlockAttExtract.cs` | Reads a title block's attribute text (key/value) from each layout |

---

## BlockAttExtract

**File:** `BlockAttExtract.cs`

For each **layout** you name, finds the **title block** (a block instance with
the given name placed on that layout) and returns its **attribute user text** as
key / value pairs - drawing number, title, revision, date and so on.

| Input | Access | Meaning |
|---|---|---|
| `Run` | item | True = read the layouts |
| `BlockName` | item | Title-block definition name (not case-sensitive) |
| `LayoutNames` | list | Layout (page) names to read |

| Output | Meaning |
|---|---|
| `Keys` | Attribute keys, sorted A-Z |
| `Values` | Matching attribute values |
| `FoundLayouts` | Layouts that exist, in input order |

Branch `{k}` of `Keys` and `Values` belongs to `FoundLayouts[k]`.

**Good to know**
- **Alignment fixed:** branches used to be numbered by the *input* position, so
  after a missing layout was skipped, `{2}` no longer matched item 2 of the layout
  list. Now branch `{k}` always belongs to `FoundLayouts[k]`.
- Only real layouts (page views) are searched; a model view with the same name
  can no longer be picked up by mistake. Layout and block names are not
  case-sensitive.
- Locked title blocks are found (they often are locked); only block objects are
  scanned, which is faster on busy layouts.
- Warnings list the layouts that don't exist and the layouts without the block
  (they get an empty branch). If a layout has the block more than once, the first
  is read and a remark says so.
- Renamed output `LNames` -> `FoundLayouts`. Added metadata: a header block
  listing every input/output, component Name / NickName / Description, tooltips on
  every pin, and a Message that shows how many layouts were found (e.g. `3/4 layouts`).
- Not compiled here (no Rhino) - test in Grasshopper.
