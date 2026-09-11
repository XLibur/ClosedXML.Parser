using ClosedXML.Parser.Rolex;

namespace ClosedXML.Parser.Tests.Lexers;

public class BangNameTokenTests
{
    [Theory]
    [InlineData("!SomeName")]
    [InlineData("!_xlnm.Print_Area")]
    [InlineData("!A1B")]
    [InlineData("!Rate")]
    [InlineData("!TRUE")]
    public void Token_is_recognized_in_both_reference_styles(string text)
    {
        AssertFormula.AssertTokenType(text, Token.BANG_NAME);
        var expected = new[] { new Token(Token.BANG_NAME, 0, text.Length), Token.EofSymbol(text.Length) };
        Assert.Equal(expected, RolexLexer.GetTokensA1(text.AsSpan()));
        Assert.Equal(expected, RolexLexer.GetTokensR1C1(text.AsSpan()));
    }

    /// <summary>
    /// A bang reference and a bang name of the same length tie. Both lexers take the rule declared first,
    /// which is the bang reference.
    /// </summary>
    [Theory]
    [InlineData("!A1")]
    [InlineData("!$A$1")]
    [InlineData("!XFD1048576")]
    [InlineData("!A1:B2")]
    [InlineData("!A:A")]
    public void Bang_reference_wins_over_bang_name_in_A1(string text)
    {
        AssertFormula.AssertTokenType(text, Token.BANG_REFERENCE);
        Assert.Equal(new[] { Token.BANG_REFERENCE }, SymbolIds(RolexLexer.GetTokensA1(text.AsSpan())));
    }

    [Theory]
    [InlineData("!RC")]
    [InlineData("!R1C1")]
    [InlineData("!R")]
    [InlineData("!C2")]
    public void Bang_reference_wins_over_bang_name_in_R1C1(string text)
    {
        Assert.Equal(new[] { Token.BANG_REFERENCE }, SymbolIds(RolexLexer.GetTokensR1C1(text.AsSpan())));
    }

    [Theory]
    [InlineData("!SomeName")]
    [InlineData("SUM(!SomeName)")]
    [InlineData("!A1")]
    [InlineData("!A1B")]
    [InlineData("!TRUE")]
    [InlineData("!Sales[Amount]")]
    [InlineData("Sheet1!Name")]
    [InlineData("[1]!Name")]
    [InlineData("!1")]
    public void Rolex_and_antlr_produce_same_tokens(string formula)
    {
        Assert.Equal(AssertFormula.GetAntlrTokens(formula), RolexLexer.GetTokensA1(formula.AsSpan()));
    }

    private static int[] SymbolIds(IEnumerable<Token> tokens) =>
        tokens.Where(token => token.SymbolId != Token.EofSymbolId).Select(token => token.SymbolId).ToArray();
}
