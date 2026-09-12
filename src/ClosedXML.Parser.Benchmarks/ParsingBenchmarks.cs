using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using ClosedXML.Parser.Rolex;

namespace ClosedXML.Parser.Benchmarks;

/// <summary>
/// Lex and parse the formulas of a data set sample, in each reference style. The lexer is the
/// baseline of its style, so the ratio of a parse is the cost of the parser on top of the lexer.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class ParsingBenchmarks
{
    private static readonly F s_astFactory = new();
    private static readonly Ctx s_astContext = new();

    private string[] _formulasA1 = null!;
    private string[] _formulasR1C1 = null!;

    [Params(DataSetName.Enron, DataSetName.Euses)]
    public DataSetName DataSet { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var sample = FormulaSample.Load(DataSet);
        _formulasA1 = sample.A1;
        _formulasR1C1 = sample.R1C1;
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = FormulaSample.Size)]
    [BenchmarkCategory("A1")]
    public int LexA1()
    {
        var tokenCount = 0;
        foreach (var formula in _formulasA1)
            tokenCount += RolexLexer.GetTokensA1(formula.AsSpan()).Count;

        return tokenCount;
    }

    [Benchmark(OperationsPerInvoke = FormulaSample.Size)]
    [BenchmarkCategory("A1")]
    public object ParseA1()
    {
        object node = null!;
        foreach (var formula in _formulasA1)
            node = FormulaParser<object, object, object?>.CellFormulaA1(formula, null, NoOpFactory.Instance);

        return node;
    }

    [Benchmark(OperationsPerInvoke = FormulaSample.Size)]
    [BenchmarkCategory("A1")]
    public AstNode ParseA1ToAst()
    {
        AstNode node = null!;
        foreach (var formula in _formulasA1)
            node = FormulaParser<ScalarValue, AstNode, Ctx>.CellFormulaA1(formula, s_astContext, s_astFactory);

        return node;
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = FormulaSample.Size)]
    [BenchmarkCategory("R1C1")]
    public int LexR1C1()
    {
        var tokenCount = 0;
        foreach (var formula in _formulasR1C1)
            tokenCount += RolexLexer.GetTokensR1C1(formula.AsSpan()).Count;

        return tokenCount;
    }

    [Benchmark(OperationsPerInvoke = FormulaSample.Size)]
    [BenchmarkCategory("R1C1")]
    public object ParseR1C1()
    {
        object node = null!;
        foreach (var formula in _formulasR1C1)
            node = FormulaParser<object, object, object?>.CellFormulaR1C1(formula, null, NoOpFactory.Instance);

        return node;
    }
}
