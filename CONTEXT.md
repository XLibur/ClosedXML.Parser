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

**Sheet error**:
A reference whose area is gone but whose sheet is not, written `Sheet1!#REF!`. The sheet is still
named, so a formula modification renames it like the sheet of any other reference. A reference whose
sheet itself was deleted is a plain `#REF!` instead.
_Avoid_: ref error (for this one), deleted reference

**Cell function**:
A call that uses a cell reference as the function being called (`R7C3(TRUE)`), rather
than a function name. A macro-sheet construct, and written in the formula's reference
style like any other reference.

**Dynamic data exchange reference**:
A reference to an item that another application serves over dynamic data exchange (DDE).
Stored after the book prefix of its DDE link (`[1]!'id1?req?AAPL'`) and displayed after the
link's application and topic (`Sdemo123|tik!'id1?req?AAPL'`). The parser recognises one only
when the item is quoted: a bare item (`MT4|BID!EURUSD`) reads as a name in a sheet called
`MT4|BID`.
_Avoid_: DDE formula, DDE name

**DDE link**:
The application and the topic a dynamic data exchange reference reads from, written
`application|topic`. Stored as a `ddeLink` in an external link part.
_Avoid_: DDE server (for the pair), DDE topic (for the pair)

### Changing formulas

**Formula modification**:
Rewriting a stored formula so its sheets, tables, functions and references follow a change
to the workbook, such as a renamed or deleted sheet, while the rest of its text stays as
written. A sheet behind a book prefix belongs to another workbook and is never renamed.
_Avoid_: transformation, rewrite
