# Benchmarks

[BenchmarkDotNet](https://benchmarkdotnet.org/) benchmarks of the hot paths of the parser.

## Run

Run the benchmarks in Release, from this directory (*src/ClosedXML.Parser.Benchmarks*):

```
dotnet run -c Release -f net10.0 -- --filter "*"
```

From the repository root, give the path of the project:

```
dotnet run -c Release -f net10.0 --project src/ClosedXML.Parser.Benchmarks -- --filter "*"
```

* To run one class, filter by its name: `--filter "*ParsingBenchmarks*"`.
* To get a fast result that is less accurate, add `--job short`.
* To compare runtimes, add `--runtimes net8.0 net10.0`.
* To list the benchmarks without running them, use `--list flat`.

BenchmarkDotNet writes the reports to *BenchmarkDotNet.Artifacts* in the current directory.

## What each class measures

| Class | Measures |
|-------|----------|
| `ParsingBenchmarks` | Lexing and parsing of a sample of the Enron and EUSES data sets, per formula, in A1 and in R1C1. The lexer is the baseline of each style. `ParseA1` and `ParseR1C1` use a factory that builds no tree, so they measure the lexer and the parser only. `ParseA1ToAst` adds the tree of *ClosedXML.Parser.Ast*. |
| `FormulaShapeBenchmarks` | The same lex and parse steps on single formulas of typical shapes: a cell, a lookup into a quoted sheet, an array constant, a structured reference and others. Use it to find the construct that costs the most. |
| `FormulaConverterBenchmarks` | `FormulaConverter.ToR1C1`, `FormulaConverter.ToA1` and `FormulaConverter.ModifyA1` with a sheet rename, per formula of the sample. |
| `ReferenceParserBenchmarks` | The `ReferenceParser` methods on typical references and names, and on text that is not a reference. |
| `NameUtilsBenchmarks` | `NameUtils.ShouldQuote` and `NameUtils.IsSheetNameValid` on sheet names. |

## The formula sample

The data sets are the ones the data set tests use, in *src/ClosedXML.Parser.Tests/data*. The build stores their absolute path in the benchmark assembly, because BenchmarkDotNet runs each benchmark from a directory of its own.

A sample has 10,000 formulas. The sample takes one formula from each of 10,000 equal slices of the data set. It skips a formula that the data set lists as a known failure, and a formula that does not parse in both A1 and R1C1. The R1C1 formulas are the A1 formulas converted at the cell R1000C100. A benchmark over the sample declares the sample size as its `OperationsPerInvoke`, so its results are per formula.

The Rolex lexer is internal. The benchmark assembly is signed with the key of the parser, and the parser gives it access to its internals.
