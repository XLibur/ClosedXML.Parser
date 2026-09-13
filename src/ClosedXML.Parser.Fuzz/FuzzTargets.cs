using System.Text;

namespace ClosedXML.Parser.Fuzz;

/// <summary>The targets a fuzzing run can drive, and what each of them counts as acceptable.</summary>
internal static class FuzzTargets
{
    public const string FormulaA1 = "formula-a1";
    public const string FormulaR1C1 = "formula-r1c1";
    public const string Modify = "modify";
    public const string Convert = "convert";
    public const string Reference = "reference";

    public static readonly string[] All = [FormulaA1, FormulaR1C1, Modify, Convert, Reference];

    public const string Default = FormulaA1;

    /// <summary>
    /// The cell a formula is treated as sitting in. Away from <c>A1</c> on purpose, so relative
    /// references get offsets of both signs when they are converted to R1C1 and back. The same
    /// anchor the data set round-trip test uses, so a finding here can be pasted into that test.
    /// </summary>
    private const int AnchorRow = 1000;

    private const int AnchorCol = 100;

    private const string AnchorSheet = "Sheet1";

    // Created on first use rather than in a static initialiser, and that is a constraint rather
    // than a style choice. All three live in the assemblies SharpFuzz rewrites, and rewritten code
    // reports coverage into a trace buffer that Fuzzer.LibFuzzer.Run is what allocates. A static
    // initialiser on this class would run when Program.Main reads FuzzTargets.All, before Run --
    // the process would then die during startup, and libFuzzer reports that only as an exit code
    // before waiting forever for a target that is already gone. The string fields above are safe
    // there precisely because a string array is not instrumented.
    private static Ctx? s_context;

    private static F? s_factory;

    private static FormulaModifier? s_identity;

    private static Ctx Context => s_context ??= new Ctx();

    private static F Factory => s_factory ??= new F();

    /// <summary>Changes nothing. Every property of the <see cref="Modify"/> target rests on that.</summary>
    private static FormulaModifier Identity => s_identity ??= new FormulaModifier();

