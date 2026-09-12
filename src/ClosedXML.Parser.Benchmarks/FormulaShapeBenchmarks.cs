using BenchmarkDotNet.Attributes;
using ClosedXML.Parser.Rolex;

namespace ClosedXML.Parser.Benchmarks;

/// <summary>
/// Lex and parse single formulas of typical shapes, to find the constructs that cost the most.
/// </summary>
[MemoryDiagnoser]
public class FormulaShapeBenchmarks
{
    private static readonly F s_astFactory = new();
    private static readonly Ctx s_astContext = new();

    [ParamsSource(nameof(Shapes))]
    public FormulaShape Shape { get; set; } = null!;

    public static IEnumerable<FormulaShape> Shapes =>
    [
        new("Cell", "A1"),
        new("SumArea", "SUM($A$1:$B$10)"),
        new("Arithmetic", "A1*B1+C1/D1-E1^2"),
        new("NestedIf", "IF(A1>0,IF(B1>0,\"++\",\"+-\"),IF(B1>0,\"-+\",\"--\"))"),
        new("SheetLookup", "VLOOKUP($A2,'Price list'!$A$1:$D$500,MATCH(B$1,'Price list'!$A$1:$D$1,0),FALSE)"),
        new("External", "[1]Sheet1!$A$1+'[2]Other sheet'!B2"),
        new("ArrayConstant", "SUM({1,2,3;4,5,6}*A1:C2)"),
        new("Structured", "SUM(Table1[[#This Row],[Amount]:[Tax]])"),
        new("EscapedText", "\"He said \"\"hi\"\" to \"&A1"),
        new("FutureFunction", "_xlfn.XLOOKUP(A1,Sheet2!B:B,Sheet2!C:C,\"none\")"),
        new("RefOperators", "SUM((A1:B5 B2:C7,D1:D3))"),
        // A formula of the Enron data set.
        new("Long", "SUM(IF((DelPoint= \"4C\")*IF((DType = \"firm\")+(DType = \"econ\")>0,1,0)*(OFFSET(DelPoint,0,B3+2)<0),OFFSET(DelPoint,0,B3+2),0))"),
    ];

    [Benchmark(Baseline = true)]
    public int Lex() => RolexLexer.GetTokensA1(Shape.Formula.AsSpan()).Count;

    [Benchmark]
    public object Parse() => FormulaParser<object, object, object?>.CellFormulaA1(Shape.Formula, null, NoOpFactory.Instance);

    [Benchmark]
    public AstNode ParseToAst() => FormulaParser<ScalarValue, AstNode, Ctx>.CellFormulaA1(Shape.Formula, s_astContext, s_astFactory);
}

/// <summary>
/// A formula of one shape. BenchmarkDotNet shows the name of the shape as the parameter.
/// </summary>
public sealed record FormulaShape(string Name, string Formula)
{
    public override string ToString() => Name;
}
