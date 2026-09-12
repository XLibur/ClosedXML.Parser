# Visualizer design

The visualizer is a web page that parses an Excel formula and draws its abstract syntax tree.
It runs the parser in the browser with Blazor WebAssembly, so it needs no server. GitHub Pages
hosts it at <https://xlibur.github.io/ClosedXML.Parser/>.

It replaced the old visualizer: a static page, `src/ClosedXML.Parser.Web`, that sent each
formula to an Azure Function, `src/ClosedXML.Parser.Function`, for the parse. Both were removed
after the Pages site worked.

## Goals

- Parse a formula as the user types and show the tree at once.
- Show the parser that is on `develop`, not the last NuGet release.
- Make a wrong parse easy to report: a link that reopens the same formula, and a Mermaid text
  that GitHub draws in an issue.
- Need no server, no Node toolchain for a normal build, and no manual deploy step.

## Project layout

| Path | Purpose |
|---|---|
| `src/ClosedXML.Parser.Visualizer` | The standalone Blazor WebAssembly app (net10.0). |
| `src/ClosedXML.Parser.Visualizer.Tests` | xunit and bUnit tests for the app (net10.0). |
| `tools/tailwind/generate-css.sh` | Regenerates or checks the committed Tailwind CSS. |
| `.github/workflows/pages.yml` | Publishes the app to GitHub Pages. |

Both projects are in `src/ClosedXML.Parser.sln`, so every workflow that builds the solution also
builds the app. The app sets `IsPackable=false`. The release workflows pack only
`ClosedXML.Parser.csproj`, so the app never goes to NuGet.

The app references `ClosedXML.Parser.Ast` with a project reference. That project holds the AST
records and the `F` factory, and it references the parser. The Ast project is not in the NuGet
package, so a package reference is not possible.

## Parsing

The app calls `FormulaParser<ScalarValue, AstNode, Ctx>.CellFormulaA1` or `CellFormulaR1C1`. It
uses the node types, `GetTypeString()` and `GetDisplayString(style)` from the Ast project, so the
labels are the same as the labels of the old Function.

The `F` factory ignores the `SymbolRange` of each node. The app wraps `F` in a factory that
records the range of each node it creates. The records have value equality, so the lookup uses
reference equality. The Ast records and their tests do not change.

`Nested` returns its inner node, so parentheses do not show in the tree. The inner node keeps its
own range; the range of the parentheses is not recorded.

`BinaryNode` displays `NotEqual` as `<>`, the Excel operator. It was `!=` before.

### Parse errors

`ParsingException` has a message but no position property. Most messages contain the position
("at position N", "from position N", "at index N", "at char N"). The app reads the position from
the message and marks that character. When a message has no position, the app shows only the
message. On an error the last good tree stays on the page, dimmed.

## User interface

The page has one screen. It is the root component, so the app has no router. The page reads the
URL query when it starts.

1. A formula input. The default formula is `SUM(B5,2)`. The app parses 150 ms after the last
   keystroke.
2. An **A1 | R1C1** switch.
3. Example chips: implicit intersection, a structured reference, a 3D reference, an array and an
   R1C1 formula.
4. A monospace copy of the formula under the input. It highlights the text of the selected node
   and marks the position of a parse error.
5. A details line for the selected node: type, label and character range.
6. The tree diagram, with a **Copy Mermaid** button.
7. A legend of the node families.
8. A footer with the version of the parser assembly, linked to its commit.

The URL holds the state: `?f=<formula>&style=R1C1`. A1 is the default and is not written to the
URL. The app updates the URL with a replace, so typing does not add history entries.

The app uses the invariant culture, so a number shows the same in every browser.

### Node families

There are about 25 node types. Each node shows its exact type, and a colour shows its family.

| Family | Node types |
|---|---|
| Function | Function, ExternalFunction, CellFunction |
| Reference | Reference, SheetReference, BangReference, Reference3D, ExternalSheetReference, ExternalReference3D |
| Structured reference | StructureReference, ExternalStructureReference |
| Name | Name, SheetName, BangName, ExternalName, ExternalSheetName, DynamicDataExchange, ExternalDynamicDataExchange |
| Value | Number, Text, Logical, Blank, Array |
| Error | Error |
| Operator | Binary, Unary |

## Rendering

The diagram is a Mermaid `flowchart TD`. A small JS module and a Razor component wrap Mermaid.
The app does not use Blazorade.Mermaid, because that package re-renders on every parent render
(the diagram flickers), writes the definition through `innerHTML` (a formula with `<` is
damaged), hides errors, and cannot send node clicks to .NET.

