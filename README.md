# Bullbull - `shape` branch

C# script components for **Rhino 8 Grasshopper**: shape / joinery tools.

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
[`main`](https://github.com/pularirajeev77-bit/Bullbull/tree/main) (overview)

## How to use a script

1. In Grasshopper, place a **C# Script** component (Maths > Script).
2. Open its editor and replace everything with the contents of the `.cs` file.
3. The component takes its inputs and outputs from the `RunScript(...)` line.
   If they don't appear, add them by hand with the exact same names.

## Components

| Component | File | What it does |
|---|---|---|
| [Hybrid Interlock](#hybrid-interlock) | `HybridInterlockNotcher.cs` | 3D interlocking (egg-crate / lap) notches between crossing members, any orientation, solids or surfaces |

---

## Hybrid Interlock

**File:** `HybridInterlockNotcher.cs` &middot; Component name: *Hybrid Interlock Notcher* &middot; v2.1

Cuts **interlocking notches** wherever Base and Cutter members cross - egg-crate ribs,
waffle structures, half-lap beams - in **any orientation**, on **closed solids or open
surfaces**. An optional third set (AddBreps) running through the same joint gets an
H-notch so three members lock together.

| Input | Access | Meaning |
|---|---|---|
| `BaseBreps` | list | Primary members. Keep the **positive** side of each joint axis (slot opens from below) |
| `CutterBreps` | list | Crossing members. Keep the **negative** side (slot opens from above) |
| `AddBreps` | list | Optional third members - get a two-sided **H-notch**, web centred on the joint |
| `Gap` | item | Clearance **along** the joint axis between mating parts. 0 = flush |
| `AddGap` | item | Web height left on AddBreps, centred on the joint. 0 = cut clean through |
| `Tolerance` | item | Extra slot **width** (in-plane clearance). 0 = 10 x model tolerance |
| `Bake` | item | Connect a **Button** - replaces referenced inputs in Rhino, or adds new objects for internal geometry |

| Output | Meaning |
|---|---|
| `SlottedBases` | Notched BaseBreps, 1:1 with the input |
| `SlottedCutters` | Notched CutterBreps, 1:1 with the input |
| `SlottedAdd` | Notched AddBreps, 1:1 with the input |

Set all three Brep inputs to **List Access** (they are `List<Brep>` in `RunScript`). The
component warns if it is looping item by item. The message shows the number of joints cut.

### How a joint is cut

Along the joint axis (0 = middle of the intersection line):

| Joint | Base keeps | AddBrep keeps | Cutter keeps |
|---|---|---|---|
| Base + Cutter | z > +Gap/2 | - | z < -Gap/2 |
| Base + Add + Cutter | z > +(AddGap/2 + Gap) | -AddGap/2 < z < +AddGap/2 | z < -(AddGap/2 + Gap) |
| Add + one member | same H-notch, measured on that pair alone | | |

**Joint axis** (per joint, first one that isn't degenerate):

1. plate x plate: `normalA x normalB` - the line the two planes share
2. plate x beam: `normalPlate x axisBeam`
3. beam x beam: `axisA x axisB`
4. the principal axis of the intersection itself
5. world Z

Members are measured by principal-axis analysis of their vertices and edge points
(plate = thinnest extent <= 0.35 x middle extent). The axis gets a fixed sign (biased to
+Z, then +Y, then +X) so Base and Cutter always end up on opposite sides. Every joint is
measured on the **uncut** input, so earlier cuts never shift later joints.

### Good to know

- **Attributes:** names, colours, layer and user text of referenced inputs are kept on the
  outputs; Rhino 8 Model Objects are passed back as Model Objects.
- **Bake** replaces referenced objects **in place** (same id, attributes kept). After the
  replace Grasshopper re-solves on the already-notched geometry; with `Gap > 0` nothing is
  cut again, with `Gap = 0` the preview may show a second thin cut - the baked result is right.
- If a cut cuts a member into pieces (e.g. `AddGap = 0`), only the **largest piece** is kept.
- Two or more AddBreps on the same intersection line share the web and will clash - warned.
- Boolean fallbacks: a slightly enlarged slot box for solids, split-and-sort for open surfaces.

### Changelog

**v2.1** (2026-10-03)
- **Wrong input attributes with null items:** the input lookup skipped nulls (`AllData(true)`),
  so after a null every later member got the name/colour/bake id of the *next* input. Nulls
  are now kept so indices line up; the input pins are found by name.
- **Name/Colour/Layer wiped:** when a referenced object had user text, that user text was
  copied *after* Name/Colour/LayerIndex and replaced them. Order fixed.
- **Seat shift on retry:** the boolean retry scaled the slot box by 1.001. Slot boxes run
  2 x member reach + 10 long, so this moved the slot floor (the `Gap` face) by up to a
  millimetre or more. The retry now grows the box by ~2 x model tolerance only.
- **Silent failed cuts:** when every boolean attempt failed the member was left uncut with no
  message; failed cuts are now counted and warned.
- **Bake user text:** the internal Name/Color/LayerIndex helper keys are no longer written into
  the Rhino object's user text on bake.
- Tolerance 0 warning corrected (slots still get 10 x model tolerance, not flush).
- Metadata set once; description and tooltips on every input/output; joint count in the message.
- Not compiled here (no Rhino) - test in Grasshopper.
