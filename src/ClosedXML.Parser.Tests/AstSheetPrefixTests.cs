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

    /// <summary>
    /// A bare <c>first:last!</c> reads back as a name, a colon and a single sheet prefix, so a
    /// first sheet that is also a cell has to be quoted. The name is written without knowing the
    /// reference style of the formula it ends up in, so it has to be a name in both styles. The
    /// last sheet stands after the colon, where a cell-like name is a sheet already.
    /// </summary>
    [Theory]
    [InlineData("PWD1", "Dec", "'PWD1:Dec'!A1")]
    [InlineData("LOG10", "Dec", "'LOG10:Dec'!A1")]
    [InlineData("R1C1", "Dec", "'R1C1:Dec'!A1")]
    [InlineData("C", "Dec", "'C:Dec'!A1")]
    [InlineData("Jan", "PWD1", "Jan:PWD1!A1")]
    public void Reference_3d_quotes_a_first_sheet_that_is_also_a_cell(string firstSheet, string lastSheet, string expected)
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

    /// <summary>
    /// A book prefix already says a sheet prefix has started, so a cell-like first sheet behind one
    /// needs no quotes.
    /// </summary>
    [Theory]
    [InlineData("PWD1", "Dec", "[2]PWD1:Dec!A1")]
    [InlineData("R1C1", "Dec", "[2]R1C1:Dec!A1")]
    public void External_reference_3d_leaves_a_cell_like_first_sheet_bare(string firstSheet, string lastSheet, string expected)
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
