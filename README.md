# Bullbull — `TEXT` branch

C# script components for **Rhino 8 Grasshopper**: text tools, plus two small
geometry helpers.

Other branches: [`Vector`](https://github.com/pularirajeev77-bit/Bullbull/tree/Vector) (polyline frames) ·
[`curves`](https://github.com/pularirajeev77-bit/Bullbull/tree/curves) (curve division, lines from points) ·
[`utility`](https://github.com/pularirajeev77-bit/Bullbull/tree/utility) (weight/area/volume) ·
[`main`](https://github.com/pularirajeev77-bit/Bullbull/tree/main) (overview)

## How to use a script

1. In Grasshopper, place a **C# Script** component (Maths → Script).
2. Open its editor and replace everything with the contents of the `.cs` file.
3. The component takes its inputs and outputs from the `RunScript(...)` line.
   If they don't appear, add them by hand with the exact same names.
4. The component shows its name (e.g. `GetNumbers`) and a label underneath.

**Access types** used below:
**item** = one value at a time (Grasshopper repeats the script for every item
and keeps your list/tree shape) · **list** = the whole list at once ·
**tree** = the whole data tree at once.

## Components

| Component | File | What it does |
|---|---|---|
| [Name-Format](#name-format) | `NameFormat.cs` | Sequential names like `B001, B002, B003` |
| [Find&Replace](#findreplace) | `FindReplaceText.cs` | Replaces several words in text at once |
| [Search Text](#search-text) | `SearchText.cs` | True/false: does the text contain any of the words? |
| [Multi > Single-Line-Text](#multi--single-line-text) | `MultiToSingleLineText.cs` | Joins a list of lines into one text |
| [Single > Multi-Line-Text](#single--multi-line-text) | `SingleToMultiLineText.cs` | Splits text into separate lines |
| [GetNumbers](#getnumbers) | `GetNumbers.cs` | Pulls the numbers out of text |
| [GetText](#gettext) | `GetText.cs` | Keeps only the letters of text |
| [Plane><Text](#planetext) | `PlaneTextConverter.cs` | Plane → text, and text → plane |
| [Leader Points](#leader-points) | `LeaderPoints.cs` | Leader line with a landing leg, from a plane |

---

## Name-Format

**File:** `NameFormat.cs`

Makes a list of numbered names: **prefix + zero-padded number + suffix**.

| Input | Access | Meaning |
|---|---|---|
| `Pr` | list | Prefix text. Name *i* uses prefix *i*; if the list is shorter, the last prefix is reused |
| `Sn` | item | Start number |
| `Ct` | item | How many names to make |
| `Pn` | item | Pad digits — total width of the number, filled with zeros |
| `Sf` | list | Suffix text, matched to names like `Pr` |

| Output | Meaning |
|---|---|
| `T` | The list of names |

**Example:** `Pr = B`, `Sn = 1`, `Ct = 3`, `Pn = 3`, `Sf = -A` → `B001-A, B002-A, B003-A`

**Good to know**
- Numbers can also be typed as text (`"5"`); decimals are cut, not rounded (`2.9` → `2`).
- Empty or unplugged `Pr`/`Sf` means no prefix/suffix.
- Negative `Ct` or `Pn` is treated as 0.

---

## Find&Replace

**File:** `FindReplaceText.cs`

Replaces every find word with its matching replace word, in each text.

| Input | Access | Meaning |
|---|---|---|
| `T` | item | The text to change (a list works; output keeps the same shape) |
| `F` | list | Words to find |
| `R` | list | Replacement words — `R[0]` replaces `F[0]`, `R[1]` replaces `F[1]`, … |

| Output | Meaning |
|---|---|
| `Txt` | The changed text, one per input text |

**Example:** `T = Beam B1 Level 2`, `F = [Beam, Level]`, `R = [Column, L]` → `Column B1 L 2`

**Good to know**
- If `R` is shorter than `F`, the last `R` is reused. If `R` is empty, found words are deleted.
- No `F` → the text passes through unchanged.
- Case-sensitive: `beam` does not match `Beam`.
- Replacements run in order, so a later pair also sees earlier results
  (`A→B` then `B→C` turns `A` into `C`).

---

## Search Text

**File:** `SearchText.cs`

For each text, returns **True** if it contains any of the search words.

| Input | Access | Meaning |
|---|---|---|
| `texts` | list | Texts to search in |
| `words` | list | Words to look for |

| Output | Meaning |
|---|---|
| `result` | One True/False per text, same order as `texts` |

**Example:** `texts = [HEA 200 beam, IPE 300 column]`, `words = [Beam]` → `True, False`

**Good to know**
- Matches **whole words**, ignoring upper/lower case: `beam` matches `Beam`, but not `beams`.
- Words are separated by spaces, tabs or line breaks, so punctuation stuck to a
  word blocks the match: `beam,` does not match `beam`.
- No `words` → all False (one per text). No `texts` → empty output.

---

## Multi > Single-Line-Text

**File:** `MultiToSingleLineText.cs`

Joins a list of lines into **one** multi-line text.

| Input | Access | Meaning |
|---|---|---|
| `Mtext` | list | The lines |

| Output | Meaning |
|---|---|
| `Stext` | One text with each line on its own row |

**Example:** `[Line 1, Line 2, Line 3]` → one text:
```
Line 1
Line 2
Line 3
```

**Good to know:** empty items are skipped; no input gives an empty text.

---

## Single > Multi-Line-Text

**File:** `SingleToMultiLineText.cs`

The reverse: splits multi-line text into **separate lines**.

| Input | Access | Meaning |
|---|---|---|
| `Stext` | list | One or more texts that may contain line breaks |

| Output | Meaning |
|---|---|
| `Mtext` | One item per line |

**Example:** a Panel with three rows → `[row 1, row 2, row 3]`

**Good to know**
- All input texts are split into **one combined list**.
- A line break at the very end gives an empty last line.
- Blank lines in the middle are kept.

---

## GetNumbers

**File:** `GetNumbers.cs`

Pulls **all numbers** out of a text.

| Input | Access | Meaning |
|---|---|---|
| `String` | item | The text (a list works; you get one branch of numbers per text) |

| Output | Meaning |
|---|---|
| `NumbersOnly` | The numbers found, in order |

**Examples**

| Text | Numbers |
|---|---|
| `M20x100` | `20, 100` |
| `Length 12.5 m` | `12.5` |
| `.5 thick` | `0.5` |
| `x = -5` | `-5` |
| `B-12` | `12` (a dash stuck to a letter is not a minus) |
| `100-200` | `100, 200` (a dash between numbers is a separator) |
| `1,000` | `1, 0` (commas separate numbers) |

**Good to know:** only the digits 0–9 are read; empty text gives an empty list.

---

## GetText

**File:** `GetText.cs`

Keeps **only the letters** of a text, removing numbers and symbols.

| Input | Access | Meaning |
|---|---|---|
| `String` | item | The text (a list works; output keeps the same shape) |

| Output | Meaning |
|---|---|
| `TextOnly` | The letters, with single spaces between words |

**Examples**

| Text | Result |
|---|---|
| `Beam 12 Column` | `Beam Column` |
| `HEA200` | `HEA` |
| `Beam-Column` | `BeamColumn` (symbols between words are removed without a space) |

**Good to know**
- Line breaks are kept, so multi-line text stays multi-line.
- Letters in any language are kept (e.g. Arabic, accented letters).

---

## Plane><Text

**File:** `PlaneTextConverter.cs`

Converts planes to text and back — useful for storing planes in Excel, CSV
or object attributes. The two directions work independently.

| Input | Access | Meaning |
|---|---|---|
| `Pln` | list | Planes to turn into text |
| `Txt` | list | Texts to turn back into planes |

| Output | Meaning |
|---|---|
| `Text` | One text per plane |
| `Plane` | One plane per valid text |

**Text format:** origin, X axis and Y axis, 6 decimals:
```
O{10.000000,0.000000,0.000000}&X{1.000000,0.000000,0.000000}&Y{0.000000,1.000000,0.000000}
```

**Good to know**
- Invalid texts are skipped silently, so `Plane` can be shorter than `Txt`.
- Extra spaces or line breaks around the text are ignored.

---

## Leader Points

**File:** `LeaderPoints.cs`

Builds a **leader line**: from the plane origin out to an elbow point, then a
short horizontal **landing leg** (like a dimension or tag leader).

| Input | Access | Meaning |
|---|---|---|
| `plane` | item | Where the leader starts, and its orientation |
| `leader_length` | item | Distance from origin to the elbow |
| `parameter` | item | Direction of the elbow, as a fraction of a full turn: `0` = plane X direction, `0.25` = plane Y, `0.5` = −X, `0.75` = −Y |
| `landing_leg` | item | Length of the landing leg |

| Output | Meaning |
|---|---|
| `plcrv` | The leader as one polyline |
| `pts` | Its 3 points: origin, elbow, end of landing leg |

**Good to know**
- The landing leg runs along the plane's X axis, toward the side the leader
  leans (left leader → leg goes left).
- `parameter` can be outside 0–1 (`1.25` is the same as `0.25`).
- A length of 0 gives no curve, but `pts` still has all 3 points.
