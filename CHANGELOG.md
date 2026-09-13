# Changelog

All notable changes to XLibur.ClosedXML.Parser are recorded here. The Publish Release
workflow rolls the Unreleased section into a dated version heading, so add an entry
under Unreleased with each change.

## Contents

- [Unreleased](#unreleased)

## Unreleased

### Added

- `FormulaConverter.ModifyR1C1`, the R1C1 counterpart of `ModifyA1`. `ModContext.IsA1` tells a
  modifier the reference style of the formula, and a reference it gets is in that style.
- `ReferenceParser.TryParseR1C1`. Every public method of `ReferenceParser` lexed with the
  A1 table, so a caller holding an R1C1 reference had no entry point at all, and the
  library's own tests had to reach through `InternalsVisibleTo` to parse one. The
  reference is read as written: a relative axis keeps its offset and is not resolved
  against an anchor cell, so `R[-1]C` gives a relative row of -1 and a relative column of
  0. The other five public methods keep their A1-only form.
- Parse dynamic data exchange (DDE) references. Excel stores one as the book prefix of
  its link followed by the quoted item, `[1]!'id1?req?AAPL'`, and displays it with the
  application and the topic of the link instead, `Sdemo123|tik!'id1?req?AAPL'`. Both
  forms now parse. A quoted item was a lexer error, so every such formula failed,
  including 3,276 formulas in the Enron and EUSES data sets that the tests had filed as
  invalid external references. [MS-XLSX] has no production for DDE, so the item is a new
  token, `DDE_ITEM`. It is the last token of `FormulaLexer.g4`, so no other token ID
  moves, and the ANTLR lexer and both Rolex DFA tables are regenerated. Before the token
  was added, the tables were regenerated from the unchanged grammar and matched the
  committed ones byte for byte, so they differ only by the new token. A sheet name can
  contain `|`, so a prefix is read as a DDE link only when a quoted item follows it:
  `a|b!A1` is still a reference into the sheet `a|b`. This is what the skipped ClosedXML
  test `Reference_can_be_dynamic_data_exchange` needs. Two displayed forms are still not
  recognised. A bare item (`MT4|BID!EURUSD`) cannot be told from a name in a sheet called
  `MT4|BID` and still parses as one. A quoted topic (`App|'topic'!'item'`) still does not
  parse.

### Changed

- A formula modification now writes a part of the formula again only when it changes that part, and
  copies the rest of the text character for character. It used to write every reference, sheet
  prefix and area again from the values the parser read, so a modifier that changes nothing still
  changed the text: quotes a sheet name doesn't need were dropped (`'Wk2'!C5` gave `Wk2!C5`), quotes
  it would be written with were added (`592101500!D11` gave `'592101500'!D11`), an area of one cell
  was collapsed (`'Org Chart'!D5:D5` gave `'Org Chart'!D5`), a structured reference lost the braces
  of a single keyword (`[[#All]]` gave `[#All]`), and the whitespace before a formula was dropped.
  Renaming one sheet therefore re-quoted unrelated references across the whole formula. Of the
  2,231 formulas in the Enron and EUSES data sets that a do-nothing modifier used to change, 18 are
  left, and all 18 are the deliberate exception below. A part is compared as a whole, so a reference
  whose sheet is renamed is still written again in full and its area comes out as the parser read
  it. The exception: a ref error that swallowed a reference, `#REF!A1` or `#REF!#REF!`, is still
  always written as `#REF!`, because Excel can't parse the longer form and saves such a reference as
  a plain `#REF!`.
  - A conversion between reference styles writes every reference again by definition, so its output
    is unchanged apart from the whitespace before a formula, which it now keeps: 184 of the 238,572
    data set formulas.
  - `CopyVisitor` is gone. It was internal, and writing the text is now the one job of the rewriter
    behind `FormulaConverter.ModifyA1` and `ModifyR1C1`.
- Give a sheet-qualified `#REF!` its own method on `IAstFactory`, `SheetErrorNode`. A ref error such
  as `Sheet1!#REF!` or `'[1]Jane''s'!#REF!` used to arrive at `ErrorNode`, whose `range` covered the
  sheet prefix while its `error` did not, so a factory that wanted the sheet had to compare the two
  lengths, slice the formula text and lex the prefix again. `FormulaModifier` did exactly that, and
  no other factory could: the Ast, the visualizer and every implementer outside this repository saw
  `Sheet1!#REF!` as a bare `#REF!` and lost the sheet. The parser has read the sheet and the book
  index already, so it now hands them over, `AstFactory` keeps them in a `SheetErrorNode` record, and
  a modification renames that sheet like the sheet of any other reference. `ErrorNode` keeps the
  errors with no sheet to rename: `#REF!`, `#REF!A1`, `#REF!#REF!` and `!#REF!`. The grammar allows
  one sheet here, optionally behind a book prefix, and never a sheet range, so the method takes one
  sheet name and a nullable workbook index. The written output doesn't change.
  - BREAKING CHANGE: every implementer of `IAstFactory` has to add `SheetErrorNode`. netstandard2.0
    has no default interface methods, so it can't be given a default.
- Replace `RefModVisitor` with `FormulaModifier`, which has only the methods a modification
  overrides: `ModifySheet`, `ModifyTable`, `ModifyFunction`, `ModifyRef` and
  `ModifyCellFunction`. A method that returns `null` still replaces the part with `#REF!`.
  `RefModVisitor` implemented the 34 methods of `IAstFactory`, 22 of them only to pass the
  call on, and the formula text was rebuilt from offsets by it, by `CopyVisitor` and by
  `ModContext`, all of them public. Now that is inside the library. The breaking changes:
  - `RefModVisitor` is renamed `FormulaModifier` and doesn't implement `IAstFactory`.
    `FormulaConverter.ModifyA1` takes a `FormulaModifier`. To migrate, derive from
    `FormulaModifier`; an override of `ModifySheet`, `ModifyTable` or `ModifyFunction`
    doesn't change.
  - `ModifyRef` and `ModifyCellFunction` are protected, so they can be overridden outside the
    library, e.g. to shift references when rows are inserted. They were internal, although
    the class invited overriding them.
  - `CopyVisitor` and `TransformedSymbol` are internal. `RefModVisitor` used a `CopyVisitor` of
    its own, so an override of `CopyVisitor` never changed a modification.
  - A `ModContext` can't be created outside the library, and it doesn't expose the text of the
    formula any more, so a modifier can't cut the formula by offsets. `Sheet`, `Row`, `Col`
    and `IsA1` stay.
  - The obsolete `FormulaConverter.ModifyA1` overload without a sheet and the obsolete
    `ModContext` constructor are removed.
- Forked from ClosedXML.Parser 2.0.0 and published as `XLibur.ClosedXML.Parser`. The
  `ClosedXML.Parser` namespace is unchanged.
- The Rolex grammars `LexerA1.rl` and `LexerR1C1.rl` are generated from `FormulaLexer.g4`
  by a new converter, `tools/Antlr2Rolex`. It replaces the Antlr2Rolex tool the README
  named, which was never published, so the grammars had been edited by hand. A test
  regenerates both and fails when a committed one differs, so the ANTLR grammar and the
  Rolex lexer can no longer drift apart unnoticed. The first run found such a drift: the
  fix that made `C0` a name had edited `LexerR1C1.rl` but not the R1C1 section of
  `FormulaLexer.g4`, which now has it too. The generated grammars differ from the
  committed ones only in brackets (the hand-written `DDE_ITEM` and the absolute column),
  and both DFA tables regenerated from them match the committed tables byte for byte, so
  the lexer is unchanged. The Rolex build that generates the tables is now vendored in
  `tools/rolex/91a2d6d`, and a Windows CI job regenerates both tables with it and fails
  when a committed table differs, so a grammar can't be committed without its table.
- `IAstFactory` has two new methods: `ExternalDynamicDataExchange` for the stored form of
  a DDE reference and `DynamicDataExchange` for the displayed form. This breaks every
  implementation, because the library targets netstandard2.0, which has no default
  interface methods. `CopyVisitor` and `RefModVisitor` write a DDE reference back in the
  form it was written in, normalised the way a sheet reference is (e.g. a space after the
  `!` is dropped). The `application|topic` prefix of the displayed form is not a sheet, so a sheet
  rename leaves it alone. It stays bare, the way Excel writes it, although a sheet of the
  same name would be quoted because of the `|`; it is quoted only when it would not read
  back as the same link, e.g. with a space.

### Fixed

- Quote a sheet name that needs quotes in the display string of an Ast node. `SheetNameNode` and
  `SheetErrorNode` were the only nodes that did, so a sheet called `My Sheet` came out bare from the
  other seven: `My Sheet!A1` from `SheetReferenceNode`, `[1]My Sheet!A1` from
  `ExternalSheetReferenceNode`, and the same from `ExternalSheetNameNode`, `ExternalFunctionNode`,
  `FunctionNode`, `Reference3DNode` and `ExternalReference3DNode`. None of those strings parse back
  as the node they came from, and the visualizer puts them in its diagram. The rule is the one the
  library already applies to a written formula: quote when `NameUtils.ShouldQuote` says so, double
  an apostrophe inside the name, and let the quote wrap the whole prefix, book index included,
  `'[2]Jane''s'!A1`. Either sheet of a 3D reference needing quotes quotes the pair,
  `'My Jan:Dec'!A1`. A name that needs no quotes still stays bare. All nine nodes that carry a sheet
  write their prefix in one place now, `SheetPrefixWriter`, instead of each building its own. A
  first sheet of a 3D reference that is also a cell, e.g. `PWD1:Dec!A1`, still comes out unquoted;
  that is [#31](https://github.com/XLibur/ClosedXML.Parser/issues/31), and a written formula has it
  too.
  [#34](https://github.com/XLibur/ClosedXML.Parser/issues/34)
- Keep the range of an expression in braces that turns out to be a reference, e.g. `(A1):B2`. The
  parser reads `(A1)` as a value expression, and when the `:` shows it is a reference expression, it
  backtracks and passes the node it has already read to the reference expression. That expression
  took its start from the token the parser had reached, which is past the braces, so every range
  around them began too late. A modification then spliced the text at the wrong place:
  `SUM((Total_Cost Jan):(Total_Cost Apr.))` of the EUSES data set came back as
  `SUM(Total_Cost Jan)(Total_Cost Jan):(Total_Cost Apr.))`, and its R1C1 form no longer parsed. The
  node the parser has already read now carries the index it starts at.
- Leave the sheets of a 3D reference into another workbook alone when a sheet is renamed or
  deleted. `RefModVisitor.ExternalReference3D` passed both sheets of `[1]First:Last!A1` to
  `ModifySheet`, so renaming a sheet of this workbook renamed the sheet of the same name in
  the other workbook, and deleting it turned the reference into `#REF!`. Every other reference
  behind a book prefix, e.g. `[1]Sheet!A1`, `[1]Sheet!Name` or `[1]Sheet!F(1)`, already left
  its sheet alone, and now the 3D reference does too.
- Parse `LOG10(` in an A1 formula as the function `LOG10`. `LOG10` is also a cell, column `LOG`
  row 10, so the lexer reads `LOG10(` as a cell function, and `IAstFactory.CellFunction`
  received it. `RefModVisitor` special-cased the name by scanning the formula text, but every
  other factory, e.g. an evaluator, had to know it too. A cell function is a construct of a
  macro sheet and no other function has a name that is also a cell, so the parser now calls
  `IAstFactory.Function` for it. A cell function on any other cell, e.g. `B$3(5)`, is
  unchanged, and so is R1C1, where `LOG10` isn't a cell.
- Read an area whose colon has spaces around it, e.g. `A1 : B2`, in `ReferenceParser`. The
  lexer puts the whitespace around `:` into the colon token, but `ReferenceParser` read the
  whole text as if the colon were bare, so `TryParseA1("A1 : B2")` returned `true` with the
  area `A1::-16`, and `TryParseR1C1("R1C1 : R2C2")` threw `InvalidOperationException`. It now
  reads each cell of such an area, as the formula parser already did.
- Keep the sheet of a sheet-qualified `#REF!` when a formula is converted or modified.
  `RefModVisitor.ErrorNode` cut the sheet out of the formula text instead of reading the
  sheet prefix, so a quoted sheet was quoted again: `FormulaConverter.ToR1C1("'Old sheet'!#REF!", 1, 1)`
  gave `'''Old sheet'''!#REF!`, which doesn't parse back, and a rename of `Old sheet` missed
  it. A space after the `!` became part of the name (`Old! #REF!` gave `'Old!'!#REF!`), a
  bang reference `!#REF!` threw `ArgumentException`, and the book index of
  `'[1]Old sheet'!#REF!` reached `ModifySheet` as part of the sheet name. The sheet is now
  read the way the parser reads it, and a space after the `!` is dropped as it is for a
  sheet reference. A sheet behind a book prefix belongs to another workbook and is left as
  it is. 116 formulas of the Enron and EUSES data sets that didn't survive a conversion to
  R1C1 and back now do.
- Parse the implicit intersection operator `@` wherever a reference operand can start.
  It parsed only at the head of a whole reference expression. `SUM(@A1:A4)`,
  `IF(@A1,1,2)` and `D3:@A1:C2` failed with `Unexpected token INTERSECT`, and
  `A1:B2 @C1:C9` stopped after `A1:B2`. R1C1 had the same failures, e.g.
  `SUM(@RC:R[3]C)`. An argument entered the reference rules one level below the only rule
  that accepts `@`. The ANTLR grammar already went through that rule, so only the
  recursive descent parser refused `@` in an argument. The precedence of `@` does not
  change. It binds looser than `:` and the space, so an operand that starts with `@` takes
  the rest of the intersection: `D3:@A1:C2` is `D3:(@(A1:C2))`, and `SUM(@A1:A4)` takes the
  implicit intersection of the whole range. Excel displays a legacy formula the same way,
  e.g. `ABS(@A1:A10)`. The lexer puts the space before `@` into the `INTERSECT` token, so
  after a reference, a space before `@` is the intersection operator. Without the space,
  `A1@B1` is still refused. A closing brace and the spill operator `#` take the space
  after them into their own token, so `(A1) @B1` and `A1# @B1` are refused too. The
  recursive descent parser also read an `@` after a reference in braces as the prefix of
  that reference, so it accepted `(A1) @:B1` as `@((A1):B1)`. The ANTLR parser refused it,
  and now both parsers do. The ANTLR grammar has the same rules and its parser is
  regenerated. The test helper `AssertFormula.CstParsed` also fails now when the ANTLR
  parser recovers from an error in a nested rule. Before this change, it accepted
  `D3:@A1:C2`, which the ANTLR parser did not parse.
  [#13](https://github.com/XLibur/ClosedXML.Parser/issues/13)
- Parse a bang name, e.g. `!SomeName`. The lexer had no token for a name after a bang, so
  `!SomeName` and `SUM(!SomeName)` failed in both reference styles with `Unable to
  determine token`, although `IAstFactory.BangName`, `BangNameNode` and
  `CopyVisitor.BangName` were already in place. [MS-XLSX] 2.2.2.1 forbids a bang name in a
  cell formula, but the formula of a defined name uses it. The name is a new token,
  `BANG_NAME`, declared last in `FormulaLexer.g4` so no other token ID moves, and the ANTLR
  lexer and parser, both Rolex grammars and both DFA tables are regenerated. A reference
  such as `A1` is also a valid name, so the two tokens tie on `!A1`, and the bang reference
  is declared first and wins: `!A1`, `!$A$1`, `!A1:B2` and, in R1C1, `!RC` are still bang
  references. A name can't be `TRUE` or `FALSE`, so `!TRUE` and `!FALSE` are refused. A bang
  before a structure reference (`!Sales[Amount]`) is still refused, because [MS-XLSX]
  defines a bang name as a plain name.
  [#14](https://github.com/XLibur/ClosedXML.Parser/issues/14)
- Lex the error values Excel added after [MS-XLSX] was written: `#SPILL!`, `#CALC!`,
  `#FIELD!`, `#BLOCKED!`, `#CONNECT!`, `#BUSY!`, `#UNKNOWN!`, `#EXTERNAL!`, `#PYTHON!` and
  `#TIMEOUT!`. The lexer knew only the [MS-XLSX] list, which ends at `#GETTING_DATA`, so a
  formula such as `ERROR.TYPE(#SPILL!)` failed in both reference styles with `Unexpected
  token SPILL`. That names the token of a bare `#`, the spill operator, not the error. They
  are error constants like the others, so `IAstFactory.ErrorValue` receives them in upper
  case and no parser rule changes. The list is Microsoft's `ErrorCellValueType` plus
  `#UNKNOWN!` from Python in Excel. The internal Pratt lexer accepts them too.
  [#12](https://github.com/XLibur/ClosedXML.Parser/issues/12)
- Treat malformed UTF-16 as invalid input instead of throwing out of the lexer. A trailing
  high surrogate read past the end of the input: the bounds check was `index >=
  input.Length`, which can never be true, because the caller only calls into the reader
  while `index < input.Length` — it should have been `index + 1`. Text ending in a lone
  high surrogate therefore threw `IndexOutOfRangeException`, and a high surrogate followed
  by anything other than a low surrogate threw `ArgumentOutOfRangeException` from
  `char.ConvertToUtf32`. A surrogate is now only combined when a low surrogate actually
  follows it; anything else is lexed as an error token, so `TryParseA1` and `TryParseR1C1`
  return `false` rather than throwing. Paired surrogates are unaffected.
- Reject a written R1C1 axis number of zero. `C0` parsed as if it were `C`, so
  `TryParseR1C1("C0")` returned `true` and `ToA1("C0")` quietly produced `C:C`. Rows and
  columns are numbered from 1; only a missing number (`R`, `C`) and a bracketed zero
  (`R[0]`, `C[0]`) mean an axis relative to the current cell. The axis reader could not
  tell an absent number from a written `0`, because both left its accumulator at zero.
  Only columns were affected — the grammar had a bare zero for a column but not for a row,
  so `R0` and `R0C0` were already refused. The bare zero is now gone from the grammar and
  the R1C1 DFA is regenerated, so `C0`, `R1C0` and `C0:C2` lex as a name rather than as a
  reference and are refused before the reader sees them. `ToA1("C0")` now round trips it as
  a defined name, which is what it is.
- Read the called cell of a cell function in the formula's reference style.
  `TokenParser.ExtractCellFunction` always read it as A1, so in R1C1 mode `R7C3(TRUE)` was
  read as the A1 cell `R7` and the `C3` was thrown away. It failed silently, because what
  it produced still looked like a plausible reference. `FormulaConverterToA1Tests` carried
  the case as `Skip = "Parser bug"`, and its expectation was wrong as well — `R7C3` is
  absolute row 7 and absolute column 3, so it converts to `$C$7`, not `$E$11`. The
  reference style is now an adapter taken once beside the DFA table it belongs with, so
  the table and the reader cannot disagree and there is no longer a style flag that can be
  passed incorrectly.
- Parse a quoted sheet prefix in the Pratt parser. `QIdent` was lexed but no prefix
  parselet was registered for it, so every quoted sheet reference failed — `'New York'!A1`
  and `'Jane''s'!A1` as much as anything else a serializer quotes. The new parselet strips
  the apostrophes, collapses the doubled ones, and handles `'sheet'!A1`, `'sheet'!name` and
  `'first:last'!A1`. External workbook prefixes stay unsupported, as they are on the
  unquoted path.
- Quote sheet names the way Excel's file format requires, not the way its formula bar
  displays them. The quotation tables were collected from the formula bar, which is more
  permissive than the file format: Excel shows `ABC～!A1` for a sheet named `ABC～`
  (U+FF5E) but stores `'ABC～'!A1`. For 41 codepoints in the first position and 37 in a
  later one the tables said no quotes were needed when Excel quotes. For some of those
  Excel refuses to open a workbook that references them unquoted, and which ones depends
  on the position: U+2028, U+2029, U+202A–U+202E, U+303D and U+303E as the first
  character, U+2065–U+2069 and U+303D anywhere. The tables were re-collected from saved
  workbooks across the whole BMP; see `tools/sheet-quotation`. Upstream issue
  [ClosedXML/ClosedXML.Parser#29](https://github.com/ClosedXML/ClosedXML.Parser/issues/29).
- Quote a sheet named `TRUE` or `FALSE`, in any casing. Every character is unremarkable
  on its own, so only the whole name gives it away; unquoted, Excel reads `TRUE!A1` as a
  logical literal and refuses to open the file.
- Escape apostrophes in a sheet name relative to where the name was appended.
  `NameUtils.EscapeName` replaced from a hardcoded index 1, which is only correct when
  the `StringBuilder` is empty. Every caller happens to pass an empty builder, so no
  output was wrong, but the helper mangled the formula for any that did not.
- Write formula numbers with the invariant culture. `ToR1C1` used the current culture's
  negative sign, so under sv-SE, fi-FI or nb-NO (negative sign U+2212) it emitted
  `RC[−1]`, which the R1C1 reader could not parse back. Upstream issue
  [ClosedXML/ClosedXML.Parser#30](https://github.com/ClosedXML/ClosedXML.Parser/issues/30).
- Parse a keyword list that is a whole inner reference.
