using ClosedXML.Parser.Rolex;

namespace ClosedXML.Parser.Tests.Lexers;

public class NonrefErrorsTokenTests
{
    [Theory]
    [MemberData(nameof(ErrorValues.AddedAfterMsXlsx), MemberType = typeof(ErrorValues))]
    public void Error_added_after_MS_XLSX_is_a_single_token_in_any_casing(string error)
    {
        foreach (var text in new[] { error, error.ToLowerInvariant() })
        {
            AssertFormula.AssertTokenType(text, FormulaLexer.NONREF_ERRORS);
            var expected = new[] { new Token(Token.NONREF_ERRORS, 0, text.Length), Token.EofSymbol(text.Length) };
            Assert.Equal(expected, RolexLexer.GetTokensA1(text.AsSpan()));
            Assert.Equal(expected, RolexLexer.GetTokensR1C1(text.AsSpan()));
        }
    }

    /// <summary>
    /// A bare <c>#</c> is the spill operator. An error value is the longer match only when the
    /// whole value is there, so the operator must still lex as one.
    /// </summary>
    [Theory]
    [InlineData("ERROR.TYPE(#SPILL!)")]
    [InlineData("A1#")]
    [InlineData("SUM(A1#)")]
    [InlineData("A1#+#CALC!")]
    [InlineData("A1#SPILL")]
    public void Rolex_and_antlr_produce_same_tokens(string formula)
    {
        Assert.Equal(AssertFormula.GetAntlrTokens(formula), RolexLexer.GetTokensA1(formula.AsSpan()));
    }

    [Fact]
    public void Spill_operator_after_a_cell_is_not_an_error_value()
    {
        Assert.Equal(new[] { Token.A1_CELL, Token.SPILL }, SymbolIds(RolexLexer.GetTokensA1("A1#".AsSpan())));
        Assert.Equal(new[] { Token.A1_CELL, Token.SPILL }, SymbolIds(RolexLexer.GetTokensR1C1("RC#".AsSpan())));
    }

    private static int[] SymbolIds(IEnumerable<Token> tokens) =>
        tokens.Where(token => token.SymbolId != Token.EofSymbolId).Select(token => token.SymbolId).ToArray();
}
