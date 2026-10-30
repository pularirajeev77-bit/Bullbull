# Bullbull - Annotation

C# script components for **Rhino 8 Grasshopper** that create and manage annotations:
dimensions, text, leaders, tags and labels.

Back to the index: [`main`](https://github.com/pularirajeev77-bit/Bullbull/tree/main)

## Components

| Component | Nickname | What it does |
|---|---|---|
| [AutoDimension](#autodimension) | `AutoDim` | Automatically dimensions curves, lines, polylines and arcs without overlaps |

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

