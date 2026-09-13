using ClosedXML.Parser.Rolex;

namespace ClosedXML.Parser.Tests.Lexers;

public class DdeItemTokenTests
{
    [Theory]
    [InlineData("'id1?req?AAPL_STK_SMART_USD_~/'", "id1?req?AAPL_STK_SMART_USD_~/")]
    [InlineData("'NG5100-EOL,PRIM ACT 1,1'", "NG5100-EOL,PRIM ACT 1,1")]
    [InlineData("'It''s'", "It's")]
    [InlineData("''''", "'")]
    public void Token_is_recognized_and_unescaped(string tokenText, string expectedItem)
    {
        AssertFormula.AssertTokenType(tokenText, FormulaLexer.DDE_ITEM);
        Assert.Equal(expectedItem, TokenParser.ParseDdeItem(tokenText, new Token(Token.DDE_ITEM, 0, tokenText.Length)));
    }

    [Theory]
    [InlineData("[1]!'id1?req'")]
    [InlineData("Sdemo123|tik!'id1?req'")]
    [InlineData("'Sheet 1'!A1")]
    [InlineData("'It''s'!A1")]
    [InlineData("''")]
    [InlineData("'unterminated")]
    public void Rolex_and_antlr_produce_same_tokens(string formula)
    {
        Assert.Equal(AssertFormula.GetAntlrTokens(formula), RolexLexer.GetTokensA1(formula.AsSpan()));
    }

    [Theory]
    [InlineData("[1]!'id1?req'")]
    [InlineData("Sdemo123|tik!'id1?req'")]
    public void Both_reference_styles_produce_same_tokens(string formula)
    {
        Assert.Equal(RolexLexer.GetTokensA1(formula.AsSpan()), RolexLexer.GetTokensR1C1(formula.AsSpan()));
    }

    /// <summary>
    /// An item is as long as the formula holding it, so one past <c>MaxStackAllocChars</c> unescapes
    /// through a buffer taken from the heap rather than the stack. Both paths write the same item.
    /// </summary>
    [Theory]
    [InlineData(255)]
    [InlineData(256)]
    [InlineData(257)]
    [InlineData(1000)]
    public void Item_longer_than_the_stack_buffer_is_unescaped(int escapedLength)
    {
        // A run of letters with one doubled tick halfway along it.
        var before = (escapedLength - 2) / 2;
        var after = escapedLength - 2 - before;
        var tokenText = "'" + new string('x', before) + "''" + new string('y', after) + "'";

        AssertFormula.AssertTokenType(tokenText, FormulaLexer.DDE_ITEM);
        var item = TokenParser.ParseDdeItem(tokenText, new Token(Token.DDE_ITEM, 0, tokenText.Length));

        Assert.Equal(new string('x', before) + "'" + new string('y', after), item);
    }

    [Fact]
    public void Quoted_sheet_prefix_is_longer_match_than_item()
    {
        var tokens = RolexLexer.GetTokensA1("'Sheet 1'!A1".AsSpan());
        Assert.Equal(Token.SINGLE_SHEET_PREFIX, tokens[0].SymbolId);
    }
}
