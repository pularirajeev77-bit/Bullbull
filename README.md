# Bullbull — `curves` branch

C# script components for **Rhino 8 Grasshopper**: dividing, classifying and
checking curves, and making lines from points.

Other branches: [`TEXT`](https://github.com/pularirajeev77-bit/Bullbull/tree/TEXT) (text tools) ·
[`Vector`](https://github.com/pularirajeev77-bit/Bullbull/tree/Vector) (polyline frames) ·
[`main`](https://github.com/pularirajeev77-bit/Bullbull/tree/main) (overview)

## How to use a script

1. In Grasshopper, place a **C# Script** component (Maths → Script).
2. Open its editor and replace everything with the contents of the `.cs` file.
3. The component takes its inputs and outputs from the `RunScript(...)` line.
   If they don't appear, add them by hand with the exact same names.

**Access types** used below:
**item** = one value at a time (Grasshopper repeats the script for every item
and keeps your list/tree shape) · **list** = the whole list at once ·
**tree** = the whole data tree at once.

## Components

| Component | File | What it does |
|---|---|---|
| [CenterDiv](#centerdiv) | `CenterDivide.cs` | Divides a curve symmetrically from its middle |
| [PVL](#pvl) | `PVL.cs` | Lines from a point, a direction and a length |
| [CrvClass](#crvclass) | `CurveClassifier.cs` | Sorts curves by type: line, polyline, arc, circle, ellipse … |
| [IntAng](#intang) | `InternalAngleFilter.cs` | Finds the sharp corners of a polyline |
| [IntAngDom](#intangdom) | `InternalAngleDomain.cs` | Finds polyline corners whose angle is within a range |
| [AdaptDiv](#adaptdiv) | `AdaptiveDivide.cs` | Divides a curve with more points where it bends more |

---

## CenterDiv

**File:** `CenterDivide.cs`

Divides each curve into points spaced `Distance` apart, **symmetric about the
middle of the curve**. The start and end points are always included.

| Input | Access | Meaning |
|---|---|---|
| `Curve` | list | The curves to divide |
| `Distance` | list | Spacing between points. One per curve; if the list is shorter, the last value is reused. Missing or ≤ 0 → 1 |
| `Toggle` | list | What happens at the middle (see below). One per curve, last value reused |

| Output | Meaning |
|---|---|
| `OUT` | The points, one branch per curve, in order from start to end |

**The three Toggle modes** (D = Distance)

| Toggle | Points from the middle outward |
|---|---|
| **True** | A point **at** the middle, then ±D, ±2D, ±3D … |
| **False** | A **gap** at the middle: ±D/2, ±3D/2, ±5D/2 … |
| **Unplugged** | Auto: picks True or False so that both end pieces are between ½D and 1D long (no tiny slivers at the ends) |

`Toggle` also accepts `1`/`0`, `yes`/`no`, `on`/`off`.

**Good to know**
- The middle is the true half-way point by length, on lines, polylines and NURBS alike.
- Spacing is measured **along the curve**. On a bend, the straight-line
  distance between two points is a little shorter than `Distance`.

---

## PVL

**File:** `PVL.cs`

From a **point**, a **direction vector** and a **length**, makes three lines:

| Output | Line |
|---|---|
| `Line` | Centered on the point: half the length each way (total length = `L`) |
| `PosLine` | From the point, length `L` in the vector direction |
| `NegLine` | From the point, length `L` in the opposite direction |

| Input | Access | Meaning |
|---|---|---|
| `P` | tree | Points |
| `V` | tree | Direction vectors — only the direction is used, not their length |
| `L` | tree | Lengths |

**How the inputs are matched**
- Item by item within each branch, like native Grasshopper components: a
  shorter list repeats its last item (1 vector for 10 points → all use it).
- Missing branches reuse the last branch.
- Outputs keep the branch structure of the input — grafted in, grafted out.

**Good to know**
- A zero vector or a length of 0 gives an empty item (null) in that position,
  so the outputs stay lined up with the inputs.
- A negative length flips the lines to the other side.
- If any input is unplugged, the component shows an orange warning.

---

## CrvClass

**File:** `CurveClassifier.cs`

Looks at each curve's **shape** and sorts it into a group: line, polyline,
arc, circle, ellipse, elliptical arc, polycurve or other.

| Input | Access | Meaning |
|---|---|---|
| `Crv` | tree | The curves (a single curve or a list works too) |

| Output | Meaning |
|---|---|
| `Type` | The type name of every curve, in input order |
| `Lines` | Straight curves |
| `Polylines` | Polylines with 3 or more points |
| `Arcs` | Open arcs |
| `Circles` | Full circles |
| `Ellipses` | Closed ellipses |
| `EllipticalArcs` | Open parts of an ellipse |
| `PolyCurves` | Joined curves that are none of the above |
| `Others` | Everything else (e.g. free-form NURBS) |

**How a curve is classified** (first match wins, in this order)

1. **Line** — straight, whatever it was made as (line, 2-point polyline, straight NURBS)
2. **Polyline** — made only of straight segments
3. **Circle** / **Arc** — a circle or part of one
4. **Ellipse** / **EllipticalArc** — an ellipse or part of one
5. **PolyCurve** — joined curve that is none of the above
6. **Other**

**Good to know**
- Outputs keep the input's branch structure. An output with no curves of its
  type stays empty.
- Shapes are checked within the model tolerance, so curves from imports that
  are *almost* an arc or line are still recognised.
- A straight polyline (all points in a line) counts as a **Line**.
- An empty item in the input gives an empty item in `Type`, so `Type` stays
  lined up with the input.

---

## IntAng

**File:** `InternalAngleFilter.cs`

Finds the **sharp corners** of a polyline: every vertex whose inside angle is
smaller than `Ang`. Useful for spotting corners that are too tight to
fabricate, weld or bend.

| Input | Access | Meaning |
|---|---|---|
| `Crv` | item | A polyline (a list works; you get one branch per curve) |
| `Ang` | item | Angle limit, **in radians** (90° = π/2 ≈ 1.5708) |

| Output | Meaning |
|---|---|
| `P` | The corner points with an angle smaller than `Ang` |
| `N` | At each of those corners, the direction splitting the angle in half, pointing inside the shape |
| `t` | Where each corner is on the curve (curve parameter) |

**Example:** a rectangle with `Ang = 1.6` (≈ 92°) → all 4 corners (90° each).

**Good to know**
- **Closed polylines:** the real inside angle is used, 0–360°. The concave
  corner of an L-shape is 270°, so it is not flagged as sharp.
- **Open polylines:** there is no inside, so the angle between the two edges
  (0–180°) is used; the two end points are never flagged.
- Radians, not degrees: to use degrees, put a *Radians* component in front.
- Curves that are not polylines (arcs, NURBS) give empty output.
- Doubled points are merged first, so a corner drawn with a repeated point is still found.

---

## IntAngDom

**File:** `InternalAngleDomain.cs`

Same as [IntAng](#intang), but finds corners whose inside angle is **within a
range** (min to max) instead of below one limit. For example, find every
corner between 80° and 100° to check for right angles.

| Input | Access | Meaning |
|---|---|---|
| `Crv` | item | A polyline (a list works; you get one branch per curve) |
| `Dom` | item | Angle range as a domain, **in radians**, min and max included |

| Output | Meaning |
|---|---|
| `P` | The corner points whose angle is within `Dom` |
| `N` | At each of those corners, the direction splitting the angle in half, pointing inside the shape |
| `t` | Where each corner is on the curve (curve parameter) |

**Example:** an L-shape with `Dom = 4.6 To 4.8` (≈ 264°–275°) → only the concave inside corner (270°).

**Good to know**
- Everything in IntAng's *Good to know* applies here too (closed = true inside
  angle 0–360°, open = angle between edges 0–180°, radians).
- The domain works either way round: `2.0 To 1.0` is the same as `1.0 To 2.0`.

---

## AdaptDiv

**File:** `AdaptiveDivide.cs`

Divides a curve **adaptively**: more points where it bends a lot, fewer
where it is nearly straight. The result is a polyline that stays within `Tol`
of the curve using as few points as possible.

| Input | Access | Meaning |
|---|---|---|
| `Crv` | item | The curve to divide (NURBS, polycurve, line, arc, circle …) |
| `ForcePts` | list | Optional points — the division always includes the closest point on the curve to each |
| `CullIdx` | list | Optional indices of points to remove from the result; negative counts from the end (`-1` = last) |
| `Tol` | item | Max gap allowed between the curve and the polyline. Unplugged or 0 → 0.01 |
| `MaxSeg` | item | Max number of polyline segments. Unplugged or 0 → 100; at least 4 |

| Output | Meaning |
|---|---|
| `P` | The division points, in order along the curve |
| `PL` | The polyline through those points |

**How it works**
1. Start with 4 equal segments, plus the `ForcePts` locations.
2. Find the segment that is furthest from the curve and split it at that spot.
3. Repeat until every segment is within `Tol`, or `MaxSeg` is reached.

**Good to know**
- For a closed curve the polyline is closed too.
- `CullIdx` is applied last, to the finished list of points.
- The gap is checked at 3 spots per segment, so a very small wiggle between
  two of them can occasionally be missed; lowering `Tol` catches it.