- Mermaid 11 is loaded from jsdelivr as an ES module, pinned to an exact version. The first
  diagram loads it. The version is one constant in the JS module. Dependabot does not see that
  constant, so update it by hand.
- Mermaid runs with `securityLevel: "strict"` and `suppressErrorRendering: true`. A render error
  comes back to .NET as a message.
- The component renders only when the Mermaid text changes. Each render has a sequence number,
  and the module drops the result of an older render. The new SVG replaces the old one in one
  step.
- A click listener on the diagram sends the id of the clicked node to .NET.

### Mermaid text

- Node ids are synthetic (`n0`, `n1`, ...). The formula text is never an id.
- A label has two lines: the display string and the type in brackets.
- Mermaid reads `#name;` as an entity. The builder encodes `#` first, then `"`, `<`, `>`, `&` and
  the backtick. A line break in the formula becomes a space.
- Each node has a class for its family, for example `fam-function`. The page CSS sets the colours
  with custom properties, so a change between light and dark mode needs no new render.
- **Copy Mermaid** adds `classDef` lines with the light colours, so the pasted diagram has colours
  on GitHub.

## Styling

The page uses Tailwind CSS v4. The generated `wwwroot/css/app.css` is committed, not minified.
A normal `dotnet build` does not run Tailwind and needs no Tailwind binary.

`tools/tailwind/generate-css.sh` regenerates the CSS. It downloads the pinned standalone CLI
(v4.3.3) into the ignored `.tools/` folder and verifies its SHA-256 against a value stored in
the script.

```text
tools/tailwind/generate-css.sh           write app.css
tools/tailwind/generate-css.sh --watch   write app.css again after each change
tools/tailwind/generate-css.sh --check   change nothing, fail if app.css is not current
```

The Tailwind input uses `source(none)` with explicit `@source` globs for `.razor` files and
`wwwroot/index.html`. Thus the output is the same on each machine. Put Tailwind classes only in
those files, not in `.cs` files.

Dark mode follows `prefers-color-scheme`.

## Tests

`ClosedXML.Parser.Visualizer.Tests` covers:

- the Mermaid text and the label escaping, with formulas such as `"a"&"b"`, `'Sheet 1'!A1`,
  `{1,2;3,4}`, `Table[[#Headers],[Col]]` and `#REF!`;
- the family of each node type;
- the recorded range of each node;
- the error position read from each kind of `ParsingException` message;
- the page, rendered with bUnit: the URL state, the example chips, the error display and node
  selection.

## Continuous integration

`build-and-test.yml`:

- runs the app tests after the parser tests;
- has a `tailwind-css` job that runs `tools/tailwind/generate-css.sh --check`.

`pages.yml`:

- runs on a push to `develop`, on a pull request to `develop` and on a manual run. A path filter
  limits it to the parser, the Ast project, the app and the workflow.
- publishes the app in Release. Trimming runs only at publish, so the pull request run finds a
  trim warning before it gets to `develop`. A trim warning in the app, the parser or the Ast
  project is an error, because the parser reads the fields of `Token` by reflection. The Blazor
  framework assemblies have trim warnings that Blazor expects. The trimmer reports each of those
  assemblies as one `IL2104` warning, and the app ignores `IL2104`.
- sets `<base href>` to the repository path and adds `.nojekyll`, so Pages serves `_framework/`.
- deploys with `actions/upload-pages-artifact` and `actions/deploy-pages` into the `github-pages`
  environment. A pull request run builds but does not deploy.

The repository setting **Settings > Pages > Source** must be **GitHub Actions**.

## Local development

```text
dotnet run --project src/ClosedXML.Parser.Visualizer      the app at http://localhost:5238
tools/tailwind/generate-css.sh --watch                    regenerate app.css while you edit
```

After a change to `wwwroot/index.html`, delete
`src/ClosedXML.Parser.Visualizer/obj/<configuration>/net10.0/staticwebassets/htmlassetplaceholders`
before a local publish. The SDK (10.0.400) caches the processed `index.html` and does not see the
change, so the publish copies `index.html` with its `#[.{fingerprint}]` placeholder, and the page
does not load. `dotnet clean` does not delete the cache. The Pages workflow always builds from a
clean checkout, so it is not affected.

## Out of scope

- Mermaid 12, which changes the default layout and look.
- Hover highlighting, SVG download and a custom domain.
- A self-hosted copy of Mermaid.
