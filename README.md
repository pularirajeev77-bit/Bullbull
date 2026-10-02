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
| [CHL](#chl) | `ChangeLayer.cs` | Moves objects from one layer to another |
| [DLM](#dlm) | `DetailLayerManager.cs` | Shows/hides layers inside layout details (v2.1) |
| [DVNE](#dvne) | `DetailViewNameEditor.cs` | Lists layout details in reading order and renames them |

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

---

## CHL

**File:** `ChangeLayer.cs`

Moves **every object from a base layer onto a target layer**, creating the
target layer (and any parents) if it does not exist. Good for re-organising a
model by layer in one step.

| Input | Access | Meaning |
|---|---|---|
| `Run` | item | Toggle True to move objects. Fires once on the True edge |
| `BaseLayer` | tree | Full path(s) of the layer(s) to move objects FROM |
| `TargetLayer` | tree | Full path(s) to move objects TO, matched to `BaseLayer` per branch. Created if missing |

| Output | Meaning |
|---|---|
| `status` | What happened: working, done (with a count), idle, or an error |

**Good to know**
- **Fires once on the rising edge of `Run`.** After it runs, toggle `Run` off
  then on again to repeat - this stops it looping when moving objects re-solves
  the definition.
- `BaseLayer` and `TargetLayer` must have the same branch structure. Within a
  branch, a single target is reused for all base layers; otherwise they match
  by position.
- Target layers use `Parent::Child` for nesting and are created if missing.
- Changes the Rhino document (not Grasshopper); the view refreshes
  automatically, and a missing base layer is noted rather than fatal.

---

## DLM

**File:** `DetailLayerManager.cs`

Controls **which layers are visible inside layout detail views** - isolate,
hide, or show layers per detail, without touching their overall visibility.

| Input | Access | Meaning |
|---|---|---|
| `Run` | item | True to apply, using `Mode`. Cannot be True together with `Reset` |
| `Reset` | item | True to SHOW the listed layers (undo an isolate/hide) |
| `Mode` | item | 0 = Show Only (show listed, hide all others); any other value = Hide listed |
| `Layouts` | list | Layout page names to act on |
| `Details` | list | Detail view names within those layouts |
| `LayerNames` | list | Layer names or full paths (`Parent::Child`) to show/hide |

| Output | Meaning |
|---|---|
| `status` | What happened, with a count of changes, or an error |

**Good to know**
- This sets **per-detail** visibility only - it does not change whether a layer
  is on/off globally.
- **Show Only (`Mode 0`)** hides every other layer in that detail; use `Reset`
  (or Show Only with the full layer list) to bring them back.
- Layers are matched by **name or full path**. A plain name affects every layer
  with that name; use `Parent::Child` to pick one.
- **Show Only keeps parent layers visible**, so a listed sub-layer really shows
  (before, its hidden parent still hid it).
- Missing layouts or details are noted and skipped, not fatal; if no detail
  matched at all you get a warning. Each run is one Undo step.

**Fixes (v2.1)**
- **It changed nothing:** visibility was set with the detail *object's* id, but
  Rhino keys per-detail layer visibility by the detail's **viewport** id
  (`detail.Viewport.Id`). Now uses the viewport id and commits the change.
- **Crash on unnamed details:** a detail without a name made `d.Name.Equals(...)`
  throw, which aborted the whole run with an error. Names are now compared safely.
- **Pin names:** the old tooltip helper set pins by position and also renamed the
  pin's `Name` (the script variable). With the extra `out` pin that shifted names
  and could break the script. Pins are now found by name and only their
  nickname/tooltip are set.
- Only layers whose visibility actually changes are counted.
- Not compiled here (no Rhino) - test in Grasshopper.

---

## DVNE

**File:** `DetailViewNameEditor.cs` &middot; Component name: *DViewNameEditor*

Lists the **detail views on each layout in reading order** - rows from top to
bottom, left to right inside a row - and **renames** them with `DetailNames` in
that order. Handy to give every sheet the same detail names (`PLAN`, `ELEV`,
`SECTION`...) so [DLM](#dlm) can target them by name.

| Input | Access | Meaning |
|---|---|---|
| `Run` | item | True = read the details and apply the names |
| `Layouts` | list | Layout page names |
| `DetailNames` | list | New names in reading order, used for **every** layout. Empty = keep the existing names |

| Output | Meaning |
|---|---|
| `Details` | Detail view objects, path `{layout}`, in reading order |
| `DetailIds` | Their object ids (text) |
| `Rectangles` | Detail frame on the page (null if it has no geometry) |
| `Centers` | Frame centre on the page |
| `Names` | The name each detail has now |

**Good to know**
- **Row fix:** details were sorted strictly by Y, so a row whose details were a
  hair out of line came out in the wrong order (e.g. right-hand detail first).
  Details whose centres are within a quarter of the smallest detail's height now
  count as one row and are read left to right.
- **Aligned branches:** a missing layout used to be skipped, so branch `{i}` no
  longer matched `Layouts[i]`; it now gets an empty branch and a warning.
- Renaming goes through the document (one **Undo** step per run) and the `Names`
  output shows the real name after renaming; the message shows how many were
  renamed. A remark appears when a layout has a different number of details than
  names given.
- `Rectangles` / `Centers` are now plain trees with null for missing frames (the
  old nullable-struct trees may not show in Grasshopper).
- Missing document / layouts are messages on the component (were `Print`).
- Renamed: `LNames, DNames` / `DViews, DIds, DRect, DCent, DOut` ->
  `Layouts, DetailNames` / `Details, DetailIds, Rectangles, Centers, Names`.
- Not compiled here (no Rhino) - test in Grasshopper.
