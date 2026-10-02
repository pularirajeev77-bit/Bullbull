# Bullbull
Rhino grasshopper codes

C# script components for **Rhino 8 Grasshopper**. The scripts are grouped by
topic into branches — switch branch to see the code and full instructions.

## Branches

| Branch | Components |
|---|---|
| [`TEXT`](https://github.com/pularirajeev77-bit/Bullbull/tree/TEXT) | Name-Format, Find&Replace, Search Text, Multi > Single-Line-Text, Single > Multi-Line-Text, GetNumbers, GetText, Plane><Text, Leader Points, Format-RealNumbers |
| [`Vector`](https://github.com/pularirajeev77-bit/Bullbull/tree/Vector) | Bisect Frame |
| [`curves`](https://github.com/pularirajeev77-bit/Bullbull/tree/curves) | CenterDiv, PVL, CrvClass, IntAng, IntAngDom, AdaptDiv, PolyPlus, VarChamfer |
| [`utility`](https://github.com/pularirajeev77-bit/Bullbull/tree/utility) | Calci, RemThruHole, PSize, CrtFold, DelFiles, ModFold, TimerCnt |
| [`point`](https://github.com/pularirajeev77-bit/Bullbull/tree/point) | CullDupPt, FarPts, ClosePts, SideSort, PtOnCrv, RadSort, WeightSort, UVSort |
| [`surface`](https://github.com/pularirajeev77-bit/Bullbull/tree/surface) | EdgeAn, DraftThick, SrfExt, SmoothGeo, BlendAnalyze |
| [`Plane`](https://github.com/pularirajeev77-bit/Bullbull/tree/Plane) | CamPlane |
| [`Layers`](https://github.com/pularirajeev77-bit/Bullbull/tree/Layers) | CurrLyr, LBS, CHL, DLM, DVNE, TDL, ViewGen |
| [`display`](https://github.com/pularirajeev77-bit/Bullbull/tree/display) | Zebra, GoldEmap, CrvProp, DynSplit |
| [`SpaceFrame`](https://github.com/pularirajeev77-bit/Bullbull/tree/SpaceFrame) | sharedNodes, nodeSize, HardLook |
| [`Creation`](https://github.com/pularirajeev77-bit/Bullbull/tree/Creation) | Bake, CadExport, SolidExport, CNCExport |
| [`Tree`](https://github.com/pularirajeev77-bit/Bullbull/tree/Tree) | CreateTree, TreeSwap, TreeSwap+ |
| [`TitleBlock`](https://github.com/pularirajeev77-bit/Bullbull/tree/TitleBlock) | BlockAttExtract, KeySearch, BlockAttEditor, SrchBlk |
| [`intersect`](https://github.com/pularirajeev77-bit/Bullbull/tree/intersect) | CrvPlaneInt, DirFilter, CoplanarFilter |

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
| Format-RealNumbers | `TEXT` | Number -> text with exactly 14 decimals (exact value, no e-notation) |
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
| CrtFold | `utility` | Creates a folder on disk when toggled on |
| DelFiles | `utility` | Deletes files of one extension from a folder |
| ModFold | `utility` | Folder of the saved Rhino model and Grasshopper file path |
| TimerCnt | `utility` | Counts up over time; loops, pauses and resets |
| CullDupPt | `point` | Removes duplicate points within a tolerance |
| FarPts | `point` | Finds the two points that are farthest apart |
| ClosePts | `point` | Finds the two points that are closest together |
| SideSort | `point` | Sorts points into left and right of a curve |
| PtOnCrv | `point` | Tests which points lie on curves, per branch |
| RadSort | `point` | Sorts points counter-clockwise around a plane |
| WeightSort | `point` | Sorts points by a weighted key of X, Y, Z |
| UVSort | `point` | Sorts points row by row by their UV on a surface |
| EdgeAn | `surface` | Sorts a Brep's edges: outer, inner, naked, interior, non-manifold |
| DraftThick | `surface` | Thickens a surface/polysurface/closed brep along its normals with a draft angle |
| SrfExt | `surface` | Extends surfaces by a distance on each side (N/E/S/W) |
| SmoothGeo | `surface` | Smooths meshes, curves and surfaces (Rhino Smooth) with axis / coordinate control |
| BlendAnalyze | `surface` | Blend surface between two brep edges (G0-G2) + its CVs, weights, Greville |
| CamPlane | `Plane` | Planes at points that face the camera |
| CurrLyr | `Layers` | Sets the current layer, creating it if needed |
| LBS | `Layers` | Creates and styles layer trees (colour + linetype) |
| CHL | `Layers` | Moves objects from one layer to another |
| DLM | `Layers` | Shows/hides layers inside layout details |
| DVNE | `Layers` | Lists layout details in reading order and renames them |
| TDL | `Layers` | Locks / unlocks all detail views on layouts with one button |
| ViewGen | `Layers` | Zooms to objects and saves a Named View (or clears all) |
| Zebra | `display` | Zebra-stripe surface analysis in the viewport |
| GoldEmap | `display` | Brushed-gold reflection map on geometry |
| CrvProp | `display` | Reports a curve's length, domain, type and more |
| DynSplit | `display` | Live section: slide/rotate a cutting plane, get section curves and the kept side |
| sharedNodes | `SpaceFrame` | Groups curves by the nodes (points) they start or end at |
| nodeSize | `SpaceFrame` | Sizes a node from the smallest angle between its members |
| HardLook | `SpaceFrame` | Looks up bolt/sleeve/cone/thread sizes for a pipe diameter from Excel (reference table [`HardLook.xlsx`](https://github.com/pularirajeev77-bit/Bullbull/blob/SpaceFrame/HardLook.xlsx)) |
| Bake | `Creation` | Bakes geometry with name, layer, colour, print width, isocurves and grouping |
| CadExport | `Creation` | Exports points, curves and text to a DWG/DXF file on one layer |
| SolidExport | `Creation` | Exports one DWG/DXF per branch with breps as ACIS solids |
| CNCExport | `Creation` | Exports one DWG/DXF per panel with per-layer colours and linetypes |
| CreateTree | `Tree` | Sorts a flat list into branches by index (like Elefront Create Tree) |
| TreeSwap | `Tree` | Swaps the first two path indices ({A;B} -> {B;A}) |
| TreeSwap+ | `Tree` | Swaps indices for 2+ names, prefixes {0} for 1 name |
| BlockAttExtract | `TitleBlock` | Reads a title block's attribute text (key/value) from each layout |
| KeySearch | `TitleBlock` | Flags which title-block keys contain any of the search keys |
| BlockAttEditor | `TitleBlock` | Writes attribute text (key/value) back onto the title block on each layout |
| SrchBlk | `TitleBlock` | Finds block names containing a search text |
| CrvPlaneInt | `intersect` | Splits curves into those that hit a plane and those that don't, with indices |
| DirFilter | `intersect` | Splits curves by how well their direction matches a vector |
| CoplanarFilter | `intersect` | Splits curves into those lying in a plane and the rest, with indices |

## How to use a script

1. In Grasshopper, place a **C# Script** component (Maths → Script).
2. Open its editor and replace everything with the contents of the `.cs` file.
3. The component takes its inputs and outputs from the `RunScript(...)` line.
   If they don't appear, add them by hand with the exact same names.

Each branch's README explains every input, output and option of its components.
