using ClosedXML.Parser.Pratt;

namespace ClosedXML.Parser.Tests.Lexers;

public class ParseletQIdentTests
{
    [Theory]
    // Quoted for a space
    [InlineData("'New York'!A1", "New York")]
    // Quoted for an apostrophe, which is doubled inside the quotes
    [InlineData("'Jane''s'!A1", "Jane's")]
    [InlineData("'a''''b'!A1", "a''b")]
    // Quoted for being a logical literal, which is otherwise read as TRUE/FALSE
    [InlineData("'TRUE'!A1", "TRUE")]
    [InlineData("'false'!A1", "false")]
    public void Quoted_sheet_reference_unescapes_the_sheet_name(string formula, string expectedSheet)
    {
        var node = Assert.IsType<SheetReferenceNode>(Parse(formula));

        Assert.Equal(expectedSheet, node.Sheet);
    }

    /// <summary>
    /// A sheet name may not start or end with an apostrophe, so neither of these names a sheet even
    /// though the quotes and the doubling read as one. The main parser already refused both, reading
    /// the quoted text as a DDE item; this parser used to accept them.
    /// </summary>
    [Theory]
    [InlineData("'''leading'!A1")]
    [InlineData("'trailing'''!A1")]
    public void Quoted_sheet_reference_refuses_a_name_bounded_by_an_apostrophe(string formula)
    {
        Assert.Throws<ParsingException>(() => Parse(formula));
    }

    [Theory]
    [InlineData("'New York'!A1", typeof(SheetReferenceNode))]
    [InlineData("'New York'!$A$1", typeof(SheetReferenceNode))]
    [InlineData("'New York'!A1:B2", typeof(SheetReferenceNode))]
    [InlineData("'New York'!$A$1:$B$2", typeof(SheetReferenceNode))]
    [InlineData("'New York'!A:C", typeof(SheetReferenceNode))]
    [InlineData("'New York'!1:2", typeof(SheetReferenceNode))]
    [InlineData("'New York'!$1:$2", typeof(SheetReferenceNode))]
    [InlineData("'New York'!name", typeof(SheetNameNode))]
    [InlineData("'Jan 1:Dec 31'!A1", typeof(Reference3DNode))]
    [InlineData("'Jan 1:Dec 31'!A1:B2", typeof(Reference3DNode))]
    [InlineData("'Jan 1:Dec 31'!A:C", typeof(Reference3DNode))]
    [InlineData("'Jan 1:Dec 31'!1:2", typeof(Reference3DNode))]
    public void Can_parse_references_starting_at_quoted_ident(string formula, Type expectedNodeType)
    {
        Assert.Equal(expectedNodeType, Parse(formula).GetType());
    }

    [Fact]
    public void Quoted_sheet_range_unescapes_both_sheet_names()
    {
        var node = Assert.IsType<Reference3DNode>(Parse("'Jane''s:Bob''s'!A1"));

        Assert.Equal("Jane's", node.FirstSheet);
        Assert.Equal("Bob's", node.LastSheet);
    }

    [Fact]
    public void Quoted_sheet_name_reference_keeps_the_name_after_the_bang()
    {
        var node = Assert.IsType<SheetNameNode>(Parse("'New York'!MyName"));

        Assert.Equal("New York", node.Sheet);
        Assert.Equal("MyName", node.Name);
    }

    [Theory]
    [InlineData("'New York'")] // no bang, so not a reference
    [InlineData("'New York'!")] // nothing to reference
    [InlineData("'New York'!$")]
    [InlineData("'Jan 1:Dec 31'!")]
    [InlineData("'Jan 1:Dec 31'!name")] // there is no such thing as a 3D name
    [InlineData("'a:b:c'!A1")] // a sheet name cannot contain a colon, so this is not a range
    public void Invalid_references_starting_with_quoted_ident_throw_parsing_exception(string formula)
    {
        Assert.Throws<ParsingException>(() => Parse(formula));
    }

    [Theory]
    // The workbook index is lexed as part of the quoted token, but the unquoted path has
    // no external-workbook support either, so this stays out of scope rather than being
    // half-implemented here. Delete this test when external references are added.
    [InlineData("'[7]Year 20'!A1")]
    [InlineData("'[7]Year 20:Year 25'!A1")]
    public void External_workbook_reference_is_not_supported_yet(string formula)
    {
        Assert.Throws<ParsingException>(() => Parse(formula));
    }

    [Theory]
    [InlineData("New York")]
    [InlineData("Jane's")]
    [InlineData("a'b'c")]
    [InlineData("1st quarter")]
    [InlineData("+")]
    public void Parses_back_the_reference_a_serializer_writes_for_a_quoted_name(string sheet)
    {
        // The two halves have to agree: whatever ShouldQuote decides to wrap in
        // apostrophes has to survive a trip back through the parser.
        Assert.True(NameUtils.ShouldQuote(sheet));
        var formula = "'" + sheet.Replace("'", "''") + "'!A1";

        var node = Assert.IsType<SheetReferenceNode>(Parse(formula));

        Assert.Equal(sheet, node.Sheet);
    }

    private static AstNode Parse(string formula)
    {
        var parser = ParserFactory.Create(new F());
        return parser.ParseFormula(formula, new Ctx());
    }
}
