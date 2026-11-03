using XLibur.Parser.Rolex;

namespace XLibur.Parser.Tests;

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
    /// where they drift apart: no formula in enron or euses holds <c>[ ]</c>. So sweep every short
    /// string over a small alphabet as well, and require the two lexers to read all of them alike.
    /// <para>
    /// This sweep used to be held to a recorded list of 20 inputs, all one defect: ANTLR refuses a
    /// bracket holding nothing but spaces, because a simple column name has to start and end with a
    /// non-space, while the Rolex lexer read it as an <c>INTRA_TABLE_REFERENCE</c>. The regular
    /// expression in <c>LexerA1.rl</c> was right; Rolex built the wrong DFA from it. Rolex's
    /// <c>Optional</c> joined the start state to the accepting states of the optional group, so a
    /// skipped group left the start state standing in the state the group ends in, loop included:
    /// <c>(ab*)?a</c> accepted <c>ba</c>, and the column name <c>(X Y*)? X</c> accepted a leading
    /// space. The vendored generator now carries the fix (#69), the list is empty, and a divergence
    /// of any kind fails here.
    /// </para>
    /// </summary>
    [Fact]
    public void Produce_same_tokens_for_every_short_input()
    {
        var differences = new List<string>();
        var text = new char[4];

        void Sweep(int length)
        {
            if (length > 0)
                CollectDifference(new string(text, 0, length), differences);

            if (length == text.Length)
                return;

            foreach (var c in Alphabet)
            {
                text[length] = c;
                Sweep(length + 1);
            }
        }

        Sweep(0);

        Assert.Empty(differences);
    }

    private static void CollectDifference(string text, List<string> differences)
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


