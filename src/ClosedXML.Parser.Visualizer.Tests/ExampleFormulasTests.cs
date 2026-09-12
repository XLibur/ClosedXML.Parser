using ClosedXML.Parser.Visualizer.Parsing;

namespace ClosedXML.Parser.Visualizer.Tests;

public class ExampleFormulasTests
{
    public static IEnumerable<object[]> Labels => ExampleFormulas.All.Select(example => new object[] { example.Label });

    [Theory]
    [MemberData(nameof(Labels))]
    public void Each_example_parses(string label)
    {
        var example = ExampleFormulas.All.Single(example => example.Label == label);

        var result = FormulaAnalyzer.Parse(example.Formula, example.Style);

        Assert.True(result is ParsedFormula, (result as FailedParse)?.Message);
    }
}
