namespace ClosedXML.Parser.Tests;

/// <summary>
/// A reference can only name a sheet the workbook could hold. <see cref="NameUtils.IsSheetNameValid"/>
/// says which names those are, and a name it rejects has no spelling a formula can be written in: it
/// would need quotes, and a quoted name holding a <c>?</c> reads back as a DDE item rather than a
/// sheet prefix. So the parser refuses the name rather than building a node that can't be written.
/// <para>
/// The lexer already refuses most invalid names, because a sheet name token is narrower than a sheet
/// name. Two shapes reach the parser: the first sheet of a bare 3D reference, which is a <c>NAME</c>
/// token and so may hold a <c>?</c>, and any name longer than the 31 characters a sheet may have.
/// </para>
/// </summary>
public class SheetNameValidationTests
{
    /// <summary>A name of 32 characters, one more than a sheet may have.</summary>
    private const string TooLong = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private const string Refusal = "doesn't name a sheet";

    [Theory]
    [InlineData("€?:D!A1")]
    [InlineData("a?:D!A1")]
    public void A_3D_reference_refuses_a_first_sheet_holding_a_question_mark(string formula)
    {
        AssertFormula.CheckParsingErrorContains(formula, Refusal);
    }

    [Theory]
    [InlineData("{0}!A1")]
    [InlineData("{0}!A1:B2")]
    [InlineData("{0}!Name")]
    [InlineData("{0}!#REF!")]
    [InlineData("'{0}'!A1")]
    [InlineData("[1]{0}!A1")]
    [InlineData("{0}:Dec!A1")]
    [InlineData("Jan:{0}!A1")]
    [InlineData("'{0}:Dec'!A1")]
    [InlineData("'[1]Jan:{0}'!A1")]
    public void A_reference_refuses_a_sheet_longer_than_a_sheet_name_may_be(string shape)
    {
        AssertFormula.CheckParsingErrorContains(string.Format(shape, TooLong), Refusal);
    }

    /// <summary>
    /// The parser only knows a prefix names a sheet once it has read past it, so the error has to
    /// point back at the name rather than at the token the parser stopped on.
    /// </summary>
    [Fact]
    public void The_error_points_at_the_prefix_holding_the_name()
    {
        AssertFormula.CheckParsingErrorContains($"1+'{TooLong}'!A1", "Error at char 2 of");
    }

    /// <summary>
    /// The rule refuses a name, not a character: everything Excel can name a sheet still parses,
    /// including the names that need quotes and the ones at the 31 character limit.
    /// </summary>
    [Theory]
    [InlineData("€:D!A1")]
    [InlineData("Jan:Dec!A1")]
    [InlineData("'My Jan:My Dec'!A1")]
    [InlineData("Sheet1!A1")]
    [InlineData("'My Sheet'!A1")]
    [InlineData("'Jane''s'!A1")]
    [InlineData("[1]Sheet1!A1")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa!A1")]
    public void A_valid_sheet_name_still_parses(string formula)
    {
        AssertFormula.CstParsed(formula);
        FormulaParser<ScalarValue, AstNode, Ctx>.CellFormulaA1(formula, new Ctx(), new F());
    }

    /// <summary>
    /// The rule is about naming a sheet, so a name of the same text that names something else is
    /// untouched.
    /// </summary>
    [Theory]
    [InlineData(TooLong)]
    [InlineData("a?")]
    public void A_name_that_is_not_a_sheet_name_is_not_held_to_the_rule(string formula)
    {
        FormulaParser<ScalarValue, AstNode, Ctx>.CellFormulaA1(formula, new Ctx(), new F());
    }

    /// <summary>
    /// The prefix of a DDE reference is an application and a topic, not a sheet, so it is not held to
    /// what a sheet may be called.
    /// </summary>
    [Theory]
    [InlineData("'My App|A topic name that is long'!'item'")]
    [InlineData("Sdemo123|tik!'id1?req?AAPL_STK_SMART_USD_~/'")]
    public void A_dde_link_is_not_a_sheet_name(string formula)
    {
        FormulaParser<ScalarValue, AstNode, Ctx>.CellFormulaA1(formula, new Ctx(), new F());
    }

    [Theory]
    [InlineData("'" + TooLong + "'!A1")]
    [InlineData("'" + TooLong + "'!A1:B2")]
    public void TryParseSheetA1_refuses_a_sheet_that_is_not_a_valid_sheet_name(string text)
    {
        Assert.False(ReferenceParser.TryParseSheetA1(text, out var sheet, out var area));
        Assert.Equal(string.Empty, sheet);
        Assert.Equal(default, area);
    }

    [Fact]
    public void TryParseSheetName_refuses_a_sheet_that_is_not_a_valid_sheet_name()
    {
        Assert.False(ReferenceParser.TryParseSheetName($"'{TooLong}'!Name", out var sheet, out var name));
        Assert.Equal(string.Empty, sheet);
        Assert.Equal(string.Empty, name);
    }
}
