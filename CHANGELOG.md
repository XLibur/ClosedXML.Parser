# Changelog

All notable changes to XLibur.ClosedXML.Parser are recorded here. The Publish Release
workflow rolls the Unreleased section into a dated version heading, so add an entry
under Unreleased with each change.

## Contents

- [Unreleased](#unreleased)

## Unreleased

### Added

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
