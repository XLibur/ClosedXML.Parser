# ClosedXML.Parser.Fuzz

A coverage-guided fuzzing harness for the parser, driven by [libFuzzer][libfuzzer] through
[SharpFuzz][sharpfuzz]. It feeds bytes to a public entry point and checks a property that a wrong
answer breaks, not only that the library did not crash.

Run it from the repository root:

```powershell
./fuzz.ps1 -Target formula-a1 -MaxTotalTime 600
```

`fuzz.ps1` publishes this project, instruments `ClosedXML.Parser.dll` and
`ClosedXML.Parser.Ast.dll`, seeds a working corpus from `corpus/<target>`, and runs libFuzzer. Use
`-Target all` for every target in turn.

One binary is needed that NuGet cannot restore: `libfuzzer-dotnet-windows.exe`, from the
[libfuzzer-dotnet releases][driver]. Put it in `tools/`, where it is gitignored. The script says so
if it is missing.

## Targets

| Target | Input | What it checks beyond "does not crash" |
| --- | --- | --- |
| `formula-a1` | A1 formula text | Every reference and name in the tree is written back out and parses again as the same node. |
| `formula-r1c1` | R1C1 formula text | The same, in R1C1. |
| `modify` | A1 formula text | A modification that changes nothing returns the formula character for character, and applying it twice changes nothing more. |
| `convert` | A1 formula text | The R1C1 form parses as R1C1 and the A1 form it converts back to parses as A1. |
| `reference` | Reference or name text | The six `ReferenceParser` entry points agree with each other, and an area survives being written and read again. |

## Triage

A crash writes an artifact to `temp/fuzz/artifacts`. Replay it — no libFuzzer needed, and the same
oracle decides:

```powershell
./fuzz.ps1 -Target formula-a1 -Replay temp/fuzz/artifacts
```

Replay prints one line per input and then groups them by exception type and originating library
frame. Six artifacts under one signature are one defect; fix it once and all six go.

Reduce an artifact before diagnosing it — a 128-byte input cut to 4 bytes usually names its own bug:

```powershell
./fuzz.ps1 -Target formula-a1 -Minimize temp/fuzz/artifacts/crash-<hash>
```

The shrunk input lands next to the original as `minimized-from-<hash>`.

Each defect ends as a unit test plus a seed in `corpus/<target>`, named for what it is. The seed
keeps the fuzzer from spending its budget rediscovering the same input; the test keeps the next
refactoring from putting the defect back.

## What counts as wrong

`Oracle.cs` decides, and it splits by phase, because the same exception type means opposite things
at different points:

- **Parse.** `ParsingException` means "this text is not a formula", which is correct behaviour.
  Nothing else is. In particular an `ArgumentException` names a parameter the caller never passed,
  so it is an internal precondition escaping rather than a verdict on the input.
- **Process.** The parser has already accepted the input, so writing a display string, modifying the
  formula or converting it must work. Only `OutOfMemoryException` is tolerated, and it is recorded.
- **Reparse.** Nothing is tolerated. The library wrote that text, so the library can read it.

Tolerated-but-notable events are appended to `temp/fuzz/artifacts/tolerated.tsv`, deduplicated by
message. A run that finds no crash is not necessarily a run that found nothing.

## Known gaps

- Input is decoded as UTF-8, which never produces a lone surrogate, so the lexer's
  `ParsingException.UnpairedSurrogate` path is unreachable from every target here. Reaching it needs
  a target that decodes UTF-16.
- The parser has no recursion guard, so deeply nested text will exhaust the stack. `-max_len` is
  held at 512 to keep the budget on the grammar rather than on nesting depth.

[libfuzzer]: https://llvm.org/docs/LibFuzzer.html
[sharpfuzz]: https://github.com/Metalnem/sharpfuzz
[driver]: https://github.com/Metalnem/libfuzzer-dotnet/releases
