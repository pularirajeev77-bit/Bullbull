# Bullbull
Rhino grasshopper codes

C# script components for Rhino 8 Grasshopper. Paste a file's contents into a
C# Script component; the inputs and outputs follow the `RunScript` signature.

## Text tools

| File | Component | Inputs | Output | What it does |
|---|---|---|---|---|
| [NameFormat.cs](NameFormat.cs) | Name-Format | `Pr` prefix (list), `Sn` start, `Ct` count, `Pn` pad digits, `Sf` suffix (list) | `T` | Generates sequential names, e.g. `B001`, `B002` |
| [FindReplaceText.cs](FindReplaceText.cs) | Find&Replace | `T` text (item), `F` find (list), `R` replace (list) | `Txt` | Replaces each find term with its matching replace term |
| [SearchText.cs](SearchText.cs) | Search Text | `texts` (list), `words` (list) | `result` | True for each text containing any of the words (whole word, case-insensitive) |
| [MultiToSingleLineText.cs](MultiToSingleLineText.cs) | Multi > Single-Line-Text | `Mtext` (list) | `Stext` | Joins a list of lines into one multi-line string |
| [SingleToMultiLineText.cs](SingleToMultiLineText.cs) | Single > Multi-Line-Text | `Stext` (list) | `Mtext` | Splits multi-line text into separate lines |
| [GetNumbers.cs](GetNumbers.cs) | GetNumbers | `String` (item) | `NumbersOnly` | Extracts the numbers from text, e.g. `M20x100` → `20, 100` |
| [GetText.cs](GetText.cs) | GetText | `String` (item) | `TextOnly` | Keeps only the letters, e.g. `Beam 12 Column` → `Beam Column` |

## Geometry tools

| File | Component | Inputs | Output | What it does |
|---|---|---|---|---|
| [PlaneTextConverter.cs](PlaneTextConverter.cs) | Plane><Text | `Pln` (list), `Txt` (list) | `Text`, `Plane` | Converts planes to text and back (`O{..}&X{..}&Y{..}`) |
| [LeaderPoints.cs](LeaderPoints.cs) | Leader Points | `plane`, `leader_length`, `parameter` (0–1 around the circle), `landing_leg` | `plcrv`, `pts` | Builds a leader line with a horizontal landing leg from a plane |
