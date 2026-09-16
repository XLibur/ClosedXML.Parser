# Upgrading XLibur.ClosedXML.Parser

This guide tells you what to change in your code when you move to a new major version. For the full
list of changes in each version, see [CHANGELOG.md](CHANGELOG.md).

## Contents

- [From v4 to v5](#from-v4-to-v5)

## From v4 to v5

### What changed

Version 5 changes two names. It does not change any types, members or parser behaviour.

| | v4 | v5 |
|---|---|---|
| NuGet package | `XLibur.ClosedXML.Parser` | `XLibur.ClosedXML.Parser` (no change) |
| Assembly | `ClosedXML.Parser.dll` | `XLibur.ClosedXML.Parser.dll` |
| Namespace | `ClosedXML.Parser` | `XLibur.Parser` |
| Strong-name public key token | `1d5f7376574c51ec` | `1d5f7376574c51ec` (no change) |

The names changed because of [issue #59](https://github.com/XLibur/ClosedXML.Parser/issues/59). The
v4 assembly had the same name and key as the upstream `ClosedXML.Parser` assembly that ClosedXML
uses. .NET loads only one assembly for each name, so an application that referenced both ClosedXML
and XLibur loaded only one of the two parsers, and the other library failed at runtime. In v5 the
two parsers load side by side.

Version 5 is a breaking change for two reasons:

- **Source:** code that names the `ClosedXML.Parser` namespace does not compile.
- **Binary:** a library compiled against v4 looks for an assembly named `ClosedXML.Parser`. It
  cannot use v5 until it is compiled again.

### Before you start

Find out which of your dependencies use this package. Run this command in your solution directory:

```sh
dotnet list package --include-transitive | grep -i "XLibur.ClosedXML.Parser"
```

If a package, such as XLibur, brings in `XLibur.ClosedXML.Parser` 4.x, that package was compiled
against v4. Do not upgrade the parser until that package has a release that depends on 5.x. XLibur
0.600.0 and earlier depend on 4.x. See [Libraries compiled against v4](#libraries-compiled-against-v4).

### Step 1: Update the package reference

```xml
<PackageReference Include="XLibur.ClosedXML.Parser" Version="5.0.0" />
```

If you use Central Package Management, change the version in `Directory.Packages.props` instead.

### Step 2: Change the namespace

Replace `ClosedXML.Parser` with `XLibur.Parser` everywhere your code names the namespace:

| v4 | v5 |
|---|---|
| `using ClosedXML.Parser;` | `using XLibur.Parser;` |
| `using static ClosedXML.Parser.ReferenceStyle;` | `using static XLibur.Parser.ReferenceStyle;` |
| `global using ClosedXML.Parser;` | `global using XLibur.Parser;` |
| `@using ClosedXML.Parser` (Razor) | `@using XLibur.Parser` |
| `ClosedXML.Parser.ReferenceArea area;` | `XLibur.Parser.ReferenceArea area;` |
| `<see cref="ClosedXML.Parser.FormulaParser{TScalarValue, TNode, TContext}"/>` | `<see cref="XLibur.Parser.FormulaParser{TScalarValue, TNode, TContext}"/>` |

Type names and member names do not change. `FormulaParser`, `IAstFactory`, `ReferenceArea`,
`RowCol`, `FormulaModifier` and all other types keep their names.

To make the change in many files at once, you can run this command from your source directory:

```sh
grep -rlE --include='*.cs' --include='*.razor' --include='*.cshtml' 'ClosedXML\.Parser\b' . \
  | xargs perl -pi -e 's/(?<![\w.])ClosedXML\.Parser\b/XLibur.Parser/g'
```

The pattern does not match the package id `XLibur.ClosedXML.Parser`. Do a review of the diff before
you commit:

- The pattern also changes the assembly part of an assembly-qualified type name to the wrong value.
  `"ClosedXML.Parser.ReferenceArea, ClosedXML.Parser"` becomes
  `"XLibur.Parser.ReferenceArea, XLibur.Parser"`. Correct the assembly part to
  `XLibur.ClosedXML.Parser`. See [Step 3](#step-3-find-references-to-the-assembly-name).
- If your project also uses the upstream `ClosedXML.Parser` directly, put back the lines that refer
  to upstream types.
- If your own namespaces start with `ClosedXML.Parser` (for example `ClosedXML.Parser.Extensions`),
  put back those namespace names.

### Step 3: Find references to the assembly name

The compiler does not find text that names the assembly. Search your repository for
`ClosedXML.Parser` in these places, and change the assembly name to `XLibur.ClosedXML.Parser`:

- **Assembly-qualified type names**, for example
  `Type.GetType("ClosedXML.Parser.ReferenceArea, ClosedXML.Parser")`. Change both parts:
  `"XLibur.Parser.ReferenceArea, XLibur.ClosedXML.Parser"`.
- **Serialized type names**, for example a `$type` value in stored JSON. Data that you saved with v4
  keeps the old name. Migrate that data, or map the old name to the new type when you read it.
- **Trimming and AOT configuration**, for example `<TrimmerRootAssembly Include="ClosedXML.Parser" />`
  or `<assembly fullname="ClosedXML.Parser">` in an ILLink descriptor.
- **Code coverage filters**, for example `[ClosedXML.Parser]*` in a `.runsettings` file or a
  coverlet `Include` or `Exclude` property.
- **Build and deployment scripts** that copy, sign, instrument or obfuscate `ClosedXML.Parser.dll`.
- **Logs and monitoring rules** that match stack frames or logger names that start with
  `ClosedXML.Parser.`. Frames from this package now start with `XLibur.Parser.`.

### Step 4: Build and test

Build and run your tests. Then look at the output directory. It must contain
`XLibur.ClosedXML.Parser.dll`. It contains `ClosedXML.Parser.dll` only if your application also
uses upstream ClosedXML.

### Libraries compiled against v4

A library compiled against v4, such as XLibur 0.600.0, looks for an assembly named
`ClosedXML.Parser`. Version 5 does not supply that assembly, so the library fails when it first uses
the parser. It fails in one of two ways:

- If your application also references ClosedXML, .NET loads the upstream parser, and the library
  throws a `TypeLoadException`, for example
  `Could not load type 'ClosedXML.Parser.FormulaModifier' from assembly 'ClosedXML.Parser, Version=1.0.0.0'`.
- If no upstream parser is present, the library throws a `FileNotFoundException` for
  `ClosedXML.Parser, Version=4.0.0.0`.

To prevent these errors, keep the parser and the libraries that use it on the same major version:

- Upgrade the parser to v5 together with a release of the library that depends on 5.x.
- Until that release is available, stay on v4. Do not add a direct reference to 5.0.0, because
  NuGet then gives the v5 package to the library too.

If you maintain a library that depends on this package, do Steps 1 to 4 for the library. Release
the library as a version that depends on 5.x. Tell your users that they cannot use it with the v4
parser.

### Using ClosedXML and XLibur in one application

Version 5 is the first version that works in the same application as upstream ClosedXML. To use
both, reference ClosedXML and a release of XLibur that depends on `XLibur.ClosedXML.Parser` 5.x.

If your code uses both parsers directly, the namespaces are different, so you do not need
`extern alias`. Remove any `extern alias` and `Aliases` metadata that you added for v4.

If a file imports both namespaces, a type name that exists in both is ambiguous (error `CS0104`).
Use an alias, or write the full name:

```csharp
using UpstreamReferenceParser = ClosedXML.Parser.ReferenceParser;
using XLibur.Parser;

var upstream = UpstreamReferenceParser.ParseA1("B3");
var fork = ReferenceParser.ParseA1("B3");
```

### Troubleshooting

| Error | Cause | Fix |
|---|---|---|
| `CS0246` or `CS0234`: the type or namespace name `ClosedXML` or `Parser` could not be found | Code still names the v4 namespace. | Do [Step 2](#step-2-change-the-namespace). |
| `CS0104`: a type name is ambiguous between `ClosedXML.Parser` and `XLibur.Parser` | A file imports both namespaces. | Use an alias or the full type name. |
| `TypeLoadException` from assembly `ClosedXML.Parser, Version=1.0.0.0` | A library compiled against v4 loaded the upstream parser. | See [Libraries compiled against v4](#libraries-compiled-against-v4). |
| `FileNotFoundException` for `ClosedXML.Parser, Version=4.0.0.0` | A library compiled against v4 cannot find its parser. | See [Libraries compiled against v4](#libraries-compiled-against-v4). |
| `TypeLoadException: Method 'SheetErrorNode' in type 'AstFactory' from assembly 'ClosedXML'` | The application still uses v4 together with ClosedXML. | Upgrade to v5, together with a release of XLibur that depends on 5.x. |
