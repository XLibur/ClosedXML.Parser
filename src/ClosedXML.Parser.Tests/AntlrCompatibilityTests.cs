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
    /// Every input the two lexers read differently, with the token stream each of them produces,
    /// ordered as <see cref="Produce_same_tokens_for_every_short_input"/> collects them.
    /// <para>
    /// All of them are one defect. ANTLR is the source of truth and refuses a bracket holding
    /// nothing but spaces, because a simple column name has to start and end with a non-space; the
    /// Rolex lexer reads the bracket as one <c>INTRA_TABLE_REFERENCE</c> token and carries on
    /// lexing after it, which is why the same bracket appears here after a prefix, before a suffix
    /// and alone. <c>TokenParser</c> refuses the token when it reads it, so no formula parses
    /// differently, but the lexers still disagree. Tabs and line breaks are ordinary column
    /// characters, so <c>[\r]</c> is read the same way by both and only a space does this.
    /// </para>
    /// <para>
    /// The defect is in Rolex's DFA construction rather than in the grammar or a stale table: the
    /// regular expression in <c>LexerA1.rl</c> refuses <c>[ ]</c>, the committed table is what the
    /// vendored build produces from it, and writing the column name as <c>X (Y* X)?</c> rather than
    /// <c>(X Y*)? X</c> changes the table without changing this. See issue #44. When Rolex is fixed
    /// this list empties and the test says so, entry by entry.
    /// </para>
    /// </summary>
    private static readonly string[] KnownDivergences =
    {
        "<[  ]> antlr: ErrorSymbolId@0+0 rolex: INTRA_TABLE_REFERENCE@0+4,EofSymbolId@4+0",
        "<[ ]> antlr: ErrorSymbolId@0+0 rolex: INTRA_TABLE_REFERENCE@0+3,EofSymbolId@3+0",
        "<[ ][> antlr: ErrorSymbolId@0+0 rolex: INTRA_TABLE_REFERENCE@0+3,ErrorSymbolId@3+0",
        "<[ ] > antlr: ErrorSymbolId@0+0 rolex: INTRA_TABLE_REFERENCE@0+3,SPACE@3+1,EofSymbolId@4+0",
        "<[ ]]> antlr: ErrorSymbolId@0+0 rolex: INTRA_TABLE_REFERENCE@0+3,ErrorSymbolId@3+0",
        "<[ ]#> antlr: ErrorSymbolId@0+0 rolex: INTRA_TABLE_REFERENCE@0+3,SPILL@3+1,EofSymbolId@4+0",
        "<[ ]A> antlr: ErrorSymbolId@0+0 rolex: INTRA_TABLE_REFERENCE@0+3,NAME@3+1,EofSymbolId@4+0",
        "<[ ]:> antlr: ErrorSymbolId@0+0 rolex: INTRA_TABLE_REFERENCE@0+3,COLON@3+1,EofSymbolId@4+0",
        "<[ ],> antlr: ErrorSymbolId@0+0 rolex: INTRA_TABLE_REFERENCE@0+3,COMMA@3+1,EofSymbolId@4+0",
        "<[ ]'> antlr: ErrorSymbolId@0+0 rolex: INTRA_TABLE_REFERENCE@0+3,ErrorSymbolId@3+0",
        "<[ ]!> antlr: ErrorSymbolId@0+0 rolex: INTRA_TABLE_REFERENCE@0+3,ErrorSymbolId@3+0",
        "<[ ]1> antlr: ErrorSymbolId@0+0 rolex: INTRA_TABLE_REFERENCE@0+3,NUMERICAL_CONSTANT@3+1,EofSymbolId@4+0",
        "<[ ].> antlr: ErrorSymbolId@0+0 rolex: INTRA_TABLE_REFERENCE@0+3,ErrorSymbolId@3+0",
        "<[ ]\\r> antlr: ErrorSymbolId@0+0 rolex: INTRA_TABLE_REFERENCE@0+3,ErrorSymbolId@3+0",
        "< [ ]> antlr: SPACE@0+1,ErrorSymbolId@1+0 rolex: SPACE@0+1,INTRA_TABLE_REFERENCE@1+3,EofSymbolId@4+0",
        "<#[ ]> antlr: SPILL@0+1,ErrorSymbolId@1+0 rolex: SPILL@0+1,INTRA_TABLE_REFERENCE@1+3,EofSymbolId@4+0",
        "<A[ ]> antlr: NAME@0+1,ErrorSymbolId@1+0 rolex: NAME@0+1,INTRA_TABLE_REFERENCE@1+3,EofSymbolId@4+0",
        "<:[ ]> antlr: COLON@0+1,ErrorSymbolId@1+0 rolex: COLON@0+1,INTRA_TABLE_REFERENCE@1+3,EofSymbolId@4+0",
        "<,[ ]> antlr: COMMA@0+1,ErrorSymbolId@1+0 rolex: COMMA@0+1,INTRA_TABLE_REFERENCE@1+3,EofSymbolId@4+0",
        "<1[ ]> antlr: NUMERICAL_CONSTANT@0+1,ErrorSymbolId@1+0 rolex: NUMERICAL_CONSTANT@0+1,INTRA_TABLE_REFERENCE@1+3,EofSymbolId@4+0",
    };

    /// <summary>
    /// The data sets only say the two lexers agree on text somebody wrote, and text nobody wrote is
    /// where they drift apart: no formula in enron or euses holds <c>[ ]</c>, which the two read
    /// differently. So sweep every short string over a small alphabet as well, and hold the whole
    /// sweep to <see cref="KnownDivergences"/> - nothing is skipped, so a divergence that spreads
    /// into a context this already covers fails here too.
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

        Assert.Equal(KnownDivergences, differences);
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


