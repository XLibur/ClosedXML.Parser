# ClosedParser

> **Fork notice**
>
> This repository is a fork of [ClosedXML.Parser](https://github.com/ClosedXML/ClosedXML.Parser), Copyright (c) 2023, Jan Havlíček.
>
> The purpose of this fork is to bundle fixes and changes required by the [XLibur](https://github.com/XLibur/XLibur) library. It is published as the `XLibur.ClosedXML.Parser` NuGet package.
>
> Where appropriate, fixes will be contributed back to upstream [ClosedXML.Parser](https://github.com/ClosedXML/ClosedXML.Parser). Some changes are intentionally specific to XLibur and may remain in this fork, particularly where they do not align with upstream goals, such as dropping `netstandard2.0` support.

ClosedParser parses Excel formulas, in the form stored in OOXML workbooks, into an abstract syntax tree (AST) suitable for evaluation and transformation.

The official source for Excel formula grammar is [MS-XLSX](https://learn.microsoft.com/en-us/openspecs/office_standards/ms-xlsx/2c5dee00-eff2-4b22-92b6-0738acd4475e), section 2.2.2, **Formulas**.

The grammar published in the specification cannot be used directly by a parser generator because it contains ambiguities and does not fully express operator precedence. A copy of the `v20221115` grammar is included under `docs/grammar`, together with an annotated version containing notes made during the original parser development.

## Installation and usage

Install the `XLibur.ClosedXML.Parser` NuGet package.

The package targets .NET 8 and requires .NET 8 or later. It has no external package dependencies.

### 1. Implement an AST factory

Implement:

```csharp
IAstFactory<TScalarValue, TNode, TContext>
```

The parser calls the factory for each AST node and provides the range of source formula text from which that node was parsed.

`src/ClosedXML.Parser.Ast/AstFactory.cs` provides an example implementation. The test suite and visualizer also use AST factories.

### 2. Parse a formula

For A1 notation:

```csharp
FormulaParser<TScalarValue, TNode, TContext>
    .CellFormulaA1("SUM(A1, 2)", context, factory);
```

For R1C1 notation:

```csharp
FormulaParser<TScalarValue, TNode, TContext>
    .CellFormulaR1C1("SUM(R1C1, 2)", context, factory);
```

The supplied context is passed to each applicable method on the AST factory.

A formula that does not satisfy the grammar throws a `ParsingException`.

## Visualizer

An interactive AST visualizer is available at:

**https://xlibur.github.io/ClosedXML.Parser/**

It runs the parser from the `develop` branch directly in the browser using Blazor WebAssembly.

See [docs/visualizer-design.md](docs/visualizer-design.md) for implementation details.

![AST visualizer](assets/visualizer.png)

## Goals

ClosedParser is designed around the requirements of ClosedXML and XLibur.

* **Performance**
  Excel workbooks can contain very large numbers of formulas, so parsing should be fast and avoid unnecessary allocations.

* **Evaluation-oriented AST**
  The parser produces an abstract syntax tree rather than a concrete syntax tree because its primary purpose is formula evaluation.

  Formula transformation is also supported. `FormulaConverter` converts formulas between A1 and R1C1 notation, while `FormulaModifier` can rename or remove sheets, tables, and functions, shift references, and preserve unchanged portions of the original formula.

* **Multiple formula contexts**
  Formulas are primarily used in worksheet cells, but Excel also uses formula-like expressions in features with different grammar rules, such as sparklines and data validation.

* **A1 and R1C1 notation**
  Both reference styles are supported.

  For example:

  ```text
  SUM(R5)
  ```

  can mean the sum of cell `R5` in A1 notation, but the sum of row 5 in R1C1 notation.

## Limitations

The primary goal is to parse formulas as they are **stored in OOXML files**, rather than exactly as Excel displays them to users.

The formula shown in Excel's UI is not always identical to the formula stored in the workbook.

For example:

* The `IFS` function is stored as `_xlfn.IFS`, while Excel displays it as `IFS`.
* In structured references, Excel may display `@` to indicate the current row, while the stored representation uses the `[#This Row]` specifier.

As a consequence:

* External references are accepted only using an external workbook index, for example:

  ```text
  [5]Sheet1!A1
  ```

  A UI-style reference such as:

  ```text
  [Book1.xlsx]Sheet1!A1
  ```

  is not supported.

* A quoted item following an external workbook index, such as:

  ```text
  [1]!'Some name in external wb'
  ```

  is interpreted as a Dynamic Data Exchange (DDE) item because that is how such references are represented in workbook files.

  An external defined name must therefore be unquoted:

  ```text
  [1]!SomeName
  ```

* Formula syntaxes used by other spreadsheet implementations may differ from OOXML formula syntax and are outside the scope of this project.

The parser also does not currently support invoking the result of a function expression, for example:

```text
LAMBDA(x,x+1)(2)
```

# Architecture

## Grammar

The ANTLR4 grammars under `src/ClosedXML.ANTLR` are the source of truth:

* `FormulaLexer.g4` defines lexical rules.
* `FormulaParser.g4` defines parser rules.

ANTLR4 is one of the few actively maintained parser generators with strong C# support.

ClosedParser does not use the ANTLR runtime in the published package.

Instead:

* `FormulaLexer.g4` is converted into grammars consumed by the Rolex DFA lexer.
* `FormulaParser.g4` serves as the basis for the hand-written recursive-descent parser.
* ANTLR is retained in the test suite to verify that the production parser agrees with the authoritative grammar.

This provides the maintainability of an explicit ANTLR grammar while avoiding the runtime performance cost of using the generated ANTLR parser directly.

## Parser

The production parser is a recursive-descent parser generated and maintained around the rules defined in `FormulaParser.g4`.

Upstream benchmarks measured parsing of the Enron dataset at approximately:

* **ANTLR parser:** 8 seconds
* **Recursive-descent parser:** 700 ms

The recursive-descent implementation also allows tighter control over allocations and the AST representation produced by the parser.

## Rolex lexer

[Rolex](https://github.com/codewitch-honey-crisis/Rolex) is a DFA-based lexer generator released under the MIT license.

See [Rolex: Unicode Enabled Lexer Generator in C#](https://www.codeproject.com/Articles/5257489/Rolex-Unicode-Enabled-Lexer-Generator-in-Csharp).

ANTLR remains the source of truth for the lexer grammar. `FormulaLexer.g4` is converted into a Rolex-compatible grammar, which Rolex then compiles into DFA tables used by the production lexer.

This introduces some complexity into the generation process, but upstream benchmarks found the resulting lexer to be approximately twice as fast as the ANTLR lexer:

* **Rolex:** approximately 1.9 μs per formula
* **ANTLR:** approximately 3.676 μs per formula

## Why not use XLParser?

[ClosedXML](https://github.com/ClosedXML/ClosedXML) previously used [XLParser](https://github.com/spreadsheetlab/XLParser), converting its concrete syntax tree (CST) into an abstract syntax tree (AST).

It was later replaced by [ClosedXML.Parser](https://github.com/ClosedXML/ClosedXML.Parser).

The main reasons for replacing XLParser, based on measurements made by the ClosedXML project, were:

### Performance

XLParser's grammar makes extensive use of regular expressions. These can be relatively slow and allocate additional memory, particularly on .NET Framework.

Parsing the Enron dataset with XLParser took approximately:

* **47 seconds on .NET Framework**
* **16 seconds on .NET 7**

The significant difference is largely due to improvements made to the .NET regular expression engine.

Irony, on which XLParser is built, also needs to determine the set of possible tokens following each parsed token. This introduces additional overhead even when `prefix` hints are used.

For comparison, upstream measurements for ANTLR were approximately:

* **ANTLR lexing:** 3.2 seconds
* **ANTLR lexing and parsing:** 11 seconds

The recursive-descent parser completes the same workload in approximately **700 ms**, while also avoiding much of the allocation overhead.

### AST generation

XLParser primarily produces a concrete syntax tree.

ClosedXML requires an abstract syntax tree for formula evaluation, meaning XLParser's CST must first be transformed into another representation.

Irony is not particularly well suited to this use case.

### Excel syntax support

XLParser also lacks support for some Excel syntax required by ClosedXML, including:

* Lambda expressions
* R1C1-style references

# Performance

## Current benchmarks

Dataset tests report the total parsing time.

The following results were measured in Release mode using .NET 10 on an AMD Ryzen 9 5950X on 2026-09-12, using the AST factory from the test suite.

| Dataset | Formulas |  Total time | Time per formula |
| ------- | -------: | ----------: | ---------------: |
| Enron   |  946,320 |   1.6–1.8 s |       1.7–1.9 μs |
| EUSES   |   89,295 | 0.12–0.15 s |       1.4–1.7 μs |

Upstream previously measured approximately **1.942 μs per formula** for the Enron dataset.

To run the dataset benchmarks:

```shell
dotnet test src/ClosedXML.Parser.Tests \
  -c Release \
  -f net10.0 \
  --filter "FullyQualifiedName~DataSetTests" \
  --logger "console;verbosity=detailed"
```

At approximately 2 μs per formula, formula parsing is unlikely to be a significant bottleneck for typical workbook processing.

# Development

## Testing strategy

The test suite is organised around lexer tokens, parser rules, real-world datasets, and compatibility with the ANTLR grammar.

### Lexer tests

Each token containing data that must be extracted into an AST node has a corresponding test class under the `Lexers` directory.

For example, an `A1_REFERENCE` token containing `C5` must be translated into row 5, column 3.

Test classes follow the naming convention:

```text
{TokenPascalName}TokenTests.cs
```

### Parser rule tests

Each parser rule has a corresponding test class under the `Rules` directory.

Tests should cover the supported combinations of the rule and verify the resulting AST nodes.

### Dataset tests

`DataSetTests.cs` parses formulas from the:

* Enron dataset
* EUSES dataset
* Contributed datasets

Each dataset is stored as a directory under `data`, with formulas contained in the single-column file:

```text
formulas.csv
```

A formula listed in the dataset's `known-fails.csv` file must fail to parse. Every other formula must parse successfully.

These tests verify whether parsing succeeds or fails; they do not validate the resulting AST.

### ANTLR compatibility

`AntlrCompatibilityTests.cs` verifies that the Rolex lexer and ANTLR lexer produce equivalent tokens for the test datasets.

This helps ensure that the generated production lexer remains consistent with `FormulaLexer.g4`.

### Visualizer

`ClosedXML.Parser.Visualizer.Tests` contains tests for the browser-based AST visualizer.

## Debugging the grammar

The [vscode-antlr4](https://github.com/mike-lischke/vscode-antlr4/blob/master/doc/grammar-debugging.md) extension can be used to debug the ANTLR grammar.

## Generating the lexer

Lexer generation has two stages:

1. Convert the ANTLR lexer grammar into Rolex grammars.
2. Generate DFA tables from those Rolex grammars.

### Generate the Rolex grammars

The converter under `tools/Antlr2Rolex` generates both Rolex grammars from `FormulaLexer.g4`.

Do not edit `.rl` files manually.

`RolexGrammarConverterTests` regenerates both files and fails if the generated output differs from the committed files.

Generate the A1 grammar with:

```shell
dotnet run --project tools/Antlr2Rolex -- \
  src/ClosedXML.ANTLR/FormulaLexer.g4 \
  --style A1 \
  --output src/ClosedXML.Parser/Rolex/LexerA1.rl
```

Generate the R1C1 grammar with:

```shell
dotnet run --project tools/Antlr2Rolex -- \
  src/ClosedXML.ANTLR/FormulaLexer.g4 \
  --style R1C1 \
  --output src/ClosedXML.Parser/Rolex/LexerR1C1.rl
```

For R1C1 notation, the converter uses the **Local R1C1 References** section of the grammar instead of **Local A1 References**.

Any alternative referring to a rule defined only by the A1 section is removed and a warning is emitted.

Currently, this applies to the following `SHEET_RANGE` alternative:

```text
A1_RELATIVE_COLUMN ':' SHEET_NAME
```

The converter intentionally supports only the subset of ANTLR syntax currently used by the grammar. Unsupported syntax is reported together with its source line.

### Generate the DFA tables

Run:

```shell
tools/rolex/generate-dfa-tables.sh
```

This regenerates:

```text
RolexA1Dfa.cs
RolexR1C1Dfa.cs
```

The tables are generated from the Rolex grammars using the vendored Rolex build under:

```text
tools/rolex/91a2d6d
```

This is currently the only known build whose output exactly matches the committed DFA tables.

Rolex is a .NET Framework executable, so the script should be run from Git Bash on Windows.

To verify generated files without modifying them:

```shell
tools/rolex/generate-dfa-tables.sh --check
```

The `rolex-tables` CI job performs this check. If a Rolex grammar is changed without regenerating and committing its corresponding DFA table, the build will fail.

See `tools/rolex/README.md` for details about how the vendored Rolex build was produced.

# Resources

* [MS-XLSX formula specification](https://learn.microsoft.com/en-us/openspecs/office_standards/ms-xlsx/2c5dee00-eff2-4b22-92b6-0738acd4475e)
* [ClosedXML](https://github.com/ClosedXML/ClosedXML)
* [ClosedXML.Parser](https://github.com/ClosedXML/ClosedXML.Parser)
* [XLParser](https://github.com/spreadsheetlab/XLParser)
* [Simplified XLParser grammar](https://github.com/spreadsheetlab/XLParser/blob/master/doc/ebnf.pdf)
* [XLParser tokens](https://github.com/spreadsheetlab/XLParser/blob/master/doc/tokens.pdf)
* [Rolex](https://github.com/codewitch-honey-crisis/Rolex)
* [Getting Started With ANTLR in C#](https://tomassetti.me/getting-started-with-antlr-in-csharp/)
