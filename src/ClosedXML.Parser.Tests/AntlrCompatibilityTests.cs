using ClosedXML.Parser.Rolex;

namespace ClosedXML.Parser.Tests;

/// <summary>
/// ANTLR parser is the source of truth. This test class checks that ANTLR output and Rolex/RDP have same output.
/// </summary>
public class AntlrCompatibilityTests
{
    /// <summary>
    /// The characters a structured reference is built from, plus a few that end a token, so the
    /// sweep below covers the brackets, the keywords and the escapes rather than arbitrary text.
    /// </summary>
    private const string Alphabet = "[ ]#A:,'!1.\r";

    [Theory]
    [InlineData("./data/enron/formulas.csv")]
    [InlineData("./data/euses/formulas.csv")]
    [InlineData("./data/contributions/formulas.csv")]
    public void Produce_same_tokens_for_data_sets(string dataSetFile)
    {
        foreach (var formula in DataSets.ReadCsv(dataSetFile))
        {
            var antlrTokens = AssertFormula.GetAntlrTokens(formula);
            var rolexTokens = RolexLexer.GetTokensA1(formula.AsSpan());

            Assert.Equal(antlrTokens, rolexTokens);
        }
    }

    /// <summary>
    /// The data sets only say the two lexers agree on text somebody wrote, and text nobody wrote is
    /// where they drift apart: no formula in enron or euses holds <c>[ ]</c>, which the two read
    /// differently. So sweep every short string over a small alphabet as well. The one divergence
    /// there is has a test of its own below and is left out here.
    /// </summary>
    [Fact]
    public void Produce_same_tokens_for_every_short_input()
    {
        var differences = new List<string>();
        var text = new char[4];

        void Sweep(int length)
        {
            if (length > 0)
            {
                var input = new string(text, 0, length);
                if (!HoldsBracketOfSpacesOnly(input))
                    AssertSameTokens(input, differences);
            }

            if (length == text.Length)
                return;

            foreach (var c in Alphabet)
            {
                text[length] = c;
                Sweep(length + 1);
            }
        }

        Sweep(0);

        Assert.True(differences.Count == 0, string.Join("\n", differences));
    }

    /// <summary>
    /// The one input the two lexers read differently, asserted rather than left unsaid. ANTLR is the
    /// source of truth and refuses a bracket holding nothing but spaces, because a simple column
    /// name has to start and end with a non-space; the Rolex lexer reads the lot as one
    /// <c>INTRA_TABLE_REFERENCE</c> token. <c>TokenParser</c> refuses the token when it reads it, so
    /// no formula parses differently, but the lexers still disagree.
    /// <para>
    /// It is a defect in Rolex's DFA construction rather than in the grammar or a stale table: the
    /// regular expression in <c>LexerA1.rl</c> refuses <c>[ ]</c>, the committed table is what the
    /// vendored build produces from it, and writing the column name as
    /// <c>X (Y* X)?</c> rather than <c>(X Y*)? X</c> changes the table without changing this. See
    /// issue #44. When Rolex is fixed, this test fails - delete it and the skip above with it.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("[ ]")]
    [InlineData("[  ]")]
    public void The_two_lexers_disagree_on_a_bracket_holding_only_spaces(string text)
    {
        Assert.Equal(Token.ErrorSymbolId, AssertFormula.GetAntlrTokens(text)[0].SymbolId);

        var rolexTokens = RolexLexer.GetTokensA1(text.AsSpan());
        Assert.Equal(Token.INTRA_TABLE_REFERENCE, rolexTokens[0].SymbolId);
        Assert.Equal(text.Length, rolexTokens[0].Length);
    }

    /// <summary>
    /// Does the text hold a bracket with nothing but spaces in it, the one shape the two lexers read
    /// differently? Tabs and line breaks are ordinary column characters, so only a space counts.
    /// </summary>
    private static bool HoldsBracketOfSpacesOnly(string text)
    {
        for (var open = text.IndexOf('['); open >= 0; open = text.IndexOf('[', open + 1))
        {
            var i = open + 1;
            while (i < text.Length && text[i] == ' ')
                ++i;

            if (i > open + 1 && i < text.Length && text[i] == ']')
                return true;
        }

        return false;
    }

    private static void AssertSameTokens(string text, List<string> differences)
    {
        var antlr = Describe(AssertFormula.GetAntlrTokens(text));
        var rolex = Describe(RolexLexer.GetTokensA1(text.AsSpan()));
        if (antlr != rolex)
            differences.Add($"<{text.Replace("\r", "\\r")}> antlr: {antlr} rolex: {rolex}");
    }

    private static string Describe(IReadOnlyList<Token> tokens)
    {
        return string.Join(",", tokens.Select(t => $"{Token.GetSymbolName(t.SymbolId)}@{t.StartIndex}+{t.Length}"));
    }
}


