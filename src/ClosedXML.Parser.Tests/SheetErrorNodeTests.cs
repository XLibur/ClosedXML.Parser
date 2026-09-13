namespace ClosedXML.Parser.Tests;

/// <summary>
/// The display string of a sheet error, e.g. <c>'[2]Jane''s'!#REF!</c>. A quote wraps the whole
/// prefix, the book index included, so the string has to be built before it is quoted.
/// </summary>
public class SheetErrorNodeTests
{
    [Theory]
    [InlineData(null, "Sheet1", "Sheet1!#REF!")]
    [InlineData(null, "My Sheet", "'My Sheet'!#REF!")]
    [InlineData(null, "Jane's", "'Jane''s'!#REF!")]
    [InlineData(2, "Sheet1", "[2]Sheet1!#REF!")]
    [InlineData(2, "My Sheet", "'[2]My Sheet'!#REF!")]
    [InlineData(2, "Jane's", "'[2]Jane''s'!#REF!")]
    [InlineData(12, "592101500", "'[12]592101500'!#REF!")]
    public void Display_string_keeps_the_book_index_inside_the_quotes(int? workbookIndex, string sheet, string expected)
    {
        var node = new SheetErrorNode(workbookIndex, sheet, "#REF!");

        Assert.Equal(expected, node.GetDisplayString(ReferenceStyle.A1));
    }

    [Theory]
    [InlineData(null, "Sheet1")]
    [InlineData(null, "My Sheet")]
    [InlineData(null, "Jane's")]
    [InlineData(2, "Sheet1")]
    [InlineData(2, "My Sheet")]
    [InlineData(2, "Jane's")]
    [InlineData(12, "592101500")]
    public void Display_string_parses_back_as_the_same_node(int? workbookIndex, string sheet)
    {
        var node = new SheetErrorNode(workbookIndex, sheet, "#REF!");

        AssertFormula.SingleNodeParsed(node.GetDisplayString(ReferenceStyle.A1), node);
    }
}
