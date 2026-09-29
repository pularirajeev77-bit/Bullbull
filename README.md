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
| [RemHole](#remhole) | `RemoveHoles.cs` | Removes holes from Brep faces by size or index |

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

## RemHole

**File:** `RemoveHoles.cs`

Removes **holes** (inner loops) from Brep faces. Pick which holes to remove:
all of them, or only ones of a certain diameter, perimeter length or index.

| Input | Access | Meaning |
|---|---|---|
| `Brep` | tree | The Breps to clean; the tree structure is kept |
| `AllHoles` | item | True to remove every hole (ignores the three inputs below) |
| `TargetDiameters` | list | Remove holes whose diameter matches one of these (within `Tolerance`). Diameter = loop length / pi |
| `TargetLengths` | list | Remove holes whose perimeter length matches one of these (within `Tolerance`) |
| `HoleIndices` | list | Remove holes by number (0 = first; `-1` = last), per Brep |
| `Tolerance` | item | Match/geometry tolerance. 0 or less uses the model tolerance |

| Output | Meaning |
|---|---|
| `Result` | The Breps with the matching holes removed, same tree structure as the input |

**Example:** to remove every 20 mm hole, feed `TargetDiameters = 20` (with the
file in mm). To remove all holes, set `AllHoles = true`.

**Good to know**
- Diameter is worked out as perimeter / pi, so it is exact for round holes and
  only approximate for other shapes - match those by `TargetLengths` instead.
- The criteria add up: a hole is removed if it matches **any** of index,
  diameter or length.
- If no criteria are given, the Breps pass through unchanged with a warning.
- Empty or null input gives an empty result with a warning.
