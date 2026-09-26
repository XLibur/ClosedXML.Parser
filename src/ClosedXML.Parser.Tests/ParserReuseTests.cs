using System.Runtime.CompilerServices;

namespace XLibur.Parser.Tests;

/// <summary>
/// The parser reuses its token list, its argument buffer and itself from one parse to the next on
/// a thread, so a parse allocates only what the factory keeps. These tests hold the allocation
/// down, and prove that reuse leaks nothing from one parse into another.
/// </summary>
public class ParserReuseTests
{
    private const int Iterations = 1_000;

    /// <summary>
    /// The lexer, the parser and a growable argument list used to allocate about 380 bytes per
    /// parse of this formula. What is left is the exact-size argument array the factory receives.
    /// </summary>
    [Fact]
    public void Parsing_a_short_formula_allocates_only_the_argument_array()
    {
        var bytesPerParse = MeasureBytesPerCall(static () =>
            FormulaParser<object, object, object?>.CellFormulaA1("SUM(D1:H1)", null, NoOpFactory.Instance));

        // One object[1] is 32 bytes on a 64-bit runtime.
        Assert.True(bytesPerParse <= 32, $"A parse allocated {bytesPerParse} bytes.");
    }

    [Fact]
    public void Parsing_a_short_formula_in_r1c1_allocates_only_the_argument_array()
    {
        var bytesPerParse = MeasureBytesPerCall(static () =>
            FormulaParser<object, object, object?>.CellFormulaR1C1("SUM(R1C4:R1C8)", null, NoOpFactory.Instance));

        Assert.True(bytesPerParse <= 32, $"A parse allocated {bytesPerParse} bytes.");
    }

    [Fact]
    public void Parsing_a_reference_allocates_nothing()
    {
        var bytesPerParse = MeasureBytesPerCall(static () => ReferenceParser.TryParseA1("B2:$D7", out _));

        Assert.Equal(0, bytesPerParse);
    }

    [Fact]
    public void Factory_receives_arguments_in_an_array_of_their_count()
    {
        var factory = new RecordingFactory();

        FormulaParser<object, object, object?>.CellFormulaA1("SUM(1,2,3)", null, factory);

        var args = Assert.Single(factory.Calls).Args;
        var array = Assert.IsType<object[]>(args);
        Assert.Equal(new object[] { 1.0, 2.0, 3.0 }, array);
    }

    [Fact]
    public void Arguments_of_a_nested_call_stay_apart_from_the_arguments_of_its_caller()
    {
        var factory = new RecordingFactory();

        var node = FormulaParser<object, object, object?>.CellFormulaA1("SUM(1,MAX(2,,3),4)", null, factory);

        var sum = Assert.IsType<Call>(node);
        Assert.Equal("SUM", sum.Name);
        Assert.Equal(3, sum.Args.Count);
        Assert.Equal(1.0, sum.Args[0]);
        var max = Assert.IsType<Call>(sum.Args[1]);
        Assert.Equal(4.0, sum.Args[2]);
        Assert.Equal("MAX", max.Name);
        Assert.Equal(new object[] { 2.0, NoOpFactory.Node, 3.0 }, max.Args);
    }

    /// <summary>
    /// A factory may parse another formula while the parser waits for it. The inner parse must not
    /// take over the token list, the argument buffer or the parser the outer parse is using.
    /// </summary>
    [Fact]
    public void Factory_can_parse_another_formula_while_it_builds_a_node()
    {
        var factory = new RecordingFactory
        {
            OnCall = static call =>
            {
                if (call.Name != "INNER")
                    return;

                var inner = FormulaParser<object, object, object?>.CellFormulaA1("MAX(7,8,9,10,11)", null, new RecordingFactory());
                var max = Assert.IsType<Call>(inner);
                Assert.Equal(new object[] { 7.0, 8.0, 9.0, 10.0, 11.0 }, max.Args);
            },
        };

        var node = FormulaParser<object, object, object?>.CellFormulaA1("SUM(1,INNER(2),3,A1:B2)", null, factory);

        var sum = Assert.IsType<Call>(node);
        Assert.Equal("SUM", sum.Name);
        Assert.Equal(4, sum.Args.Count);
        Assert.Equal(1.0, sum.Args[0]);
        Assert.Equal(new object[] { 2.0 }, Assert.IsType<Call>(sum.Args[1]).Args);
        Assert.Equal(3.0, sum.Args[2]);
        Assert.Same(NoOpFactory.Node, sum.Args[3]);
    }

    /// <summary>
    /// A parse that throws leaves the parser partway through a formula: nested some levels deep,
    /// with arguments collected. The next parse must start from nothing.
    /// </summary>
    [Fact]
    public void Parse_after_a_failed_parse_starts_afresh()
    {
        var tooDeep = string.Concat(Enumerable.Repeat("SUM(1,", 300)) + "1" + new string(')', 300);
        Assert.Throws<ParsingException>(() =>
            FormulaParser<object, object, object?>.CellFormulaA1(tooDeep, null, new RecordingFactory()));

        var factory = new RecordingFactory();
        var node = FormulaParser<object, object, object?>.CellFormulaA1("SUM(5,6)", null, factory);

        Assert.Equal(new object[] { 5.0, 6.0 }, Assert.IsType<Call>(node).Args);
    }

    /// <summary>
    /// The parser a thread keeps for its next parse must not keep the factory, the context or the
    /// formula of the last one alive.
    /// </summary>
    [Fact]
    public void Parser_kept_for_the_next_parse_does_not_keep_the_last_context_alive()
    {
        var context = ParseWithContextOnce();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(context.IsAlive);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference ParseWithContextOnce()
    {
        var context = new object();
        FormulaParser<object, object, object?>.CellFormulaA1("SUM(1)", context, NoOpFactory.Instance);
        return new WeakReference(context);
    }

    /// <summary>
    /// The bytes a call allocates on this thread, on average, once it is warm.
    /// </summary>
    private static long MeasureBytesPerCall<T>(Func<T> call)
    {
        for (var i = 0; i < Iterations; ++i)
            call();

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < Iterations; ++i)
            call();

        var after = GC.GetAllocatedBytesForCurrentThread();
        return (after - before) / Iterations;
    }

    private sealed record Call(string Name, IReadOnlyList<object> Args);

    /// <summary>
    /// A factory that keeps what the parser passes a function, as a real factory does.
    /// </summary>
    private sealed class RecordingFactory : NoOpFactory
    {
        public List<Call> Calls { get; } = new();

        public Action<Call>? OnCall { get; init; }

        public override object NumberNode(object? context, SymbolRange range, double value) => value;

        public override object Function(object? context, SymbolRange range, ReadOnlySpan<char> functionName, IReadOnlyList<object> arguments)
        {
            var call = new Call(functionName.ToString(), arguments);
            Calls.Add(call);
            OnCall?.Invoke(call);
            return call;
        }
    }
}
