using ClosedXML.Parser.Visualizer.Parsing;

namespace ClosedXML.Parser.Visualizer.Tests;

public class FormulaAnalyzerTests
{
    [Fact]
    public void Records_the_range_of_each_node()
    {
        var ranges = Ranges("SUM(B5,2)");

        Assert.Equal([("SUM", 0, 9), ("B5", 4, 6), ("2", 7, 8)], ranges);
    }

    [Fact]
    public void Equal_nodes_keep_their_own_ranges()
    {
        var ranges = Ranges("B5+B5");

        Assert.Equal([("+", 0, 5), ("B5", 0, 2), ("B5", 3, 5)], ranges);
    }

    [Fact]
    public void The_inner_node_of_parentheses_keeps_its_own_range()
    {
        var ranges = Ranges("(1+2)*3");

        Assert.Equal([("*", 0, 7), ("+", 1, 4), ("1", 1, 2), ("2", 3, 4), ("3", 6, 7)], ranges);
    }

    [Fact]
    public void Parses_in_the_given_reference_style()
    {
        var a1 = Assert.IsType<ParsedFormula>(FormulaAnalyzer.Parse("R2C3", ReferenceStyle.A1));
        var r1c1 = Assert.IsType<ParsedFormula>(FormulaAnalyzer.Parse("R2C3", ReferenceStyle.R1C1));

        Assert.Equal("Name", a1.Diagram.Nodes[0].Type);
        Assert.Equal("Reference", r1c1.Diagram.Nodes[0].Type);
    }

    [Fact]
    public void A_failed_parse_has_the_message_and_the_position()
    {
        var result = Assert.IsType<FailedParse>(FormulaAnalyzer.Parse("SUM(B5,", ReferenceStyle.A1));

        Assert.NotEmpty(result.Message);
        Assert.Equal(7, result.Position);
    }

    private static List<(string Label, int Start, int End)> Ranges(string formula)
    {
        var parsed = Assert.IsType<ParsedFormula>(FormulaAnalyzer.Parse(formula, ReferenceStyle.A1));
        return parsed.Diagram.Nodes.Select(node => (node.Label, node.Range.Start, node.Range.End)).ToList();
    }
}
