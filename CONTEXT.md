# ClosedXML.Parser

A lexer and parser for Excel formulas. It turns formula text into an abstract syntax
tree so the formula can be evaluated.

## Language

### Notation

**Reference style**:
The notation a formula's references are written in, either A1 or R1C1. A formula is
parsed in one style and every reference in it is read in that style.
_Avoid_: mode, notation, dialect

**A1**:
The reference style that names a cell by column letter and row number (`C7`), with `$`
marking an axis absolute (`$C$7`).
_Avoid_: alphanumeric style

**R1C1**:
The reference style that names a cell by row and column number, absolute when written
bare (`R7C3`) and relative to the formula's own cell when bracketed (`R[7]C[3]`).
_Avoid_: numeric style, offset style

### Formula text

**Stored formula**:
The formula as it is written in the file. This is what the parser reads.
_Avoid_: raw formula, file formula

**Displayed formula**:
The formula as Excel shows it in the formula bar. It differs from the stored formula —
`IFS` is stored as `_xlfn.IFS`, `@` is stored as `[#This Row]` — and is out of scope.
_Avoid_: user formula, UI formula

**Future function**:
A function newer than the file format, stored with an `_xlfn.` prefix.

### References

**Sheet prefix**:
The part of a reference naming the sheet it points into, up to and including the `!`.
Quoted (`'New York'!`) when the sheet name requires it, bare (`Sheet1!`) when it does
not.
_Avoid_: sheet qualifier

**3D reference**:
A reference spanning a range of sheets, written with a first and last sheet name
(`first:last!A1`).
_Avoid_: sheet range reference

**Bang reference**:
A reference with an empty sheet prefix (`!A1`), pointing into the workbook scope rather
than a named sheet.

**Structured reference**:
A reference addressing a table by name and its parts by column or region rather than by
cell (`Table1[Column]`).
_Avoid_: table reference, intra-table reference

**Cell function**:
A call that uses a cell reference as the function being called (`R7C3(TRUE)`), rather
than a function name. A macro-sheet construct, and written in the formula's reference
style like any other reference.
