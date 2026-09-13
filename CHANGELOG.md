# Changelog

All notable changes to XLibur.ClosedXML.Parser are recorded here. The Publish Release
workflow rolls the Unreleased section into a dated version heading, so add an entry
under Unreleased with each change. A version opens with a one-paragraph summary and
groups its entries by the area of the library they touch, each area by Added, Changed
or Fixed.

## Contents

- [Unreleased](#unreleased)
- [v3.1.0](#v310---2026-09-13)
- [v3.0.0](#v300---2026-09-13)

## Unreleased

### Summary

XLibur.ClosedXML.Parser 3.1.0 is a fix release, and most of it comes out of two new test tools: a
coverage-guided fuzzing harness driven by libFuzzer, and a differential sweep of the two lexers over
every short input from a bracket-and-punctuation alphabet. Structured references take the largest
share. Three items that reached the parser as an `IndexOutOfRangeException` or a
`NotSupportedException` are now read or refused properly, `[:b]` is a column called `:b` rather than
a range with a nameless side, `[a:b:c]` keeps the whole of its second column name, and a structured
reference is written the way the parser reads it: a range of columns keeps its colon, an ordinary
`Table1[Column]` is no longer doubled, and the four characters the grammar escapes are escaped. The
library also holds a sheet name to one rule everywhere. The formula parsers, `ReferenceParser` and a
`FormulaModifier` rename all refuse a name no workbook could hold, including a name that starts or
ends with an apostrophe, instead of building a reference that has no spelling to write it in. An
empty formula now raises the `ParsingException` all four `FormulaConverter` methods document.
## v3.1.0 - 2026-09-13

### Formula parsers

#### Changed

- Allocate less on the paths that change nothing. A modification that leaves a part of a formula
  alone is the common outcome, and three places allocated anyway: a part nothing changed was copied
  out of the formula through a `StringBuilder` rather than sliced from it, six builders were sized
  with a LINQ `Sum` over an interface list, which boxes an enumerator each time, and the letters of
  an A1 column were built by prepending to a string, one allocation a letter, before being handed
  to a builder the caller already held. Measured over the enron and euses data sets, a sheet rename
  allocates about a quarter less — 678 B to 496 B and 772 B to 575 B a formula — and a conversion
  between reference styles 3 to 8 per cent less. Run times are unchanged.

- Refuse a sheet name no workbook could hold, so `€?:D!A1` and `a?:D!A1` are parse errors rather
  than 3D references. `NameUtils.IsSheetNameValid` already said such a name is illegal, and the
  parser assembled a `Reference3DNode` from it anyway — a node with no spelling in the language:
  writing it asks for quotes, and a quoted name holding a `?` reads back as a DDE item rather than
  as a sheet prefix, so `FormulaConverter.ToR1C1` produced text the library could not read back.
  Two shapes reached the parser, because a sheet name token is narrower than a sheet name: the
  first sheet of a bare 3D reference, which is a `NAME` token and so may hold a `?`, and any name
  longer than the 31 characters a sheet may have. The prefix of a DDE reference is an application
  and a topic rather than a sheet, so it is not held to the rule, and a name of the same text that
  names something other than a sheet is untouched. No formula in the enron or euses data sets reads
  differently.

#### Fixed

- Refuse a formula that nests deeper than 256 levels, instead of taking the whole process down with
  a `StackOverflowException`. The parser descends by recursion and counted nothing, so a formula
  that nested deeply enough ran the stack out — and a stack overflow cannot be caught, so a host
  reading an untrusted workbook died where it should have rejected one formula. Braces cost about
  seven stack frames a level and ran out first, at 378 of them: a formula of 757 characters, well
  inside the 8192 a cell can hold. A chain of unary operators and a chain of arguments reach the
  same end by their own paths, at their own depths. Excel accepts at most 64 levels of nested
  functions, so the limit refuses only formulas no workbook holds, and a formula past it now raises
  a `ParsingException` like any other the parser will not read.

- Read a name, a text or a column name of any length, instead of taking the whole process down with
  a `StackOverflowException`. A scratch buffer was taken from the stack and sized from the token it
  copies, and the grammar puts no length limit on any of the three, so a long enough one ran the
  stack out the same uncatchable way — around a million characters, or half that on a thread pool
  thread, which has a smaller stack. Past 256 characters the buffer now comes from the heap, which
  costs nothing next to the string built from it. The three buffers that copy an error token are
  bounded by the grammar to about twenty characters and keep the stack, as their comments say.

- Name the argument in the exceptions two guards raise. `Token.GetSymbolName` built its
  `ArgumentOutOfRangeException` with the overload that takes a parameter name rather than a
  message, so an unknown symbol read as `Specified argument was out of the range of valid values.
  (Parameter 'Invalid symbol 5.')`. It sits on the path that reports an unexpected token, so it
  degraded every such diagnostic. Two of `ReferenceParser`'s six entry points threw
  `ArgumentNullException` with no argument name at all, three lines from four that pass one,
  leaving a caller that reads `ParamName` with nothing to read.

- Refuse a structured reference with an item that holds nothing but whitespace, `[ ]` or
  `[[#Data], ]`, instead of raising an `IndexOutOfRangeException` from inside the token parser.
  Reading the token peeked one character past its end wherever an item was expected — after the
  opening bracket and after each comma — so three characters of a stored formula came out of the
  parser as an index error rather than as a `ParsingException`. The grammar has no alternative for an empty
  inner reference — a simple column name has to start and end with a non-space — and the ANTLR
  lexer, which is the source of truth, refuses `[ ]` outright. The Rolex lexer still accepts it as
  a whole `INTRA_TABLE_REFERENCE` token, a divergence between the two lexers that outlives this
  fix; the refusal therefore happens when the token is read.

- Read a tick-escaped `#` at the start of a structured reference item as the column name it is,
  `[ '#]`, instead of raising a `NotSupportedException`. A column name escapes a `#` with a tick, so
  such a column has a `#` as its second character — the same shape a keyword has — and the keyword
  reader looked no further than that character. It then found no keyword to match and threw from a
  default arm whose comment says the tokenizer has ruled the case out. The opening bracket is what
  tells the two apart, and both places that look for a keyword ask for it now. `['#]` and `[['#]]`
  were already read correctly; it was only the item after a space or a comma that was not.

- Read a colon with nothing on one side of it as part of a column name rather than as the separator
  of a range, so `[:b]` is a column called `:b`. A colon is an ordinary column character, and the
  two sides of a range are each a column, which cannot be empty — so that is the only reading the
  grammar leaves. Splitting it invented a column with no name, and a column with no name has no
  spelling in the bracketed form a display string is written in: `[[]:[b]]` is not a structured
  reference, so the reference did not survive being written out.

- Read the last column of a simple range to the closing bracket, so `[a:b:c]` is the columns `a` to
  `b:c`. A range has one separator and two columns; stopping the second name at a colon as well cut
  it short at `b` and dropped the rest without a word.

### Standalone reference parsing

#### Changed

- `ReferenceParser.TryParseSheetA1` and `ReferenceParser.TryParseSheetName` return `false` for a
  sheet name `NameUtils.IsSheetNameValid` rejects, the rule the formula parsers now hold a sheet
  prefix to, instead of handing back a name no workbook could hold.

### Ast nodes and display strings

#### Fixed

- Write the specifier of a structured reference the way the parser reads it. A range of columns was
  joined with a comma rather than a colon, so `Table1[[#Data],[A]:[B]]` came out as
  `Table1[[#Data],[A],[B]]`; the parser fills a missing last column in with the first, so the
  ordinary `Table1[Column]` came out as `Table1[[Column],[Column]]`; and an external reference lost
  the bang after its book prefix, so `[4]!Table1[Column]` came out as `[4]Table1[Column]`. None of
  the three parse back as the node they came from, and the visualizer puts them in its diagram. A
  lone specifier keeps only its own brackets and everything else gets a pair around the list, so
  `Table1[#Totals]` and `Table1[[#Headers],[#Data]]` are both written as they are read.
  `StructureReferenceNode` and `ExternalStructureReferenceNode` write the specifier in one place
  now, `StructuredReferenceWriter`, instead of each building its own.

- Escape a tick, either square bracket and a hash in the column name of a structured reference, the
  four characters the grammar has an escape for. Written bare they read as something else: a column
  called `#` came out as `[#]`, and a hash after a bracket starts a keyword, so the string no longer
  parsed at all; a column called `[Col` came out as `[[Col]`. The library already unescapes all four
  when it reads a name, so the two halves now agree.

### Formula modification and conversion

#### Changed

- Refuse a `FormulaModifier` that renames a sheet to a name `NameUtils.IsSheetNameValid` rejects,
  with an `InvalidOperationException` from `FormulaConverter.ModifyA1` and `ModifyR1C1`. A renamed
  sheet is written back into the formula, and a name no workbook could hold has no spelling to write
  it in: it needs quotes, and a quoted name holding a `?` reads back as a DDE item rather than as a
  sheet prefix, so the modification produced text the library could not read. The modifier is the
  caller's own code, so this is a fault in the call rather than in the formula and is not a
  `ParsingException`. Only a rename is held to the rule: `null` still means the sheet is gone and
  the part becomes `#REF!`, a name the modifier leaves alone is never refused, and the prefix of a
  DDE reference is an application and a topic rather than a sheet and was already left alone.

#### Fixed

- Refuse a formula that is empty or nothing but whitespace with a `ParsingException`, the type all
  four `FormulaConverter` methods document, instead of an `ArgumentException` naming a parameter
  the caller never passed. The same text reached the parser as a `ParsingException` and the
  converter as an argument error, so which exception a caller saw depended on which entry point
  read it. A null formula is still an argument error, because that is a fault in the call rather
  than in the formula.

- Name `col` rather than `row` in the `ArgumentOutOfRangeException` for a column anchor outside the
  sheet.

### Sheet names and quoting

#### Changed

- `NameUtils.IsSheetNameValid` refuses a name that starts or ends with an apostrophe. Excel refuses
  one, and the library has its own reason to agree: a sheet prefix is quoted with apostrophes, so one
  at either end has nowhere to go. A leading apostrophe was worse than unreadable — `ShouldQuote`
  says such a name needs no quotes, so `'leading` was written bare as `'leading!`, which neither
  lexer reads at all. A trailing one was written `'trailing'''!`, which the Rolex lexer reads back
  correctly but the ANTLR lexer reads as a DDE item; that divergence is now unreachable through the
  library. An apostrophe anywhere else is ordinary and is doubled inside the quotes, so `Jane's` and
  `a'b` are names as before.

  The Pratt parser accepted `'''leading'!A1` and `'trailing'''!A1` and no longer does, which brings
  it into line with the main parser — that one already refused both, reading the quoted text as a
  DDE item. No formula in the enron or euses data sets reads differently.

- `NameUtils.ShouldQuote` says a name starting with an apostrophe has to be quoted. The quoting
  tables are collected from what Excel saves, and Excel refuses to name a sheet that way, so the
  probe had nothing to observe and the table recorded the fallback: the name needed no quotes and was
  written bare. `ShouldQuote` answers for the application and the topic of a DDE link as well, and
  either of those can start with an apostrophe, so the answer is not academic. The unobservable row
  is gone from `tools/sheet-quotation/ident-sheet-first.txt`, the way the seven characters Excel
  refuses anywhere are already left out of both tables; `ident-sheet-next.txt` keeps its `0027 YES`,
  which is a real observation.

### Packaging and tooling

#### Added

- A differential sweep of the two lexers, `AntlrCompatibilityTests.Produce_same_tokens_for_every_short_input`,
  over every string of up to four characters from a bracket-and-punctuation alphabet. The data set
  comparison only says the two agree on text somebody wrote, and text nobody wrote is where they
  drift apart: no formula in enron or euses holds `[ ]`. Nothing is skipped: the sweep holds the
  whole run to a recorded list of the 20 inputs the two lexers read differently, each with the token
  stream both of them produce, so a new divergence and a change to a known one both fail. All 20 are
  the same defect, a bracket holding nothing but spaces, read alone and after and before other
  tokens. It is a defect in Rolex's DFA construction, not a stale table: the regular expression in
  `LexerA1.rl` refuses `[ ]`, the committed table is what the vendored build produces from it, and
  rewriting the column name as `X (Y* X)?` rather than `(X Y*)? X` changes the table without
  changing this.

- A coverage-guided fuzzing harness, `src/ClosedXML.Parser.Fuzz`, driven by libFuzzer through
  SharpFuzz and run from `fuzz.ps1`. Five targets cover formula parsing in both reference styles,
  formula modification, conversion between styles and the standalone reference parsers. Each checks
  a property a wrong answer breaks rather than only that the library did not crash: a reference
  written back out has to parse as the node it came from, a modification that changes nothing has
  to return the formula character for character, and text the library wrote has to be text the
  library can read. The seed corpus is committed alongside, and every defect found keeps its input
  there. See `src/ClosedXML.Parser.Fuzz/README.md`.

## v3.0.0 - 2026-09-13

### Summary

XLibur.ClosedXML.Parser 3.0.0 is the first release of this fork of ClosedXML.Parser 2.0.0,
published under a new package id with the `ClosedXML.Parser` namespace unchanged. The largest
change is that a formula modification now copies every part of the formula it does not change,
character for character, instead of writing each reference back from the values the parser read: a
modifier that changed nothing used to drop quotes a sheet name was written with, collapse an area of
one cell and re-quote unrelated references across the whole formula. Breaking changes come with it.
`IAstFactory` gains three methods, `SheetErrorNode` for a sheet-qualified `#REF!` and two for
dynamic data exchange references, and `RefModVisitor` becomes `FormulaModifier`, carrying only the
five methods a modification overrides. The parser also reads more of what Excel writes: dynamic data
exchange references, bang names such as `!SomeName`, the ten error values Excel added after
[MS-XLSX] was written, `@` wherever a reference can start, and a space intersection after braces.
The library now quotes a sheet name the way the file format requires rather than the way the formula
bar displays it, which covers 41 codepoints in the first position and 37 in a later one that the old
tables left bare. R1C1 gains the two entry points it lacked, `FormulaConverter.ModifyR1C1` and
`ReferenceParser.TryParseR1C1`.

### Lexer and grammars

#### Added

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

#### Fixed

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

### Formula parsers

#### Fixed

- Parse a space intersection after an expression in braces, e.g. `(A1) B2`. The lexer puts the
  whitespace around an operator into its token, so the `)` of `(A1) B2` is lexed as `) ` and there is
  no `SPACE` token left for the intersection operator. Both parsers looped on a `SPACE` token, so
  `(A1) B2` failed with "the rest `B2` wasn't" parsed and `SUM((A1) B2)` with an unexpected token,
  although the comment that explains the backtracking to a reference expression in `FormulaParser`
  uses this very shape as its example. A space at the end of the token before a reference is now the
  intersection operator too, which also fixes the other tokens that take the space: a spill range
  (`A1# B2`), a structured reference (`[Col] B2`) and a call of a function that returns a reference
  (`INDEX(A1:B2,1,1) B2`). It is the operator only before a reference, so `(A1) + B2` is still an
  addition. `FormulaParser.g4` gets the same alternative behind a predicate, the way the space before
  an `@` already is, and the ANTLR parser is regenerated; the lexer grammar and both DFA tables are
  untouched. A space before an `@` that another token took, `(A1) @B1`, is still not the intersection
  operator. No formula of the data sets parses differently.

- Keep the range of an expression in braces that turns out to be a reference, e.g. `(A1):B2`. The
  parser reads `(A1)` as a value expression, and when the `:` shows it is a reference expression, it
  backtracks and passes the node it has already read to the reference expression. That expression
  took its start from the token the parser had reached, which is past the braces, so every range
  around them began too late. A modification then spliced the text at the wrong place:
  `SUM((Total_Cost Jan):(Total_Cost Apr.))` of the EUSES data set came back as
  `SUM(Total_Cost Jan)(Total_Cost Jan):(Total_Cost Apr.))`, and its R1C1 form no longer parsed. The
  node the parser has already read now carries the index it starts at.

- Parse `LOG10(` in an A1 formula as the function `LOG10`. `LOG10` is also a cell, column `LOG`
  row 10, so the lexer reads `LOG10(` as a cell function, and `IAstFactory.CellFunction`
  received it. `RefModVisitor` special-cased the name by scanning the formula text, but every
  other factory, e.g. an evaluator, had to know it too. A cell function is a construct of a
  macro sheet and no other function has a name that is also a cell, so the parser now calls
  `IAstFactory.Function` for it. A cell function on any other cell, e.g. `B$3(5)`, is
  unchanged, and so is R1C1, where `LOG10` isn't a cell.

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

- Parse a keyword list that is a whole inner reference.

### Standalone reference parsing

#### Added

- `ReferenceParser.TryParseR1C1`. Every public method of `ReferenceParser` lexed with the
  A1 table, so a caller holding an R1C1 reference had no entry point at all, and the
  library's own tests had to reach through `InternalsVisibleTo` to parse one. The
  reference is read as written: a relative axis keeps its offset and is not resolved
  against an anchor cell, so `R[-1]C` gives a relative row of -1 and a relative column of
  0. The other five public methods keep their A1-only form.

#### Fixed

- Read an area whose colon has spaces around it, e.g. `A1 : B2`, in `ReferenceParser`. The
  lexer puts the whitespace around `:` into the colon token, but `ReferenceParser` read the
  whole text as if the colon were bare, so `TryParseA1("A1 : B2")` returned `true` with the
  area `A1::-16`, and `TryParseR1C1("R1C1 : R2C2")` threw `InvalidOperationException`. It now
  reads each cell of such an area, as the formula parser already did.

### Ast factory

#### Changed

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

- `IAstFactory` has two new methods: `ExternalDynamicDataExchange` for the stored form of
  a DDE reference and `DynamicDataExchange` for the displayed form. This breaks every
  implementation, because the library targets netstandard2.0, which has no default
  interface methods. `CopyVisitor` and `RefModVisitor` write a DDE reference back in the
  form it was written in, normalised the way a sheet reference is (e.g. a space after the
  `!` is dropped). The `application|topic` prefix of the displayed form is not a sheet, so a sheet
  rename leaves it alone. It stays bare, the way Excel writes it, although a sheet of the
  same name would be quoted because of the `|`; it is quoted only when it would not read
  back as the same link, e.g. with a space.

### Ast nodes and display strings

#### Fixed

- Quote a first sheet that is also a cell in the display string of a 3D reference, e.g.
  `'PWD1:Dec'!A1`. A written formula has quoted it since the fix for
  [#31](https://github.com/XLibur/ClosedXML.Parser/issues/31), but `Reference3DNode` wrote it bare,
  and a bare `PWD1:Dec!A1` reads back as a range of the cell `PWD1` and the reference `Dec!A1` — a
  different node, silently, without an error to show for it. The two writers each held a copy of the
  rule, so fixing one left the other behind. The condition lives in `NameUtils` now and both ask it.
  Nothing else changes: a cell-like last sheet was never in doubt, so `Jan:PWD1!A1` stays bare, and
  a book prefix already says a sheet prefix has started, so `[2]PWD1:Dec!A1` does too.
  [#40](https://github.com/XLibur/ClosedXML.Parser/issues/40)

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
  write their prefix in one place now, `SheetPrefixWriter`, instead of each building its own.
  [#34](https://github.com/XLibur/ClosedXML.Parser/issues/34)

### Formula modification and conversion

#### Added

- `FormulaConverter.ModifyR1C1`, the R1C1 counterpart of `ModifyA1`. `ModContext.IsA1` tells a
  modifier the reference style of the formula, and a reference it gets is in that style.

#### Changed

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

#### Fixed

- Leave the sheets of a 3D reference into another workbook alone when a sheet is renamed or
  deleted. `RefModVisitor.ExternalReference3D` passed both sheets of `[1]First:Last!A1` to
  `ModifySheet`, so renaming a sheet of this workbook renamed the sheet of the same name in
  the other workbook, and deleting it turned the reference into `#REF!`. Every other reference
  behind a book prefix, e.g. `[1]Sheet!A1`, `[1]Sheet!Name` or `[1]Sheet!F(1)`, already left
  its sheet alone, and now the 3D reference does too.

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

- Write formula numbers with the invariant culture. `ToR1C1` used the current culture's
  negative sign, so under sv-SE, fi-FI or nb-NO (negative sign U+2212) it emitted
  `RC[−1]`, which the R1C1 reader could not parse back. Upstream issue
  [ClosedXML/ClosedXML.Parser#30](https://github.com/ClosedXML/ClosedXML.Parser/issues/30).

### Sheet names and quoting

#### Added

- `NameUtils.ShouldQuoteAsFirstSheet`, which answers whether a name needs quotes in the first
  position of a 3D reference. `ShouldQuote` answers for a name standing on its own and
  deliberately leaves a name shaped like a reference bare, because the `!` of `PWD1!A1` settles
  it. The first sheet of a bare `first:last!` has no `!` in front of it yet, so a cell-like name
  lexes as the cell and takes the prefix with it. The two callers that write a sheet prefix, one
  for a formula and one for the display string of an Ast node, ask this instead of keeping the
  rule themselves. A caller writing a 3D prefix of its own needs the same answer and had no way
  to get it.

#### Fixed

- Quote a 3D reference whose first sheet is also a cell, e.g. `'PWD1:Last'!A1`. The bare form was
  written whenever neither sheet name needed quotes, but a bare `first:last!` is read as a name, a
  colon and a single sheet prefix, and `PWD1` (column `PWD`, row 1) lexes as a cell, not a name. So
  `PWD1:Last!A1` didn't parse back: the library couldn't read what it had just written.
  `NameUtils.ShouldQuote` answers for a name standing on its own, where the `!` of `PWD1!A1` settles
  it, and in the first position of a 3D prefix there is no `!` yet. The first sheet is now quoted
  unless the lexer reads it as a name. Nothing else changes: a book prefix already tells the lexer a
  sheet prefix has started, so `[3]PWD1:Last!A1` stays bare, and a cell-like last sheet was never in
  doubt, so `First:PWD1!A1` does too. A prefix is written without knowing the reference style of the
  formula it goes into, so the name has to be a name in both styles: a sheet called `R1C1` or `C` is
  now quoted as the first sheet of a 3D reference in an A1 formula as well, where it would have read
  back. Written output changes for these names, but no formula of the Enron, EUSES or contributions
  data sets has such a sheet, so their recorded outputs are unchanged. The check costs a lex of the
  first sheet name in each style, and only for a 3D prefix that would otherwise be written bare.

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

### Packaging and tooling

#### Changed

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
