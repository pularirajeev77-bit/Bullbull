# Bullbull - `TitleBlock` branch

C# script components for **Rhino 8 Grasshopper**: layout / title-block tools.

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
[`main`](https://github.com/pularirajeev77-bit/Bullbull/tree/main) (overview)

## How to use a script

1. In Grasshopper, place a **C# Script** component (Maths > Script).
2. Open its editor and replace everything with the contents of the `.cs` file.
3. The component takes its inputs and outputs from the `RunScript(...)` line.
   If they don't appear, add them by hand with the exact same names.

## Components

| Component | File | What it does |
|---|---|---|
| [BlockAttExtract](#blockattextract) | `BlockAttExtract.cs` | Reads a title block's attribute text (key/value) from each layout |
| [KeySearch](#keysearch) | `TitleBlockKeySearch.cs` | Flags which title-block keys contain any of the search keys |
| [BlockAttEditor](#blockatteditor) | `BlockAttEditor.cs` | Writes attribute text (key/value) back onto the title block on each layout |
| [SrchBlk](#srchblk) | `SearchBlock.cs` | Finds block names containing a search text |

---

## BlockAttExtract

**File:** `BlockAttExtract.cs`

For each **layout** you name, finds the **title block** (a block instance with
the given name placed on that layout) and returns its **attribute user text** as
key / value pairs - drawing number, title, revision, date and so on.

| Input | Access | Meaning |
|---|---|---|
| `Run` | item | True = read the layouts |
| `BlockName` | item | Title-block definition name (not case-sensitive) |
| `LayoutNames` | list | Layout (page) names to read |

| Output | Meaning |
|---|---|
| `Keys` | Attribute keys, sorted A-Z |
| `Values` | Matching attribute values |
| `FoundLayouts` | Layouts that exist, in input order |

Branch `{k}` of `Keys` and `Values` belongs to `FoundLayouts[k]`.

**Good to know**
- **Alignment fixed:** branches used to be numbered by the *input* position, so
  after a missing layout was skipped, `{2}` no longer matched item 2 of the layout
  list. Now branch `{k}` always belongs to `FoundLayouts[k]`.
- Only real layouts (page views) are searched; a model view with the same name
  can no longer be picked up by mistake. Layout and block names are not
  case-sensitive.
- Locked title blocks are found (they often are locked); only block objects are
  scanned, which is faster on busy layouts.
- Warnings list the layouts that don't exist and the layouts without the block
  (they get an empty branch). If a layout has the block more than once, the first
  is read and a remark says so.
- Renamed output `LNames` -> `FoundLayouts`. Added metadata: a header block
  listing every input/output, component Name / NickName / Description, tooltips on
  every pin, and a Message that shows how many layouts were found (e.g. `3/4 layouts`).
- Not compiled here (no Rhino) - test in Grasshopper.

---

## KeySearch

**File:** `TitleBlockKeySearch.cs`

Finds which **title-block keys** (from [BlockAttExtract](#blockattextract))
contain any of your **search keys** - e.g. find the `DRAWING NO` or `REVISION`
key on every layout, then pick the matching value.

| Input | Access | Meaning |
|---|---|---|
| `Keys` | list | Attribute keys - connect BlockAttExtract `Keys` |
| `SearchKeys` | list | Words or phrases to look for |

| Output | Meaning |
|---|---|
| `Matches` | True / False per key |
| `MatchIndex` | Indices of the matching keys |
| `Summary` | `n match(es) out of m keys` |

**Typical wiring**

```
BlockAttExtract.Keys   -> KeySearch.Keys
"drawing no"           -> KeySearch.SearchKeys
KeySearch.MatchIndex   -> List Item (index)
BlockAttExtract.Values -> List Item (list)    => the drawing number of every layout
```

Because `Keys` is list access and BlockAttExtract gives one branch per layout,
KeySearch runs once per layout and its outputs keep the same `{k}` branches.

**Good to know**
- Whole-word, case-insensitive: `no` matches `Drawing No` but not `Note`.
- **Multi-word search keys now work** (`drawing no`). The old version split the
  text into single words, so any search containing a space could never match.
- Tabs, newlines and double spaces count as one space.
- Empty inputs give a warning on the component; the message shows `matches/keys`.
- Renamed from SearchText: `Texts, Words` / `Result, Index, Msg` ->
  `Keys, SearchKeys` / `Matches, MatchIndex, Summary`.
- Not compiled here (no Rhino) - test in Grasshopper.

---

## BlockAttEditor

**File:** `BlockAttEditor.cs`

The **write-back partner of BlockAttExtract**: sets attribute user text
(key / value) on the title block of each named layout - update revision, date,
drawing number and so on for a whole drawing set from Grasshopper.

| Input | Access | Meaning |
|---|---|---|
| `Run` | item | True = write |
| `BlockName` | item | Title-block definition name (not case-sensitive) |
| `Keys` | list | Attribute keys to set |
| `Values` | **tree** | Branch `{k}` = values for `LayoutNames[k]`, in `Keys` order |
| `LayoutNames` | list | Layouts to edit (e.g. BlockAttExtract `FoundLayouts`) |

| Output | Meaning |
|---|---|
| `Success` | True if the block was found on at least one layout |
| `Summary` | What happened |
| `ChangedCount` | Number of attribute values changed |

**Typical wiring**

```
BlockAttExtract.FoundLayouts -> BlockAttEditor.LayoutNames
Keys (e.g. REVISION, DATE)   -> BlockAttEditor.Keys
Values tree {0},{1},...      -> BlockAttEditor.Values   (one branch per layout)
```

A **single** `Values` branch applies the same values to every layout (e.g. the
same issue date on all sheets).

**Good to know**
- **Multi-layout fix:** `Values` is now tree access. Before it was list access,
  so a flat list of values was read as *one* layout's values and the count check
  failed for more than one layout (it only worked when Grasshopper happened to
  iterate one layout at a time).
- A **null** value leaves that key unchanged. Before, nulls were dropped, which
  shifted every following value onto the wrong key. An empty text *does* set the
  key to empty.
- Only values that differ are written, so re-running changes nothing; each run is
  **one Undo step**.
- Every instance of the block on a layout is updated (locked/hidden too); only
  block objects are scanned.
- Warnings list missing layouts, layouts without the block, and key/value count
  mismatches; the message shows `changed | layouts`.
- Renamed outputs `OK, Msg, Changed` -> `Success, Summary, ChangedCount`;
  `LayoutNames` is now a plain text list. Metadata and tooltips added.
- Not compiled here (no Rhino) - test in Grasshopper.

---

## SrchBlk

**File:** `SearchBlock.cs`

Finds the **block names** that contain a search text (case-insensitive) - e.g.
type `title` to find the exact title-block name to feed into
[BlockAttExtract](#blockattextract) / [BlockAttEditor](#blockatteditor).
Works on any list of text.

| Input | Access | Meaning |
|---|---|---|
| `BlockNames` | list | Block names (or any text) to search |
| `Search` | item | Text to look for, e.g. `title` |

| Output | Meaning |
|---|---|
| `Matches` | Names that contain the search text (original spelling) |
| `MatchIndex` | Their indices in `BlockNames` |

**Good to know**
- **Trees fixed:** the old `object` inputs were item access, so the component ran
  once per name and the home-made tree converter never saw a list; a tree input
  was also flattened into one list. Now `BlockNames` is list access and
  Grasshopper runs once per branch, so output branches match the input.
- Matching is a substring test (`title` finds `A1_TitleBlock`); original case is
  kept in the output.
- New `MatchIndex` output (use with List Item on any parallel list).
- Warnings on empty inputs; a remark when nothing matches; the message shows
  `matches/names`. Errors no longer go only to `Print`.
- Renamed: `block_list, search_block` / `block_results` ->
  `BlockNames, Search` / `Matches` (+ `MatchIndex`). Name typo (trailing space)
  fixed; metadata and tooltips set once.
- Not compiled here (no Rhino) - test in Grasshopper.
