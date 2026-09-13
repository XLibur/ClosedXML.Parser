namespace ClosedXML.Parser.Tests;

/// <summary>
/// The sheet prefix an Ast node writes into its display string, e.g. <c>'[2]Jane''s'!A1</c>. A name
/// that needs quotes has to get them and a name that doesn't must stay bare, or the string doesn't
/// parse back as the node it came from. A quote wraps the whole prefix, the book index included.
/// </summary>
public class AstSheetPrefixTests
{
    private static readonly ReferenceArea Cell = ReferenceParser.ParseA1("A1");

    [Theory]
    [InlineData("Sheet1", "Sheet1!A1")]
    [InlineData("My Sheet", "'My Sheet'!A1")]
    [InlineData("Jane's", "'Jane''s'!A1")]
    public void Sheet_reference(string sheet, string expected)
    {
        var node = new SheetReferenceNode(sheet, Cell);

        Assert.Equal(expected, node.GetDisplayString(A1));
        AssertFormula.SingleNodeParsed(expected, node);
    }

    [Theory]
    [InlineData("Sheet1", "[2]Sheet1!A1")]
    [InlineData("My Sheet", "'[2]My Sheet'!A1")]
    [InlineData("Jane's", "'[2]Jane''s'!A1")]
    public void External_sheet_reference(string sheet, string expected)
    {
        var node = new ExternalSheetReferenceNode(2, sheet, Cell);

        Assert.Equal(expected, node.GetDisplayString(A1));
        AssertFormula.SingleNodeParsed(expected, node);
    }

    [Theory]
    [InlineData("Sheet1", "[2]Sheet1!MyName")]
    [InlineData("My Sheet", "'[2]My Sheet'!MyName")]
    [InlineData("Jane's", "'[2]Jane''s'!MyName")]
    public void External_sheet_name(string sheet, string expected)
    {
        var node = new ExternalSheetNameNode(2, sheet, "MyName");

        Assert.Equal(expected, node.GetDisplayString(A1));
        AssertFormula.SingleNodeParsed(expected, node);
    }

    [Theory]
    [InlineData(null, "[2]!FUNC")]
    [InlineData("Sheet1", "[2]Sheet1!FUNC")]
    [InlineData("My Sheet", "'[2]My Sheet'!FUNC")]
    [InlineData("Jane's", "'[2]Jane''s'!FUNC")]
    public void External_function(string? sheet, string expected)
    {
        var node = new ExternalFunctionNode(2, sheet, "FUNC");

        Assert.Equal(expected, node.GetDisplayString(A1));

        // A display string names the node, it doesn't carry the arguments, so a call needs its
        // parentheses back before it parses.
        AssertFormula.SingleNodeParsed(expected + "()", node);
    }

    [Theory]
    [InlineData("Sheet1", "Sheet1!FUNC")]
    [InlineData("My Sheet", "'My Sheet'!FUNC")]
    [InlineData("Jane's", "'Jane''s'!FUNC")]
    public void Sheet_function(string sheet, string expected)
    {
        var node = new FunctionNode(sheet, "FUNC");

        Assert.Equal(expected, node.GetDisplayString(A1));
        AssertFormula.SingleNodeParsed(expected + "()", node);
    }

    [Theory]
    [InlineData("Jan", "Dec", "Jan:Dec!A1")]
    [InlineData("My Jan", "Dec", "'My Jan:Dec'!A1")]
    [InlineData("Jan", "My Dec", "'Jan:My Dec'!A1")]
    [InlineData("Jane's", "Dec", "'Jane''s:Dec'!A1")]
    public void Reference_3d(string firstSheet, string lastSheet, string expected)
    {
        var node = new Reference3DNode(firstSheet, lastSheet, Cell);

        Assert.Equal(expected, node.GetDisplayString(A1));
        AssertFormula.SingleNodeParsed(expected, node);
    }

    [Theory]
    [InlineData("Jan", "Dec", "[2]Jan:Dec!A1")]
    [InlineData("My Jan", "Dec", "'[2]My Jan:Dec'!A1")]
    [InlineData("Jan", "My Dec", "'[2]Jan:My Dec'!A1")]
    [InlineData("Jane's", "Dec", "'[2]Jane''s:Dec'!A1")]
    public void External_reference_3d(string firstSheet, string lastSheet, string expected)
    {
        var node = new ExternalReference3DNode(2, firstSheet, lastSheet, Cell);

        Assert.Equal(expected, node.GetDisplayString(A1));
        AssertFormula.SingleNodeParsed(expected, node);
    }

    [Theory]
    [InlineData("Sheet1", "Sheet1!MyName")]
    [InlineData("My Sheet", "'My Sheet'!MyName")]
    [InlineData("Jane's", "'Jane''s'!MyName")]
    public void Sheet_name(string sheet, string expected)
    {
        var node = new SheetNameNode(sheet, "MyName");

        Assert.Equal(expected, node.GetDisplayString(A1));
        AssertFormula.SingleNodeParsed(expected, node);
    }
}
