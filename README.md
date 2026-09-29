# Bullbull — `utility` branch

C# script components for **Rhino 8 Grasshopper**: general utilities.

Other branches: [`TEXT`](https://github.com/pularirajeev77-bit/Bullbull/tree/TEXT) (text tools) ·
[`Vector`](https://github.com/pularirajeev77-bit/Bullbull/tree/Vector) (polyline frames) ·
[`curves`](https://github.com/pularirajeev77-bit/Bullbull/tree/curves) (curve division, lines from points) ·
[`main`](https://github.com/pularirajeev77-bit/Bullbull/tree/main) (overview)

## How to use a script

1. In Grasshopper, place a **C# Script** component (Maths → Script).
2. Open its editor and replace everything with the contents of the `.cs` file.
3. The component takes its inputs and outputs from the `RunScript(...)` line.
   If they don't appear, add them by hand with the exact same names.

**Access types** used below:
**item** = one value at a time · **list** = the whole list at once.

## Components

| Component | File | What it does |
|---|---|---|
| [Calci](#calci) | `BullbullCalculation.cs` | Weight, area and volume of solids (Breps) |
| [RemThruHole](#remthruhole) | `RemoveThroughHoles.cs` | Removes through holes from Brep faces by size or index |
| [PSize](#psize) | `PartSize.cs` | Length, width and height of a box |
| [CrtFold](#crtfold) | `CreateFolder.cs` | Creates a folder on disk when toggled on |
| [DelFiles](#delfiles) | `DeleteFiles.cs` | Deletes files of one extension from a folder |
| [ModFold](#modfold) | `RhinoModelFolder.cs` | Folder of the saved Rhino model and Grasshopper file path |
| [TimerCnt](#timercnt) | `TimerCounter.cs` | Counts up over time; loops, pauses and resets |

---

## Calci

**File:** `BullbullCalculation.cs`

For each solid (Brep), works out its **weight, surface area and volume** in
metric units. Useful for quick material take-offs and mass estimates.

| Input | Access | Meaning |
|---|---|---|
| `Breps` | list | The solids to measure |
| `Density` | item | Material density in **kg/m³** (e.g. steel ≈ 7850, concrete ≈ 2400) |
| `ModelUnits` | item | Your Rhino file's units: `mm`, `cm` or `m` |

| Output | Meaning |
|---|---|
| `Wt` | Weight in kilograms (volume in m³ × `Density`) |
| `Area` | Surface area in square metres (m²) |
| `Vol` | Volume in cubic metres (m³) |
| `VolRaw` | Volume in the model's own units, before conversion |

**Example:** a steel block, `Density = 7850`, `ModelUnits = mm` → `Wt` in kg,
`Area` in m², `Vol` in m³.

**Good to know**
- `ModelUnits` must be one of `mm`, `cm` or `m`. **Any other value gives 0**
  for area, volume and weight — so make sure it matches your Rhino file.
- Set `Density` to match the units you want: `kg/m³` gives kilograms.
- An invalid or open Brep gives `NaN` (not-a-number) for that item, so the
  outputs stay lined up with the input list.
- No input gives empty lists.

---

---

## RemThruHole

**File:** `RemoveThroughHoles.cs`

Removes **through holes** (holes bored all the way through) from Brep faces.
Pick which holes to remove: all of them, or only ones of a certain diameter,
perimeter length or index.

| Input | Access | Meaning |
|---|---|---|
| `Brep` | tree | The Breps to clean; the tree structure is kept |
| `AllHoles` | item | True to remove every through hole (ignores the three inputs below) |
| `TargetDiameters` | list | Remove through holes whose diameter matches one of these (within `Tolerance`). Diameter = loop length / pi |
| `TargetLengths` | list | Remove through holes whose perimeter length matches one of these (within `Tolerance`) |
| `HoleIndices` | list | Remove through holes by number (0 = first; `-1` = last), per Brep |
| `Tolerance` | item | Match/geometry tolerance. 0 or less uses the model tolerance |

| Output | Meaning |
|---|---|
| `Result` | The Breps with the matching through holes removed, same tree structure as the input |

**Example:** to remove every 20 mm through hole, feed `TargetDiameters = 20`
(with the file in mm). To remove all through holes, set `AllHoles = true`.

**Good to know**
- **Through holes only.** Blind holes and pockets (that do not pass all the
  way through) are detected and matched, but cannot be filled, so those Breps
  come back unchanged with no error.
- Diameter is worked out as perimeter / pi, so it is exact for round holes and
  only approximate for other shapes - match those by `TargetLengths` instead.
- The criteria add up: a hole is removed if it matches **any** of index,
  diameter or length.
- If no criteria are given, the Breps pass through unchanged with a warning.
- Empty or null input gives an empty result with a warning.

---

## PSize

**File:** `PartSize.cs`

Measures the **length, width and height** of each box, in the model's units.
Handy for part lists and cut sheets.

| Input | Access | Meaning |
|---|---|---|
| `Box` | item | The box(es) to measure (item, list or tree) |

| Output | Meaning |
|---|---|
| `Length` | The larger of the two base edges |
| `Width` | The smaller of the two base edges |
| `Height` | The box height (its Z size in the box's own frame) |

**Good to know**
- Length is always the longer base edge and Width the shorter, so the two never swap around between parts.
- All three values are rounded to 2 decimals.
- An invalid box gives `NaN` for that item, keeping the outputs lined up with the input.
- Height follows the box's own orientation, not world Z, so a tilted box still measures correctly.

---

## CrtFold

**File:** `CreateFolder.cs`

Creates a **folder on disk** at `Path\Name`. It only writes when `Create` is
True, so you can wire it up and preview the path safely first.

| Input | Access | Meaning |
|---|---|---|
| `Path` | item | Base folder that will contain the new one (e.g. `C:\Jobs`) |
| `Name` | item | Name of the folder to create. May include sub-folders (`a\b` makes both) |
| `Create` | item | True = create it now. False = just show the path, write nothing |

| Output | Meaning |
|---|---|
| `FolderPath` | The full path (`Path` + `Name`), whether or not it was created |
| `Msg` | Status: created, already exists, waiting for Create, or an error |

**Good to know**
- With `Create = False` nothing is written - `FolderPath` still shows where it
  would go, so you can check before committing.
- Missing parent folders are created automatically.
- A leading slash on `Name` is stripped, so the folder always lands inside
  `Path` (not at the drive root).
- If the folder already exists, it is left as-is and `Msg` says so.
- Errors (bad path, no permission) appear in `Msg` and as a warning on the component.

---

## DelFiles

**File:** `DeleteFiles.cs`

Deletes every file with a chosen extension from a folder. **Permanent - the
files do not go to the Recycle Bin.** It only runs when `Button` is True, so
wire it up first and check the folder before triggering.

| Input | Access | Meaning |
|---|---|---|
| `FolderPath` | item | Folder to delete files from (this folder only, not sub-folders) |
| `FileExtension` | item | Extension to match, e.g. `.bak` or `txt` |
| `Button` | item | Set True (use a Button) to delete now |

| Output | Meaning |
|---|---|
| `Deleted` | Names of the files deleted (and any that failed, with the reason) |
| `Msg` | Summary of what happened |

**Good to know**
- **Deletion is permanent.** There is no undo and nothing goes to the Recycle
  Bin, so double-check `FolderPath` and `FileExtension` before pressing the button.
- Only exact-extension matches are deleted: `.bak` will not also catch `.bak2`
  (this guards against a Windows wildcard quirk where `*.xls` also matches `.xlsx`).
- Only the named folder is touched, not its sub-folders.
- Files in use are skipped and listed with their error in `Deleted`.
- With `Button = False` nothing is deleted.

---

## ModFold

**File:** `RhinoModelFolder.cs`

Gets the **folder of the saved Rhino model** and the **full path of the saved
Grasshopper file** - handy for saving exports next to the current file.

| Input | Access | Meaning |
|---|---|---|
| `Get` | item | Set True to read the paths. False = idle |

| Output | Meaning |
|---|---|
| `Rh_path` | Folder that contains the saved `.3dm` file |
| `Gh_path` | Full path of the saved Grasshopper file (`.gh` / `.ghx`) |

**Good to know**
- Both work only **after the files are saved**. If the Rhino model or the
  Grasshopper file has never been saved, that output says so and the component
  shows a warning.
- `Rh_path` is the folder; `Gh_path` is the full file path (including the file name).
- `Gh_path` reads this component's own document, so it is correct even if
  another Grasshopper window is in focus.

---

## TimerCnt

**File:** `TimerCounter.cs`

A **timed counter**: counts up from `start` to `end`, one step every `delay`
milliseconds. Good for driving animations, step-throughs or slideshows.

| Input | Access | Meaning |
|---|---|---|
| `start` | item | First count value |
| `end` | item | Last count value (must be greater than `start`) |
| `delay` | item | Milliseconds per step. 0 or less uses 100 |
| `loop` | item | True = jump back to `start` after `end` instead of stopping |
| `Run` | item | True = count; False = pause (keeps the current value) |
| `reset` | item | True = set the count back to `start` and stop |

| Output | Meaning |
|---|---|
| `I` | Current count value |
| `pct` | Progress from `start` to `end`, 0-100 % |

**Good to know**
- **Each component keeps its own state**, so you can run several timers at once
  without them interfering (the original shared one counter across all of them).
- Counts **up only**: `end` must be greater than `start`. If not, it just
  returns `start` at 100 %.
- While `Run` is True the component re-runs itself continuously, which uses some
  CPU - switch `Run` off when you don't need it.
- Pausing keeps the current value; `reset` returns it to `start`.
