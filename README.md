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
| [LFT](#lft) | `LayoutFromTemplate.cs` | Creates new layouts (list or tree of names) from one or more template layouts - in this file or picked from another Rhino file |

---

## LFT

**File:** `LayoutFromTemplate.cs` &middot; Component name: *Layout From Template* &middot; v2.3

**Creates one new layout per name**, each a copy of a **template layout**: page size,
detail views (position, view, scale, projection lock, display mode, layer) and all page-space
objects (title block, text, lines ...). Use **one template or a list of templates**, and names as a
**list or a tree**. Templates can be layouts of **this file** or **picked from another Rhino file** -
either way, only the layouts in `Names` are added.

| Input | Access | Meaning |
|---|---|---|
| `Run` | item | Toggle - layouts are created while it is True |
| `Template` | list | Template layout(s) - names (case-insensitive) or numbers (`1` = first layout). With `TemplateFile`: layouts of **that** file |
| `Names` | tree | Names of the **new layouts to create in this file** - a list or a tree. Only these are added |
| `TemplateFile` | item | Optional. Full path of the `.3dm` file that holds the template. Empty = use a layout of this file |

| Output | Meaning |
|---|---|
| `Report` | Where the template came from, then created / skipped / failed per name |

### Several templates: how names are paired

| `Template` | `Names` | Result |
|---|---|---|
| `A1` | `GA-01, GA-02, GA-03` | all three from `A1` |
| `A1, A3, Cover` | `GA-01, DT-01, CV-01` (flat list) | `GA-01`←`A1`, `DT-01`←`A3`, `CV-01`←`Cover` - item by item |
| `A1, A3` | `GA-01, DT-01, DT-02` (flat list) | `GA-01`←`A1`, `DT-01`←`A3`, `DT-02`←`A3` - last template reused |
| `A1, A3` | tree `{0}` `GA-01, GA-02` · `{1}` `DT-01, DT-02, DT-03` | branch `{0}` ← `A1`, branch `{1}` ← `A3` |
| `A1` | any tree | every name from `A1` |

The component reads **all** templates and names at once, whatever access the pins are set to, so an
older component whose `Template` pin is still *item* access works the same.

A shorter `Template` list reuses its last template. Templates left over (more templates than names or
branches) are not used - the component shows a remark. The `Report` lists each new layout with its template.

### Template from another Rhino file

Example: `Sheets.3dm` has 5 layouts - `A1 Cover`, `A1 Plan`, `A1 Section`, `A3 Detail`, `A3 Notes`.
You want three new plan sheets in the current file:

| Input | Value |
|---|---|
| `TemplateFile` | `C:\Standards\Sheets.3dm` |
| `Template` | `A1 Plan` (or `2`) |
| `Names` | `GA-01`, `GA-02`, `GA-03` |

Result in the current file: **only** `GA-01`, `GA-02`, `GA-03`, each a copy of `A1 Plan`.
`A1 Plan` itself and the other four layouts of `Sheets.3dm` are **not** added.

How it works:
1. Only the chosen template layout(s) are imported from the other file (Rhino's ImportLayout) - each
   **once**, as a temporary source, however many names use it.
2. The layouts in `Names` are copied from them.
3. The temporary template layouts are **deleted again**, with their page objects, plus any layers that
   came with the import and ended up empty. Layers used by the new layouts (title block, details) stay.

### Good to know
- Names that already exist (or are blank / repeated) are skipped. When **every** name exists, nothing is
  imported at all - so `Run` can stay on.
- With `TemplateFile`, the template is always taken from that file. A layout with the same name in the
  current file is never used or changed.
- Each imported layout is **checked by name**. If Rhino's ImportLayout brings a different layout (it may
  ignore a name), that one is removed and the file is searched layout by layout until the name matches.
  The `Report` shows which layout every template resolved to, e.g. `Template 'A3' -> layout 3 'A3'`.
- A number in `Template` (1 = first layout of that file) imports that layout directly.
- Creating the layouts is one **Undo** step (the import is its own step before it).
- Details copy the template detail's **whole view** - camera, projection and zoom - so the scale is the
  same as the template's.
- Not copied: per-detail layer visibility overrides.
- Not compiled here (no Rhino) - test in Grasshopper, first on a copy of the file.

### Changelog

**v2.3** (2026-10-29)
- **Fix - all names came from the first template.** When the pins kept an older access (`Template` = item),
  Grasshopper ran the script once per template and the first run made every name from template 1.
  All templates and names are now read straight from the inputs in a single run.
- **Fix - wrong layout imported.** ImportLayout may ignore the layout name and bring the same layout
  each time. The imported layout's name is now checked; if it is not the one asked for, it is removed
  and the file is searched layout by layout.
- `Report` shows which layout each template resolved to.

**v2.2** (2026-10-29)
- `Template` is a **list** and `Names` a **tree**: several templates to several names - item by item for
  a flat list, branch by branch for a tree, last template reused (see the pairing table).
- Each template is imported once and all imported templates are removed after copying.
- `Report` shows the template of every created layout; a remark when templates are left over.

**v2.1** (2026-10-29)
- From another file, the template layout is only **borrowed**: imported, copied, then deleted, so only the
  layouts in `Names` are added. Imported layers that end up empty are removed; any extra layout an import
  brings is removed straight away.
- With `TemplateFile` set, a same-named layout in the current file is never used.
- Nothing is imported when all names already exist; repeated names in `Names` are skipped.

**v2.0** (2026-10-29)
- New `TemplateFile` input; `Template` also accepts the layout number.
- **Detail scale fix:** `SetScale(PageToModelRatio, ..., 1.0, ...)` passed the ratio as the *model* length
  (1:100 became 100:1). Each detail now copies the template detail's whole view.

**v1.0** (2026-10-12) - first version.
