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
| [CadExport](#cadexport) | `CadExport.cs` | Exports points, curves and text to a DWG/DXF file on one layer |
| [SolidExport](#solidexport) | `SolidExport.cs` | Exports one DWG/DXF per branch with breps as ACIS solids ("2018 Solid" scheme) |

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

---

## CadExport

**File:** `CadExport.cs`

Writes points, curves and text straight from Grasshopper to a **DWG or DXF**
file, all on one layer. The objects are baked only for a moment, exported, then
deleted again - nothing is left in your Rhino model.

| Input | Access | Meaning |
|---|---|---|
| `Export` | item | Exports once each time it goes False -> True (use a **Button**) |
| `FileType` | item | `DWG` or `DXF` |
| `Folder` | item | Existing folder to save into |
| `FileName` | item | File name without extension (existing file is overwritten) |
| `LayerName` | item | Layer for the objects, nested `Parent::Child` OK (empty = `CadExport`) |
| `LayerColor` | item | Layer colour (empty = black) |
| `Points` | list | Points to export |
| `Curves` | list | Curves, lines, polylines, arcs, circles... |
| `TextPoints` | list | Insertion point of each text (text is centred on it) |
| `Texts` | list | Text strings, paired with `TextPoints` by index |
| `TextSize` | item | Text height (0 or less = no text) |

| Output | Meaning |
|---|---|
| `Result` | Status of the last export |

**Good to know**
- **Exports once per press.** The old version re-exported (bake + export +
  delete) on every recompute while `export` was True.
- Temporary objects are **always** deleted, even if the export fails (before, an
  error left them in the model), and your previous selection is restored.
- An existing file is deleted first - Rhino's "replace?" prompt would otherwise
  break the scripted export. Success is confirmed by checking the file exists.
- The layer is made visible and unlocked so its objects can be selected for
  export (a hidden/locked layer exported nothing).
- Accepts wrapped Grasshopper data: lines, polylines, arcs etc. now export as
  curves (the old `is Curve` check skipped them); unconvertible items are counted
  in a warning. Colour accepts colour, text (`255,0,0`) or names.
- Text uses Rhino 8's `TextEntity.Create` with centre/middle alignment (the old
  `Justification` property is obsolete). Mismatched `Texts` / `TextPoints` counts
  give a warning.
- Empty `LayerName` or `FileName` no longer crashes; `.dwg`/`.dxf` typed into
  `FileName` is stripped.
- Inputs/outputs renamed from `export, exportType, filePath, fileName,
  layerName, layerColor, points, curves, textLocs, texts, textSize` / `result`.
- Export options use Rhino's current DWG/DXF export scheme.
- Not compiled here (no Rhino) - test in Grasshopper.

---

## SolidExport

**File:** `SolidExport.cs`

Exports **one DWG/DXF file per tree branch**: branch `i` of `Solids`, `Points`
and `Annotations` goes into the file named by branch `i` of `FileNames`. It
forces Rhino's **"2018 Solid"** export scheme so breps arrive in AutoCAD as ACIS
solids instead of wireframe. Objects are baked only for the export and removed
again.

| Input | Access | Meaning |
|---|---|---|
| `Export` | item | Exports once each time it goes False -> True (use a **Button**) |
| `FileType` | item | `DWG` or `DXF` (empty = DWG) |
| `FileNames` | tree | One name per branch (first item, no extension) |
| `Folder` | item | Existing folder to save into |
| `Solids` | tree | Breps per file; open planar holes are capped |
| `Points` | tree | Points per file (same layer as the solids) |
| `Annotations` | tree | Text, leaders, dimensions, curves per file |
| `AnnotationLayers` | list | Annotation layer per file, cycles (empty = `Annotations`) |
| `AnnotationColors` | list | Annotation colour per file, cycles (empty = black) |
| `LayerNames` | list | Solid/point layer per file, cycles, `Parent::Child` OK (empty = `Solid_Geometry`) |
| `LayerColors` | list | Solid/point colour per file, cycles (empty = black) |

| Output | Meaning |
|---|---|
| `ExportedFiles` | Paths of the files written by the last press |

**Good to know**
- Needs an export scheme called **`2018 Solid`** in Rhino's DWG/DXF export
  options (create it once in the Export dialog). The name is the `SCHEME`
  constant in the script if yours differs.
- **Exports once per press** - the old version re-exported every file on every
  recompute while `export` was True.
- Temporary objects are **always** removed, even when a file fails; your
  selection is restored.
- Existing files are overwritten (deleted first, since Rhino's "replace?" prompt
  would break the command). A file only counts as exported if it exists
  afterwards - before, a failed export could still be reported as success.
- Layers are made visible/unlocked (hidden/locked layers exported nothing);
  nested layers and empty layer names now work.
- Annotations of any type are baked (dimensions and hatches were skipped before);
  objects are added with their layer/colour directly instead of edited afterwards.
- Messages now show on the component (the old `Print` output was easy to miss);
  open breps are counted in a warning per file.
- Inputs/outputs renamed from `export, exportType, fileNames, outputFolder, breps,
  points, annotations, annoLayers, annoColors, layerNames, layerColors` /
  `outputFiles`.
- Not compiled here (no Rhino) - test in Grasshopper.
