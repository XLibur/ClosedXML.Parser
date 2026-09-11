# Rolex

Rolex is a DFA based lexer generator for C#, by codewitch-honey-crisis. It takes a file of
regular expressions and emits a state machine as a C# source file.

* Source: <https://github.com/codewitch-honey-crisis/Rolex>
* Licence: MIT
* Background: [Rolex: Unicode Enabled Lexer Generator in C#](https://www.codeproject.com/Articles/5257489/Rolex-Unicode-Enabled-Lexer-Generator-in-Csharp)

This project uses it for the formula lexer. ANTLR (`src/ClosedXML.ANTLR/FormulaLexer.g4`)
is the source of truth for the grammar; `tools/Antlr2Rolex` converts it to Rolex's regular
expression format in `src/ClosedXML.Parser/Rolex/LexerA1.rl` and `LexerR1C1.rl` (see the
root `README.md`, *Generate lexer*), and Rolex turns each
of those into a table - `RolexA1Dfa.cs` and `RolexR1C1Dfa.cs` - that `RolexLexer.cs` walks
at run time. It is about twice as fast as the ANTLR lexer.

## What is in this folder

| file | what it is |
|------|------------|
| `rolex.exe` | a build of Rolex `master` with a local fix to `_Determinize` in `FA.brick.cs` |
| `rolex.exe.config` | targets .NET Framework 4.7.2 |

Debug symbols and the sample grammars that ship with Rolex are deliberately not vendored.

The local fix addresses an `OutOfMemoryException`: stock `master` cannot generate
`LexerR1C1.rl` at all, dying after about 6.5 minutes even with 64 GB of RAM available. The
epsilon closure of every state is recomputed inside the loop over character ranges,
although the input automaton never changes during determinisation, and the closure routine
uses a linear `IList.Contains` for its seen check while allocating a fresh list per call.
Memoising the closures brings it down to about 2.6 seconds. That fix belongs upstream and
is not specific to this project.

## This build cannot produce a drop-in table for this repository

Rolex changed its output format after these tables were generated.

* **This build emits** a flat `int[]` table, plus a dependency on `TextReaderEnumerable`.
* **The committed tables are** the older `DfaEntry[]` object graph, backed by
  `DfaEntry.cs`, `DfaTransitionEntry.cs` and `TableTokenizer.cs` in
  `src/ClosedXML.Parser/Rolex/`.

Swapping a table from this build into the project does not compile. `/noshared` does not
help: it only controls whether the shared runtime is emitted alongside the table, not the
table's shape.

## Which build did produce them

Worth recording, because the version history is misleading:

* The `DfaEntry` format was replaced in Rolex at `73a7c20` (2023-11-17, "simplified DFA
  generation"). The last commit that still has it is **`91a2d6d` (2020-03-14)**.
* These tables were generated on **2023-10-22** (`ad412b8` in this repository, which
  updated both the `.rl` files and both tables together).
* The Rolex repository has **no commit at all between 2020-03-14 and 2023-11-17**.

So on dates alone it looks as though the generator that built these tables is not in
Rolex's history. It is. Building `91a2d6d` unpatched produces a table that compiles and
fails ten tests; building it with the `pc.Advance()` patch that this project's root
`README.md` already describes reproduces **both** committed tables byte for byte from the
unmodified `.rl` files. The gap is explained by the missing patch, not by a missing
version.

The full recipe - the commit, the patch, emptying the `PreBuildEvent` so it builds at all,
and what goes wrong at each step if you skip one - is in
[issue #9](https://github.com/XLibur/ClosedXML.Parser/issues/9).

Keep this build for the memory fix and for experimenting with current Rolex. Do not use it
to produce a table for this repository.

## Running it

```
rolex.exe <input.rl> /noshared /class <ClassName> /namespace <Namespace> /output <output.cs>
```

`/class` matters: without it the class is named after the input file, which is not what the
committed tables are called.

## The rule that keeps this safe

Whichever build you use, **regenerate from the unmodified `.rl` first and diff the result
against the committed table.** If it is not byte for byte identical, the toolchain is wrong
and nothing generated with it can be trusted - a mismatched generator produces a table that
compiles, looks reasonable, and silently mis-tokenises. That check is the only reliable
signal, because nobody reads an 8,000 line table by eye.
