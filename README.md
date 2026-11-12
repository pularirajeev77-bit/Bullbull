# Bullbull - Annotation

C# script components for **Rhino 8 Grasshopper** that create and manage annotations:
dimensions, text, leaders, tags and labels.

Back to the index: [`main`](https://github.com/pularirajeev77-bit/Bullbull/tree/main)

## Components

| Component | Nickname | What it does |
|---|---|---|
| [AutoDimension](#autodimension) | `AutoDim` | Automatically dimensions curves, lines, polylines and arcs without overlaps |
| [DetailMark](#detailmark) | `DetMark` | Detail mark: circle, tail with arrow flag and label on a plane, plus a clip plane |

## How to use
1. Drop a **C# Script** component on the Grasshopper canvas.
2. Open it, paste the `.cs` file from this branch and close the editor.
3. The component name, nickname and the tooltips set themselves on the first run.
   The input/output **nicknames** are the variable names in the code - set them by hand
   exactly as listed for each component below (they are not renamed by the script).

---

## AutoDimension

**File:** `AutoDimension.cs`

Dimensions one or more drawings automatically: the length of every segment, angles at
the corners (inside angle on closed shapes), corner position chains along the sides,
overall width and height, arc radii, hole diameters, a single leader for holes of the
same size, and a tag for every named hole. Each dimension is placed so its text does not
sit on a line, on another text, or on another dimension line, and a final clean-up slides
any text that still clashes to the nearest free spot. Curves are joined only for
measuring - the model curves are not changed. A thing that is already dimensioned is not
dimensioned again.

| Input | Nickname to set | Access | Meaning |
|---|---|---|---|
| Curves | `Curves` | tree | Curves, lines, polylines or arcs to dimension. **One branch = one drawing**; a later branch keeps clear of the dimensions of earlier ones. Closed circles are treated as holes |
| Bake | `Bake` | item | `True` writes the dimensions into the Rhino model (one Ctrl+Z undoes it). `False` (default) only previews them |

| Output | Nickname to set | Meaning |
|---|---|---|
| Dimensions | `Dimensions` | The annotation objects (dimensions, text, leaders), one branch per drawing |
| Report | `Report` | A text summary per drawing: what was added, any overlaps left, what was already dimensioned |

**Layers** (created when baking): `Drawing::Dimension` (lengths, angles), `Drawing::Text`
(notes, corner letters), `Drawing::Marks` (radii, hole diameters and counts),
`Drawing::Tag` (name leaders).

**Notes**
- Text size and spacing come from the *current* Rhino dimension style, scaled once for the
  whole drawing so every dimension has the same size.
- Right angles (90° / 270°) are not dimensioned.
- Separate loose lines must meet exactly (within the model tolerance) to be joined into
  one outline - otherwise each line is measured on its own.
- The Report line says how many texts could not be placed fully clear; check those by eye.

---

## DetailMark

**File:** `DetailMark.cs` &middot; Component name: *DetailMark* &middot; v1.2

Draws a detail mark in the style of the SectionMark family: a circle standing perpendicular
to the detail plane (circle normal = plane X, the view direction), a tail leaving the circle
edge along plane -Y, an arrow flag at the tail end pointing along plane X, and a label just
beyond the flag. `FlipMark` sends tail, flag and label to the other side. It also outputs a
clip plane built from the detail plane.

| Input | Access | Meaning |
|---|---|---|
| `DetailPlane` | item | Placement of the mark. X = view direction, the tail runs along -Y. Invalid = World XY |
| `Radius` | item | Circle radius, default 1. Tail length = Radius x 1.333 |
| `MarkScale` | item | Size of the arrow flag (flag = 1 x MarkScale), default 1 |
| `TextHeight` | item | Label text height, default 1 |
| `LabelText` | item | Label text, default `DET-1` |
| `FlipMark` | item | True = tail, flag and label on the plane +Y side. Default false |

| Output | Access | Meaning |
|---|---|---|
| `Mark` | list | The circle and the tail polyline with its flag |
| `Label` | item | Label as a text entity (bakeable) |
| `ClipPlane` | item | Origin = plane origin, X = -Y, Y = -Z of the detail plane |

**Notes**
- The label uses the current Rhino dimension style (font), with `TextHeight` as its height.
- Proportions of tail, flag inset and text gap are constants at the top of the methods
  (`TailRatio`, `FlagBack`, `TextGap`) - edit them there.
- Not compiled here (no Rhino) - test in Grasshopper.

**Changelog**
- **v1.2** (2026-11-12) - Metadata and tooltips set once; no crash without an active document;
  invalid sizes fall back to the defaults.
- **v1.1** - First version.
