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
| [PolyPlus](#polyplus) | `PolylinePlus.cs` | Polyline through points, with chosen stretches as arcs |
| [VarChamfer](#varchamfer) | `VariableChamfer.cs` | Chamfers chosen polyline corners, each with its own distance |

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
| `Pts` | The division points, in order along the curve |
| `PLine` | The polyline through those points |

**How it works**
1. Start with 4 equal segments, plus the `ForcePts` locations.
2. Find the segment that is furthest from the curve and split it at that spot.
3. Repeat until every segment is within `Tol`, or `MaxSeg` is reached.

**Good to know**
- For a closed curve the polyline is closed too.
- `CullIdx` is applied last, to the finished list of points.
- The gap is checked at 3 spots per segment, so a very small wiggle between
  two of them can occasionally be missed; lowering `Tol` catches it.

---

## PolyPlus

**File:** `PolylinePlus.cs`

Draws a **polyline through points**, but turns chosen stretches into **arcs**,
and joins everything into one curve. Optionally closes it.

| Input | Access | Meaning |
|---|---|---|
| `Pts` | tree | The points, in order. **One curve per branch** |
| `ArcStartIdx` | list | Point number where each arc starts (0 = first point) |
| `ArcEndIdx` | list | Point number where each arc ends — paired with `ArcStartIdx` (1st start ↔ 1st end …) |
| `Close` | item | True → adds a straight segment back to the first point |

| Output | Meaning |
|---|---|
| `Crv` | One joined curve per branch, same branch paths as `Pts` |

**Example:** 7 points, `ArcStartIdx = 2`, `ArcEndIdx = 4` → straight 0→1→2,
an arc from point 2 through point 3 to point 4, then straight 4→5→6.

**How each arc is made**
- It runs from the start point to the end point and passes through the point
  halfway between them in the list. Points in between are not all hit exactly,
  unless they lie on one circle.
- If the three points are in a straight line, a straight segment is used instead.
- A range is ignored (drawn straight) if the end is not at least 2 points after
  the start, or is past the last point.

**Good to know**
- No arc inputs → a plain polyline.
- The same arc ranges are used for every branch.
- If `ArcStartIdx` and `ArcEndIdx` have different counts, no arcs are made
  and the component shows a warning.
- Repeated points in a row are skipped.
- A branch with fewer than 2 points gives an empty item.

---

## VarChamfer

**File:** `VariableChamfer.cs`

**Chamfers** (cuts off) the corners of a polyline — all of them, or only the
ones you pick — each with its own distance.

| Input | Access | Meaning |
|---|---|---|
| `Crv` | item | The polyline. Other curves are first converted to a polyline |
| `Dist` | list | Chamfer distance, measured from the corner along each edge |
| `Idx` | list | Which corners (vertex numbers, 0 = first; `-1` = last). Empty → every corner |

| Output | Meaning |
|---|---|
| `PLine` | The chamfered polyline |

**Which distance goes where:** the 1st chamfered corner uses `Dist[0]`, the
2nd uses `Dist[1]`, and so on. If `Dist` is shorter, it starts again from
`Dist[0]`. One value → the same chamfer everywhere.

**Example:** a rectangle, `Idx = [0, 2]`, `Dist = [50, 100]` → corner 0 cut
by 50, corner 2 cut by 100, corners 1 and 3 untouched.

**Good to know**
- A chamfer never takes more than half of an edge, so neighbouring chamfers
  can't overlap (smart clamping). When two meet in the middle of an edge, the
  shared point is kept once.
- Open polylines: the first and last points are never chamfered.
- A distance of 0 (or less) leaves that corner as it is.
- A curve with fewer than 3 points is passed through unchanged.
