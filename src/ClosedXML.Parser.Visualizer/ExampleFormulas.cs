namespace ClosedXML.Parser.Visualizer;

public sealed record ExampleFormula(string Label, string Formula, ReferenceStyle Style = ReferenceStyle.A1);

/// <summary>
/// The example chips of the page. Each one shows a different part of the grammar.
/// </summary>
public static class ExampleFormulas
{
    public static IReadOnlyList<ExampleFormula> All { get; } =
    [
        new("Nested IF", "IF(A1<>0,\"non-zero\",IF(B1>=10,\"big\",\"small\"))"),
        new("Implicit intersection", "SUM(@A1:A10,1)"),
        new("Structured reference", "SUM(Sales[[#Data],[Amount]])"),
        new("3D reference", "SUM(Jan:Dec!B5)"),
        new("Array", "SUM({1,2;3,4}*2)"),
        new("R1C1", "SUM(R[-3]C:R[-1]C)", ReferenceStyle.R1C1),
    ];
}
