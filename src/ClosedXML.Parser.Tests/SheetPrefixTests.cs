using System.Text;
using ClosedXML.Parser.Rolex;

namespace ClosedXML.Parser.Tests;

/// <summary>
/// The sheet prefix of a reference, read from a token and written back. The round trip is the
/// invariant that matters: a written prefix must read back as the prefix it was written from,
/// whatever quoting the name needs.
/// </summary>
public class SheetPrefixTests
{
    /// <summary>
    /// Sheet names that are awkward to write: names that need quotes, names that look like
    /// something else (a cell, a number, a logical value, a DDE link) and names with a tick.
    /// </summary>
    private static readonly string[] SheetNames =
    {
        "Sheet1",
        "592101500",
        "PWD1",
        "TRUE",
        "FALSE",
        "My Sheet",
        "Jane's",
        "a|b",
        "R1C1",
        "LOG10",
        "C",
        "ABC～",
        "Sheet.1",
        "_x",
        "über",
    };

    /// <summary>
    /// Names that are a cell as well as a sheet, e.g. <c>PWD1</c> is the cell of column PWD, row 1.
    /// </summary>
    private static readonly string[] CellLikeSheetNames = { "PWD1", "LOG10" };

    public static TheoryData<string> AwkwardSheetNames => ToTheoryData(SheetNames);

    public static TheoryData<string> AwkwardSheetNamesThatAreNotCells =>
        ToTheoryData(SheetNames.Where(name => !CellLikeSheetNames.Contains(name)));

    [Theory]
    [MemberData(nameof(AwkwardSheetNames))]
    public void A_written_sheet_reads_back_as_the_same_sheet(string sheet)
    {
        AssertReadsBack(SheetPrefix.Sheet(sheet));
    }

    [Theory]
    [MemberData(nameof(AwkwardSheetNames))]
    public void A_written_sheet_of_another_workbook_reads_back_as_the_same_sheet(string sheet)
    {
        AssertReadsBack(SheetPrefix.Sheet(sheet, 7));
    }

    [Theory]
    [MemberData(nameof(AwkwardSheetNamesThatAreNotCells))]
    public void A_written_3D_reference_reads_back_as_the_same_first_sheet(string sheet)
    {
        AssertReadsBack(SheetPrefix.Range(sheet, "Last"));
    }

    [Theory(Skip = "The bare prefix lexes as a cell, so it doesn't read back. See issue #31.")]
    [InlineData("PWD1")]
    [InlineData("LOG10")]
    public void A_written_3D_reference_reads_back_when_its_first_sheet_is_also_a_cell(string sheet)
    {
        AssertReadsBack(SheetPrefix.Range(sheet, "Last"));
    }

    [Theory]
    [MemberData(nameof(AwkwardSheetNames))]
    public void A_written_3D_reference_reads_back_as_the_same_last_sheet(string sheet)
    {
        AssertReadsBack(SheetPrefix.Range("First", sheet));
    }

    [Theory]
    [MemberData(nameof(AwkwardSheetNames))]
    public void A_written_3D_reference_of_another_workbook_reads_back_as_the_same_sheets(string sheet)
    {
        AssertReadsBack(SheetPrefix.Range(sheet, sheet, 3));
    }

    [Theory]
    [InlineData("Sdemo123", "tik")]
    [InlineData("App", "a topic")]
    [InlineData("It's", "topic")]
    public void A_written_dde_link_reads_back_as_the_same_link(string application, string topic)
    {
        var prefix = SheetPrefix.DdeLink(application, topic);

        var readBack = ReadBack(prefix);

        Assert.True(readBack.TryGetDdeLink(out var readApplication, out var readTopic));
        Assert.Equal(application, readApplication);
        Assert.Equal(topic, readTopic);
    }

    [Theory]
    [InlineData("Sheet1", null, "Sheet1!")]
    [InlineData("My Sheet", null, "'My Sheet'!")]
    [InlineData("Jane's", null, "'Jane''s'!")]
    [InlineData("592101500", null, "'592101500'!")]
    [InlineData("Sheet1", 1, "[1]Sheet1!")]
    [InlineData("My Sheet", 1, "'[1]My Sheet'!")]
    public void A_sheet_is_written_with_the_quotes_it_needs(string sheet, int? bookIndex, string expected)
    {
        Assert.Equal(expected, Write(SheetPrefix.Sheet(sheet, bookIndex)));
    }