    public static bool IsKnown(string target)
    {
        return Array.Exists(All, t => t.Equals(target, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Run one input through a target. The returned string describes what happened when nothing
    /// went wrong; fuzzing discards it, replay prints it.
    ///
    /// <para>
    /// It exists because "no failure" is ambiguous in a way that matters. Text the parser
    /// <em>refused</em> and text it <em>parsed and rendered back</em> both leave a target without
    /// throwing, and they say opposite things about whether the fuzzer is reaching the library at
    /// all. A corpus that only ever produces "rejected" is a corpus stuck at the lexer.
    /// </para>
    /// </summary>
    public static string Run(string target, ReadOnlySpan<byte> data)
    {
        switch (target.ToLowerInvariant())
        {
            case FormulaA1:
                return RunFormula(FormulaA1, ReferenceStyle.A1, data);

            case FormulaR1C1:
                return RunFormula(FormulaR1C1, ReferenceStyle.R1C1, data);

            case Modify:
                return RunModify(data);

            case Convert:
                return RunConvert(data);

            case Reference:
                return RunReference(data);

            default:
                throw new ArgumentException($"Unknown fuzz target '{target}'.", nameof(target));
        }
    }

    /// <summary>
    /// Parse the input as a cell formula and check that every reference and name in the tree
    /// survives being written back out.
    ///
    /// <para>
    /// Parsing alone would only catch a hard crash: a parser that reads <c>'a:b'!A1</c> as the
    /// wrong thing returns an AST just as happily as one that reads it right. The display string is
    /// the library's own claim about what it read, so re-parsing it and demanding the same node
    /// back is a property that can see a wrong answer.
    /// </para>
    /// </summary>
    private static string RunFormula(string target, ReferenceStyle style, ReadOnlySpan<byte> data)
    {
        var formula = Decode(data);

        AstNode root;
        try
        {
            root = Parse(formula, style);
        }
        catch (Exception e) when (Oracle.IsRejectionDuringParse(e))
        {
            return "rejected";
        }

        var checkedNodes = CheckDisplayRoundTrip(target, root, style);
        return checkedNodes > 0 ? $"parsed, {checkedNodes} node(s) round-tripped" : "parsed";
    }

    /// <summary>
    /// Walk the tree and round-trip every node whose display string is a complete expression.
    /// Returns how many were checked, so replay can say whether a corpus reaches the writers at all.
    /// </summary>
    private static int CheckDisplayRoundTrip(string target, AstNode root, ReferenceStyle style)
    {
        var checkedNodes = 0;
        var pending = new Stack<AstNode>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var node = pending.Pop();
            foreach (var child in node.Children)
                pending.Push(child);

            if (!IsRoundTrippable(node))
                continue;

            // Writing the display string is the process phase: the parser has already claimed to
            // understand this node, so almost nothing it throws here is excusable.
            string text;
            try
            {
                text = node.GetDisplayString(style);
            }
            catch (Exception e) when (Oracle.IsToleratedDuringProcess(e))
            {
                if (Oracle.ShouldReport(e))
                    Oracle.Report(target, "display", e);

                continue;
            }

            // Re-reading what the library just wrote is the reload phase, where nothing at all is
            // tolerated. A ParsingException here says the writer produced text the parser cannot
            // read -- the defect no check on the first parse can see.
            AstNode reparsed;
            try
            {
                reparsed = Parse(text, style);
            }
            catch (Exception e) when (!Oracle.IsToleratedDuringReparse(e))
            {
                throw new FuzzAssertionException(
                    $"A {node.GetType().Name} was written as '{text}', which does not parse in {style}.", e);
            }

            if (!Equals(reparsed, node))
            {
                throw new FuzzAssertionException(
                    $"A {node.GetType().Name} was written as '{text}', which parses back as " +
                    $"{Describe(reparsed)} rather than {Describe(node)}.");
            }

            checkedNodes++;
        }

        return checkedNodes;
    }

    /// <summary>
    /// Whether a node's display string is a complete expression that must parse back to the node.
    ///
    /// <para>
    /// The list is an allowlist, not a denylist, because most display strings are labels for a tree
    /// view rather than formula text: a <c>BinaryNode</c> renders as <c>"union"</c>, a
    /// <c>UnaryNode</c> as <c>"Implicit intersection"</c>, a <c>FunctionNode</c> as its bare name
    /// without arguments. An <c>ArrayNode</c> looks like a complete expression and is not one —
    /// its elements render through <c>ScalarValue</c>, which writes text unquoted, so
    /// <c>{"a,b"}</c> comes back as two elements.
    /// </para>
    ///
    /// <para>
    /// What is left is exactly the part of the language where the writing is hard: sheet prefixes,
    /// quoting, book indices and structured-reference braces.
    /// </para>
    /// </summary>
    private static bool IsRoundTrippable(AstNode node)
    {
        return node is ReferenceNode
            or SheetReferenceNode
            or BangReferenceNode
            or Reference3DNode
            or ExternalSheetReferenceNode
            or ExternalReference3DNode
            or SheetErrorNode
            or StructureReferenceNode
            or ExternalStructureReferenceNode
            or NameNode
            or SheetNameNode
            or BangNameNode
            or ExternalNameNode
            or ExternalSheetNameNode
            or DynamicDataExchangeNode
            or ExternalDynamicDataExchangeNode;
    }

    /// <summary>
    /// Apply a modifier that changes nothing, and hold the library to what it promises about that.
    ///
    /// <para>
    /// A modification writes a part of a formula again only when the answer it gets differs from
    /// what it asked about; every other part keeps its own text, character for character. So an
    /// identity modification is the identity function on formula text, with one documented
    /// exception: a swallowed ref error (<c>#REF!A1</c>) is a form Excel cannot parse, and the
    /// library normalises it to <c>#REF!</c> even though nothing changed it. Inputs carrying
    /// <c>#REF!</c> are therefore exempted from the verbatim property, and held to idempotence
    /// instead.
    /// </para>
    /// </summary>
    private static string RunModify(ReadOnlySpan<byte> data)
    {
        var formula = Decode(data);

        string once;
        try
        {
            once = FormulaConverter.ModifyA1(formula, AnchorSheet, AnchorRow, AnchorCol, Identity);
        }
        catch (Exception e) when (Oracle.IsRejectionDuringParse(e))
        {
            return "rejected";
        }

        // Property 1 -- verbatim. Compared ordinally: a modification copies text, so a difference
        // in case, in whitespace or in the quoting of a sheet name is exactly what this must catch.
        var carriesRefError = formula.IndexOf("#REF!", StringComparison.OrdinalIgnoreCase) >= 0;
        if (!carriesRefError && !string.Equals(once, formula, StringComparison.Ordinal))
        {
            throw new FuzzAssertionException(
                $"An identity modification of '{formula}' gave '{once}', and the formula carries no ref error.");
        }

        // Property 2 -- idempotence, which holds even for the swallowed ref error: normalising it
        // once is the whole of the change. Re-modifying is the reload phase; the library wrote this
        // text, so a refusal to read it back is a finding rather than a rejection.
        string twice;
        try
        {
            twice = FormulaConverter.ModifyA1(once, AnchorSheet, AnchorRow, AnchorCol, Identity);
        }
        catch (Exception e) when (!Oracle.IsToleratedDuringReparse(e))
        {
            throw new FuzzAssertionException(
                $"An identity modification of '{formula}' gave '{once}', which cannot be modified again.", e);
        }

        if (!string.Equals(twice, once, StringComparison.Ordinal))
        {
            throw new FuzzAssertionException(
                $"An identity modification of '{formula}' gave '{once}', and of that '{twice}'.");
        }

        return carriesRefError ? "modified (ref error normalised)" : "modified";
    }

    /// <summary>
    /// Convert A1 to R1C1 and back, and require that both outputs are formulas.
    ///
    /// <para>
    /// The conversion is not an equality property: a reference that leaves the sheet when it is
    /// resolved against the anchor comes back as <c>#REF!</c>, quite correctly, so the A1 form need
    /// not match what went in. What the library cannot do is emit text it cannot itself read. Each
    /// output is fed back to the parser in its own style, and a refusal there is a finding —
    /// by then the text is the library's own.
    /// </para>
    /// </summary>
    private static string RunConvert(ReadOnlySpan<byte> data)
    {
        var formula = Decode(data);

        string r1c1;
        try
        {
            r1c1 = FormulaConverter.ToR1C1(formula, AnchorRow, AnchorCol);
        }
        catch (Exception e) when (Oracle.IsRejectionDuringParse(e))
        {
            return "rejected";
        }

        try
        {
            _ = Parse(r1c1, ReferenceStyle.R1C1);
        }
        catch (Exception e) when (!Oracle.IsToleratedDuringReparse(e))
        {
            throw new FuzzAssertionException(
                $"'{formula}' converted to R1C1 as '{r1c1}', which does not parse as an R1C1 formula.", e);
        }

        string a1;
        try
        {
            a1 = FormulaConverter.ToA1(r1c1, AnchorRow, AnchorCol);
        }
        catch (Exception e) when (!Oracle.IsToleratedDuringReparse(e))
        {
            throw new FuzzAssertionException(
                $"'{formula}' converted to R1C1 as '{r1c1}', which cannot be converted back to A1.", e);
        }

        try
        {
            _ = Parse(a1, ReferenceStyle.A1);
        }
        catch (Exception e) when (!Oracle.IsToleratedDuringReparse(e))
        {
            throw new FuzzAssertionException(
                $"'{formula}' round-tripped through R1C1 as '{a1}', which does not parse as an A1 formula.", e);
        }

        return string.Equals(a1, formula, StringComparison.Ordinal) ? "converted, unchanged" : "converted";
    }

    /// <summary>
    /// Check what the reference parsers <em>claim</em>, not merely that they return.
    ///
    /// <para>
    /// <see cref="ReferenceParser"/> is six entry points onto one lexer, and they overlap: the
    /// two-output <c>TryParseA1</c> is documented as accepting whatever either the local or the
    /// sheet-qualified parser accepts. Calling them and discarding the results detects a hard crash
    /// and nothing else; comparing them against each other is what can see one of them answering
    /// the wrong question.
    /// </para>
    /// </summary>
    private static string RunReference(ReadOnlySpan<byte> data)
    {
        var text = Decode(data);

        var isLocal = ReferenceParser.TryParseA1(text, out var localArea);
        var isEither = ReferenceParser.TryParseA1(text, out var eitherSheet, out var eitherArea);
        var isSheet = ReferenceParser.TryParseSheetA1(text, out var sheetName, out var sheetArea);
        var isR1C1 = ReferenceParser.TryParseR1C1(text, out var r1c1Area);
        var isName = ReferenceParser.TryParseName(text, out var nameSheet, out var name);
        var isSheetName = ReferenceParser.TryParseSheetName(text, out var qualifiedSheet, out var qualifiedName);

        // Property 1 -- a local reference is a reference, read the same way by both overloads.
        if (isLocal && !(isEither && eitherSheet is null && eitherArea == localArea))
        {
            throw new FuzzAssertionException(
                $"'{text}' parses as the local reference {localArea}, but the combined overload " +
                $"{(isEither ? $"reads it as {eitherSheet ?? "<local>"} {eitherArea}" : "rejects it")}.");
        }

        // Property 2 -- and so is a sheet-qualified one.
        if (isSheet && !(isEither && eitherSheet == sheetName && eitherArea == sheetArea))
        {
            throw new FuzzAssertionException(
                $"'{text}' parses as {sheetName}!{sheetArea}, but the combined overload " +
                $"{(isEither ? $"reads it as {eitherSheet ?? "<local>"} {eitherArea}" : "rejects it")}.");
        }

        // Property 3 -- and the combined overload accepts nothing neither of them accepts.
        if (isEither && !isLocal && !isSheet)
        {
            throw new FuzzAssertionException(
                $"'{text}' is accepted by the combined overload as {eitherSheet ?? "<local>"} {eitherArea}, " +
                "but by neither the local nor the sheet-qualified parser.");
        }

        // Property 4 -- the same relationship between the two name parsers.
        if (isSheetName && !(isName && nameSheet == qualifiedSheet && name == qualifiedName))
        {
            throw new FuzzAssertionException(
                $"'{text}' parses as the sheet name {qualifiedSheet}!{qualifiedName}, but TryParseName " +
                $"{(isName ? $"reads it as {nameSheet ?? "<local>"}!{name}" : "rejects it")}.");
        }

        // Property 5 -- a reference the parser accepts survives being written back out. This is the
        // check that can see a wrong answer rather than a refusal, and reference rendering is
        // load-bearing for every formula modification.
        if (isLocal)
            CheckAreaRoundTrip(text, localArea, ReferenceStyle.A1);

        if (isR1C1)
            CheckAreaRoundTrip(text, r1c1Area, ReferenceStyle.R1C1);

        // Property 6 -- a reference is also a formula. The two entry points read the same text with
        // the same lexer, so text one of them calls a reference cannot be text the other cannot read.
        if (isLocal || isSheet)
        {
            try
            {
                _ = Parse(text, ReferenceStyle.A1);
            }
            catch (Exception e) when (Oracle.IsRejectionDuringParse(e))
            {
                throw new FuzzAssertionException(
                    $"'{text}' parses as an A1 reference but not as an A1 formula.", e);
            }
        }

        return (isLocal, isSheet, isR1C1, isName, isSheetName) switch
        {
            (true, _, _, _, _) => "local reference",
            (_, true, _, _, _) => "sheet reference",
            (_, _, true, _, _) => "R1C1 reference",
            (_, _, _, _, true) => "sheet name",
            (_, _, _, true, _) => "name",
            _ => "rejected",
        };
    }

    private static void CheckAreaRoundTrip(string text, ReferenceArea area, ReferenceStyle style)
    {
        // Writing the area is the process phase; reading it back is the reload phase, where a
        // refusal means the writer produced text its own parser rejects.
        string written;
        try
        {
            written = style == ReferenceStyle.A1 ? area.GetDisplayStringA1() : area.GetDisplayStringR1C1();
        }
        catch (Exception e) when (Oracle.IsToleratedDuringProcess(e))
        {
            if (Oracle.ShouldReport(e))
                Oracle.Report(Reference, "display", e);

            return;
        }

        var reread = style == ReferenceStyle.A1
            ? ReferenceParser.TryParseA1(written, out var again)
            : ReferenceParser.TryParseR1C1(written, out again);

        if (!reread)
        {
            throw new FuzzAssertionException(
                $"'{text}' parses as the {style} area {area}, which is written as '{written}' and no longer parses.");
        }

        if (again != area)
        {
            throw new FuzzAssertionException(
                $"'{text}' parses as the {style} area {area}, which is written as '{written}' and parses back as {again}.");
        }
    }

    private static AstNode Parse(string formula, ReferenceStyle style)
    {
        return style == ReferenceStyle.A1
            ? FormulaParser<ScalarValue, AstNode, Ctx>.CellFormulaA1(formula, Context, Factory)
            : FormulaParser<ScalarValue, AstNode, Ctx>.CellFormulaR1C1(formula, Context, Factory);
    }

    /// <summary>
    /// Turn the fuzzer's bytes into formula text.
    /// </summary>
    /// <remarks>
    /// UTF-8, so that a mutation of a seed formula stays mostly readable text and the corpus is
    /// worth reading. One consequence is worth writing down: <c>Encoding.UTF8.GetString</c>
    /// substitutes U+FFFD for anything malformed, so it can never produce a lone surrogate, and the
    /// <c>ParsingException.UnpairedSurrogate</c> path in the lexer is unreachable from every target
    /// here. Reaching it needs a target that decodes UTF-16.
    /// </remarks>
    private static string Decode(ReadOnlySpan<byte> data)
    {
        return Encoding.UTF8.GetString(data.ToArray());
    }

    private static string Describe(AstNode node)
    {
        return $"{node.GetType().Name}({node.GetDisplayString(ReferenceStyle.A1)})";
    }
}

/// <summary>
/// Raised when a target's own property is violated. Distinct from anything the library throws, so a
/// violated property can never be mistaken for a library exception the oracle tolerates.
/// </summary>
internal sealed class FuzzAssertionException : Exception
{
    public FuzzAssertionException(string message)
        : base(message)
    {
    }

    public FuzzAssertionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
