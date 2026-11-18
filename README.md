# Bullbull - model

C# script components for **Rhino 8 Grasshopper** for modelling: creating, editing and
preparing model geometry.

Back to the index: [`main`](https://github.com/pularirajeev77-bit/Bullbull/tree/main)

## Components

| Component | Nickname | What it does |
|---|---|---|
| [AIExport](#aiexport) | `AI Export` | Exports a geometry tree to Adobe Illustrator (.ai), one file per branch, 1:1, keeping Rhino colors |

## How to use
1. Drop a **C# Script** component on the Grasshopper canvas.
2. Open it, paste the `.cs` file from this branch and close the editor.
3. Inputs, outputs, names and tooltips set themselves on the first run.

---

## AIExport

**File:** `AIExport.cs` &middot; Component name: *AI Export (Tree)* &middot; v1.0.0

Exports geometry and annotations to **Adobe Illustrator (.ai)**, **one file per branch**, at 1:1 in
model units. Colors come from the `Colors` input, otherwise from the Rhino object, then its layer,
then black. The export runs when the button is **released**, after Grasshopper has finished solving,
so the button never freezes.

| Input | Access | Meaning |
|---|---|---|
| `Geometries` | tree | Curves, Breps, meshes, SubDs, points, hatches, text, leaders, dimensions. **Each branch = one .ai file** |
| `Colors` | list | Optional color per object inside a branch (item i -> color i, last one repeats). Overrides Rhino colors |
| `FilePath` | item | Output folder (a file path is accepted - its folder is used). Empty = folder of the saved Rhino file |
| `FileNames` | list | One name per branch, in branch order; `.ai` is added. Missing names = first name (or `Export`) + branch path, e.g. `Plan_0_1.ai` |
| `Export` | item | Button - exports once on release |
| `MoveToOrigin` | item | True = each file's bounding-box lower-left corner goes to world 0,0 |

| Output | Access | Meaning |
|---|---|---|
| `Success` | list | One true/false per branch |
| `Log` | item | Summary plus one line per branch (objects, color source, units, file) |

**Color priority:** `Colors` input -> Rhino object color -> Rhino layer color -> black. Referenced
Rhino geometry is matched by its object ID; plain copies are matched by shape and position.

**Good to know**
- Files with the same name are overwritten. A file open in Illustrator can't be overwritten - close it first.
- Invalid characters in names become `_`; two branches with the same name get `_1`, `_2`.
- The objects are baked temporarily (one undo record), exported with `PreserveUnits=Yes`, `AIScale=1`
  and deleted again. The Top view is used for the export and the previous view restored after.
- Not compiled here (no Rhino) - test in Grasshopper.

**Changelog**
- **v1.0.0** (2026-11-18) - First release in Bullbull: one .ai file per branch, 1:1 with 0,0 kept,
  Rhino or input colors, export on button release (no freeze), tooltips on every pin, active view
  restored, temporary objects in one undo record.
