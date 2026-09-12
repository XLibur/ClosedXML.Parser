using System.Text.RegularExpressions;
using ClosedXML.Parser.Visualizer.Diagram;
using ClosedXML.Parser.Visualizer.Parsing;

namespace ClosedXML.Parser.Visualizer.Tests;

public class DiagramBuilderTests
{
    [Fact]
    public void Draws_a_function_with_its_arguments()
    {
        var diagram = Parse("SUM(B5,2)");

        var expected = """
            flowchart TD
                n0["SUM<br/>[Function]"]:::famFunction
                n1["B5<br/>[Reference]"]:::famReference
                n2["2<br/>[Number]"]:::famValue
                n0 --> n1
                n0 --> n2

            """.ReplaceLineEndings("\n");
        Assert.Equal(expected, diagram.Mermaid);
    }

    [Fact]
    public void Edges_are_in_the_order_of_the_arguments()
    {
        var diagram = Parse("IF(A1,SUM(1,2),3)");

        var edges = diagram.Mermaid.Split('\n').Where(line => line.Contains("-->")).Select(line => line.Trim());
        Assert.Equal(["n0 --> n1", "n0 --> n2", "n2 --> n3", "n2 --> n4", "n0 --> n5"], edges);
    }

    [Fact]
    public void Not_equal_shows_the_Excel_operator()
    {
        var diagram = Parse("A1<>B1");

        Assert.Equal("<>", diagram.Nodes[0].Label);
        Assert.Contains("n0[\"#lt;#gt;<br/>[Binary]\"]", diagram.Mermaid);
    }

    [Fact]
    public void Copied_text_has_a_class_def_for_each_family()
    {
        var diagram = Parse("SUM(B5,2)");

        Assert.StartsWith(diagram.Mermaid, diagram.MermaidWithClassDefs);
        foreach (var family in NodeFamilies.All)
            Assert.Contains($"classDef {NodeFamilies.CssClass(family)} fill:", diagram.MermaidWithClassDefs);
    }

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("\"a\"", "#quot;a#quot;")]
    [InlineData("<>", "#lt;#gt;")]
    [InlineData("a&b", "a#amp;b")]
    [InlineData("#REF!", "#35;REF!")]
    [InlineData("#quot;", "#35;quot;")]
    [InlineData("`x`", "#96;x#96;")]
    [InlineData("a\r\nb", "a  b")]
    [InlineData("'Sheet 1'!A1:[x];{1,2}", "'Sheet 1'!A1:[x];{1,2}")]
    public void Escapes_a_label(string text, string expected)
    {
        Assert.Equal(expected, DiagramBuilder.EscapeLabel(text));
    }

    [Theory]
    [InlineData("\"a\"&\"b\"")]
    [InlineData("'Sheet 1'!A1")]
    [InlineData("{1,2;3,4}")]
    [InlineData("SUM(Table[[#Headers],[Col]])")]
    [InlineData("#REF!")]
    [InlineData("A1<>B1")]
    [InlineData("\"x<y>&z\"")]
    [InlineData("\"`tick`\"")]
    [InlineData("\"#quot;\"")]
    public void A_label_has_no_markup_of_the_formula(string formula)
    {
        var diagram = Parse(formula);

        var labels = LabelPattern().Matches(diagram.Mermaid).Select(match => match.Groups[1].Value.Replace("<br/>", string.Empty)).ToList();
        Assert.Equal(diagram.Nodes.Count, labels.Count);
        Assert.All(labels, label =>
        {
            Assert.DoesNotContain('"', label);
            Assert.DoesNotContain('<', label);
            Assert.DoesNotContain('>', label);
            Assert.DoesNotContain('&', label);
            Assert.DoesNotContain('`', label);
            // Each # starts an entity that the builder wrote.
            Assert.Matches(@"^([^#]|#(35|quot|lt|gt|amp|96);)*$", label);
        });
    }

    private static DiagramModel Parse(string formula)
    {
        var result = FormulaAnalyzer.Parse(formula, ReferenceStyle.A1);
        var parsed = Assert.IsType<ParsedFormula>(result);
        return parsed.Diagram;
    }

    private static Regex LabelPattern() => new(@"^    n\d+\[""(.*)""\]:::fam\w+$", RegexOptions.Multiline);
}
