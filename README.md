# Bullbull
Rhino grasshopper codes

C# script components for **Rhino 8 Grasshopper**. The scripts are grouped by
topic into branches — switch branch to see the code and full instructions.

## Branches

| Branch | Components |
|---|---|
| [`TEXT`](https://github.com/pularirajeev77-bit/Bullbull/tree/TEXT) | Name-Format, Find&Replace, Search Text, Multi > Single-Line-Text, Single > Multi-Line-Text, GetNumbers, GetText, Plane><Text, Leader Points |
| [`Vector`](https://github.com/pularirajeev77-bit/Bullbull/tree/Vector) | Bisect Frame |
| [`curves`](https://github.com/pularirajeev77-bit/Bullbull/tree/curves) | CenterDiv, PVL, CrvClass, IntAng, IntAngDom, AdaptDiv, PolyPlus, VarChamfer |
| [`utility`](https://github.com/pularirajeev77-bit/Bullbull/tree/utility) | Calci, RemThruHole, PSize |

## All components

| Component | Branch | What it does |
|---|---|---|
| Name-Format | `TEXT` | Sequential names like `B001, B002, B003` |
| Find&Replace | `TEXT` | Replaces several words in text at once |
| Search Text | `TEXT` | True/false: does the text contain any of the words? |
| Multi > Single-Line-Text | `TEXT` | Joins a list of lines into one text |
| Single > Multi-Line-Text | `TEXT` | Splits text into separate lines |
| GetNumbers | `TEXT` | Pulls the numbers out of text (`M20x100` → `20, 100`) |
| GetText | `TEXT` | Keeps only the letters (`Beam 12 Column` → `Beam Column`) |
| Plane><Text | `TEXT` | Plane → text and text → plane, for storing planes in Excel/CSV |
| Leader Points | `TEXT` | Leader line with a landing leg, from a plane |
| Bisect Frame | `Vector` | Point, bisector vector and plane at every polyline vertex |
| CenterDiv | `curves` | Divides a curve symmetrically from its middle |
| PVL | `curves` | Lines from a point, a direction and a length |
| CrvClass | `curves` | Sorts curves by type: line, polyline, arc, circle, ellipse … |
| IntAng | `curves` | Finds the sharp corners of a polyline |
| IntAngDom | `curves` | Finds polyline corners whose angle is within a range |
| AdaptDiv | `curves` | Divides a curve with more points where it bends more |
| PolyPlus | `curves` | Polyline through points, with chosen stretches as arcs |
| VarChamfer | `curves` | Chamfers chosen polyline corners, each with its own distance |
| Calci | `utility` | Weight, area and volume of solids (Breps) |
| RemThruHole | `utility` | Removes through holes from Brep faces by size or index |
| PSize | `utility` | Length, width and height of a box |

## How to use a script

1. In Grasshopper, place a **C# Script** component (Maths → Script).
2. Open its editor and replace everything with the contents of the `.cs` file.
3. The component takes its inputs and outputs from the `RunScript(...)` line.
   If they don't appear, add them by hand with the exact same names.

Each branch's README explains every input, output and option of its components.
