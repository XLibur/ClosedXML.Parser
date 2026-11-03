# Rolex

Rolex is a DFA based lexer generator for C#, by codewitch-honey-crisis. It takes a file of
regular expressions and emits a state machine as a C# source file.

* Source: <https://github.com/codewitch-honey-crisis/Rolex>
* Fork used here: <https://github.com/jafin/Rolex>
* Licence: MIT
* Background: [Rolex: Unicode Enabled Lexer Generator in C#](https://www.codeproject.com/Articles/5257489/Rolex-Unicode-Enabled-Lexer-Generator-in-Csharp)

This project uses it for the formula lexer. ANTLR (`src/ClosedXML.ANTLR/FormulaLexer.g4`)
is the source of truth for the grammar; `tools/Antlr2Rolex` converts it to Rolex's regular
expression format in `src/ClosedXML.Parser/Rolex/LexerA1.rl` and `LexerR1C1.rl` (see the
root `README.md`, *Generate lexer*), and Rolex turns each of those into a table -
`RolexA1Dfa.cs` and `RolexR1C1Dfa.cs` - that `RolexLexer.cs` walks at run time. It is about
twice as fast as the ANTLR lexer.

## What is in this folder

| file | what it is |
|------|------------|
| `generate-dfa-tables.sh` | regenerates both DFA tables, or checks them with `--check` |
| `v0.2/rolex.exe` | the Rolex build that generates the committed tables |
| `v0.2/rolex.exe.config` | targets .NET Framework 4.7.2 |

Debug symbols and the sample grammars that ship with Rolex are deliberately not vendored.

## Generating the DFA tables

```
tools/rolex/generate-dfa-tables.sh           # write both tables
tools/rolex/generate-dfa-tables.sh --check   # change nothing, fail if a committed table differs
```

Rolex is a .NET Framework executable, so run the script from Git Bash on Windows. A table
takes under a second. The `rolex-tables` job of the *Build and Test* workflow runs the script
with `--check` on `windows-latest`, so a `.rl` committed without its regenerated table fails
the build. `RolexGrammarConverterTests` checks the step before it, from `FormulaLexer.g4` to
the `.rl` files. Line ends are not compared, because Rolex writes CRLF and git stores the
tables with LF.

## The table format

Rolex emits one flat `int[]` per lexer. A state starts at an offset and holds:

* its accept symbol, or -1;
* its transition count;
* for each transition, the offset of the destination state, the number of ranges, and that
  many `(min, max)` pairs of code points, sorted by `min`.

The start state is at offset 0. `RolexLexer.GetToken` walks it and stops looking at a
transition's ranges once a code point is below one. The generated class also derives from a
`TableTokenizer` and has a constructor over a `TextReader`; neither is used here, and
`TableTokenizer.cs` holds stubs so the file compiles.

Until [#69](https://github.com/XLibur/ClosedXML.Parser/issues/69) the tables were an object
graph of `DfaEntry` and `DfaTransitionEntry`, which only a Rolex from 2020 could emit. That
build needed two hand patches and was the only one whose output matched. Moving to the format
the maintained generator emits removed the patches, about 2,700 heap arrays per table set, and
roughly 45 percent of the generated source (491 KB to 274 KB for A1).

## The build

`v0.2/rolex.exe` is a Release build of [jafin/Rolex](https://github.com/jafin/Rolex) at
commit `ad2301fd0801a687f6b12cde87c38a989036f133`, the head of
[jafin/Rolex#11](https://github.com/jafin/Rolex/pull/11). Its SHA-256 is
`b5af453e300c56444235dc10aedae9ffe9e9a2ea31e3128d4505757e91c903fa`. It is a local build, to
be replaced by the `v0.2` release asset of that repository once it is cut. The tables do not
change when it is, because generation is deterministic; only this hash does.

Every defect that stood in the way of using a current Rolex is fixed in the fork:

| defect | where |
|--------|-------|
| `\uXXXX` and `\xXXXX` escapes lost their last digit, so a range such as `[A-Z]` was `1-Z` | fixed in the fork, [issue 1](https://github.com/jafin/Rolex/issues/1) |
| `OutOfMemoryException` determinising `LexerR1C1.rl`, after 6.5 minutes | fixed in the fork, [issue 2](https://github.com/jafin/Rolex/issues/2) |
| `_KeySet.Add` cancelled its own hash, giving duplicate DFA states | fixed in the fork, [issue 4](https://github.com/jafin/Rolex/issues/4) |
| `x?` after a group ending in a loop let the start state inherit the loop, so `(ab*)?a` accepted `ba` and `[ ]` was a table item ([#69](https://github.com/XLibur/ClosedXML.Parser/issues/69)) | [jafin/Rolex#7](https://github.com/jafin/Rolex/pull/7) |
| a literal character above U+FFFF in a pattern was read as two surrogates, so the astral range of every name rule was missing | [jafin/Rolex#11](https://github.com/jafin/Rolex/pull/11) |

All of these were silent: the generator exited 0, the table compiled, and the lexer
recognised a different language. The fork keeps a regression test for each.

## Using the build elsewhere

```
rolex.exe <input.rl> /noshared /class <ClassName> /namespace <Namespace> /output <output.cs>
```

Without `/class` the class is named after the input file, which is what the script relies on.
Rolex draws a progress bar on stderr, so the script shows its output only when it fails.

## The rule that keeps this safe

Whichever build you use, **regenerate from the unmodified `.rl` first and diff the result
against the committed table** - `generate-dfa-tables.sh --check` does exactly that. If it is
not byte for byte identical, the toolchain is wrong and nothing generated with it can be
trusted - a mismatched generator produces a table that compiles, looks reasonable, and
silently mis-tokenises. That check, together with `AntlrCompatibilityTests`, which lexes every
short input with both this lexer and the ANTLR one and requires the same tokens, is the only
reliable signal, because nobody reads an 11,000 line table by eye.