    [Theory]
    [InlineData("Jan", "Dec", null, "Jan:Dec!")]
    [InlineData("My Jan", "Dec", null, "'My Jan:Dec'!")]
    [InlineData("Jan", "My Dec", null, "'Jan:My Dec'!")]
    [InlineData("Jan", "Dec", 2, "[2]Jan:Dec!")]
    [InlineData("My Jan", "Dec", 2, "'[2]My Jan:Dec'!")]
    public void A_3D_reference_quotes_both_sheets_when_either_needs_it(string first, string last, int? bookIndex, string expected)
    {
        Assert.Equal(expected, Write(SheetPrefix.Range(first, last, bookIndex)));
    }

    [Fact]
    public void A_deleted_sheet_is_written_as_a_ref_error()
    {
        Assert.Equal("#REF!", Write(SheetPrefix.Deleted));
        Assert.True(SheetPrefix.Deleted.IsDeleted);
    }

    [Fact]
    public void A_bang_prefix_is_written_as_the_separator_alone()
    {
        Assert.Equal("!", Write(SheetPrefix.Bang));
        Assert.True(SheetPrefix.Bang.IsBang);
    }

    [Fact]
    public void The_whitespace_after_the_separator_is_not_part_of_the_sheet()
    {
        // The lexer puts the whitespace after `!` into the token.
        const string tokenText = "Sheet1! ";

        var prefix = SheetPrefix.ReadSingle(tokenText.AsSpan(), new Token(Token.SINGLE_SHEET_PREFIX, 0, tokenText.Length));

        Assert.Equal("Sheet1", prefix.FirstSheet);
    }

    private static void AssertReadsBack(SheetPrefix prefix)
    {
        Assert.Equal(prefix, ReadBack(prefix));
    }

    /// <summary>
    /// Lex a written prefix and read it back the way <c>FormulaParser</c> does. A 3D reference has
    /// two written forms: one <c>SHEET_RANGE_PREFIX</c> token, and, when the first sheet doesn't
    /// look like a column, the <c>NAME COLON SINGLE_SHEET_PREFIX</c> triple.
    /// </summary>
    private static SheetPrefix ReadBack(SheetPrefix prefix)
    {
        var text = Write(prefix);
        var span = text.AsSpan();
        var tokens = RolexLexer.GetTokensA1(span);
        var lastIndex = tokens.Count - 1;
        Assert.Equal(Token.EofSymbolId, tokens[lastIndex].SymbolId);
        Assert.Equal(text.Length, tokens[lastIndex].StartIndex);

        if (tokens.Count == 4 &&
            tokens[0].SymbolId == Token.NAME &&
            tokens[1].SymbolId == Token.COLON &&
            tokens[2].SymbolId == Token.SINGLE_SHEET_PREFIX)
        {
            var firstSheet = TokenParser.ParseName(span, tokens[0]);
            var lastPrefix = SheetPrefix.ReadSingle(span, tokens[2]);
            return SheetPrefix.Range(firstSheet, lastPrefix.FirstSheet!, lastPrefix.BookIndex);
        }

        Assert.Equal(2, tokens.Count);
        return tokens[0].SymbolId switch
        {
            Token.SINGLE_SHEET_PREFIX => SheetPrefix.ReadSingle(span, tokens[0]),
            Token.SHEET_RANGE_PREFIX => SheetPrefix.ReadRange(span, tokens[0]),
            _ => throw new Xunit.Sdk.XunitException($"'{text}' lexed as {Token.GetSymbolName(tokens[0].SymbolId)}, not as a sheet prefix."),
        };
    }

    private static string Write(SheetPrefix prefix)
    {
        return prefix.Append(new StringBuilder()).ToString();
    }

    private static TheoryData<string> ToTheoryData(IEnumerable<string> names)
    {
        var data = new TheoryData<string>();
        foreach (var name in names)
            data.Add(name);

        return data;
    }
}
