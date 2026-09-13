namespace ClosedXML.Parser.Tests;

/// <summary>
/// The specifier an Ast node writes into its display string, e.g. <c>Table1[[#Data],[A]:[B]]</c>.
/// A range of columns is written with a colon and a single column once, or the string doesn't parse
/// back as the node it came from.
/// </summary>
/// <remarks>
/// Every case here came out of a fuzzing run: the round-trip property of the <c>formula-a1</c>
/// target writes each reference node back out and demands the same node when it is read again.
/// </remarks>
public class AstStructureReferenceTests
{
    /// <summary>
    /// The parser fills a missing last column in with the first, so an ordinary single-column
    /// reference reaches the writer with both set. Writing both gave <c>Table1[[Column],[Column]]</c>.
    /// </summary>
    [Theory]
    [InlineData(StructuredReferenceArea.None, "Column", "Column", "Table1[Column]")]
    [InlineData(StructuredReferenceArea.None, "First", "Last", "Table1[[First]:[Last]]")]
    [InlineData(StructuredReferenceArea.Data, "First", "Last", "Table1[[#Data],[First]:[Last]]")]
    [InlineData(StructuredReferenceArea.Data, "Column", "Column", "Table1[[#Data],[Column]]")]
    [InlineData(StructuredReferenceArea.Totals, null, null, "Table1[#Totals]")]
    [InlineData(StructuredReferenceArea.All, null, null, "Table1[#All]")]
    [InlineData(StructuredReferenceArea.ThisRow, null, null, "Table1[#This Row]")]
    [InlineData(StructuredReferenceArea.Headers | StructuredReferenceArea.Data, null, null, "Table1[[#Headers],[#Data]]")]
    [InlineData(StructuredReferenceArea.None, null, null, "Table1[]")]
    public void Table_reference(StructuredReferenceArea area, string? firstColumn, string? lastColumn, string expected)
    {
        var node = new StructureReferenceNode("Table1", area, firstColumn, lastColumn);

        Assert.Equal(expected, node.GetDisplayString(A1));
        AssertFormula.SingleNodeParsed(expected, node);
    }

    /// <summary>
    /// A column name escapes a tick, either square bracket and a hash with a tick. Written bare they
    /// read as something else: a column called <c>#</c> came out as <c>[#]</c>, and a hash after a
    /// bracket starts a keyword, so the string no longer parsed at all.
    /// </summary>
    [Theory]
    [InlineData("#", "Table1['#]")]
    [InlineData("#t", "Table1['#t]")]
    [InlineData("[Col", "Table1['[Col]")]
    [InlineData("a]b", "Table1[a']b]")]
    [InlineData("Jane's", "Table1[Jane''s]")]
    [InlineData("[]'#", "Table1['[']'''#]")]
    public void A_column_name_escapes_what_it_has_to(string column, string expected)
    {
        var node = new StructureReferenceNode("Table1", StructuredReferenceArea.None, column, column);

        Assert.Equal(expected, node.GetDisplayString(A1));
        AssertFormula.SingleNodeParsed(expected, node);
    }

    [Theory]
    [InlineData(StructuredReferenceArea.None, "Column", "Column", "[Column]")]
    [InlineData(StructuredReferenceArea.Data, "First", "Last", "[[#Data],[First]:[Last]]")]
    [InlineData(StructuredReferenceArea.None, null, null, "[]")]
    public void Table_reference_without_a_table(StructuredReferenceArea area, string? firstColumn, string? lastColumn, string expected)
    {
        var node = new StructureReferenceNode(null, area, firstColumn, lastColumn);

        Assert.Equal(expected, node.GetDisplayString(A1));
        AssertFormula.SingleNodeParsed(expected, node);
    }

    /// <summary>
    /// A book prefix that names no sheet ends in a bang, the same form <c>ExternalNameNode</c>
    /// writes. Without it the string read as a book index followed by a table name, which the
    /// parser refuses.
    /// </summary>
    [Theory]
    [InlineData(StructuredReferenceArea.None, "Column", "Column", "[4]!Table1[Column]")]
    [InlineData(StructuredReferenceArea.Data, "First", "Last", "[4]!Table1[[#Data],[First]:[Last]]")]
    [InlineData(StructuredReferenceArea.Totals, null, null, "[4]!Table1[#Totals]")]
    [InlineData(StructuredReferenceArea.None, null, null, "[4]!Table1[]")]
    public void External_table_reference(StructuredReferenceArea area, string? firstColumn, string? lastColumn, string expected)
    {
        var node = new ExternalStructureReferenceNode(4, "Table1", area, firstColumn, lastColumn);

        Assert.Equal(expected, node.GetDisplayString(A1));
        AssertFormula.SingleNodeParsed(expected, node);
    }
}
