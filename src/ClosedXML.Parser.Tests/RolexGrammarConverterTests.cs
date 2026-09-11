using Antlr2Rolex;

namespace ClosedXML.Parser.Tests;

/// <summary>
/// The Rolex grammars are generated from the ANTLR lexer grammar, which is the source of truth.
/// </summary>
public class RolexGrammarConverterTests
{
    /// <summary>
    /// Regenerates each committed Rolex grammar and compares it line by line, so a change to
    /// <c>FormulaLexer.g4</c> without a regenerated <c>.rl</c> (or a hand edit of a <c>.rl</c>)
    /// fails here instead of surfacing as a lexer that silently disagrees with ANTLR.
    /// </summary>
    [Theory]
    [InlineData(LexerStyle.A1, "Grammars/LexerA1.rl")]
    [InlineData(LexerStyle.R1C1, "Grammars/LexerR1C1.rl")]
    public void Committed_rolex_grammar_is_generated_from_antlr_grammar(LexerStyle style, string rolexGrammarPath)
    {
        var result = RolexGrammarConverter.Convert(File.ReadAllText("Grammars/FormulaLexer.g4"), style);

        var expected = File.ReadAllLines(rolexGrammarPath);
        var actual = result.RolexGrammar.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < Math.Min(expected.Length, actual.Length); ++i)
            Assert.Equal(expected[i], actual[i]);

        Assert.Equal(expected.Length, actual.Length);
    }

    [Fact]
    public void Tokens_are_written_in_grammar_order_with_fragments_inlined()
    {
        var result = Convert("""
            lexer grammar G;
            B : 'b' DIGIT ;
            A : 'a' ;
            fragment DIGIT : [0-9] ;
            """);

        Assert.Equal("B = '((b(([0-9]))))'\nA = '((a))'\n", result.RolexGrammar);
    }

    [Fact]
    public void Regex_metacharacters_in_a_literal_are_escaped()
    {
        var result = Convert("""
            lexer grammar G;
            T : '[#All].*$' ;
            """);

        Assert.Equal(@"T = '((\[#All\]\.\*\$))'" + "\n", result.RolexGrammar);
    }

    [Fact]
    public void Unicode_escape_is_kept_and_code_point_escape_becomes_the_character()
    {
        // Not a raw string literal, because the grammar itself needs the backslash escapes.
        var result = Convert("lexer grammar G;\nT : '\\u0020' .. '\\u{10FFFF}' ;\n");

        Assert.Equal("T = '(([\\u0020-\U0010FFFF]))'\n", result.RolexGrammar);
    }

    [Fact]
    public void Suffixed_element_is_wrapped_before_and_after_the_suffix()
    {
        var result = Convert("""
            lexer grammar G;
            T : 'a'? [0-9]+ ('b' | 'c')* ;
            """);

        Assert.Equal("T = '((((a)?)(([0-9])+)(((((b)|(c))))*)))'\n", result.RolexGrammar);
    }

    [Fact]
    public void Unsupported_syntax_is_reported_with_its_line()
    {
        var ex = Assert.Throws<AntlrGrammarException>(() => Convert("""
            lexer grammar G;
            T : ~[a] ;
            """));

        Assert.Equal(2, ex.Line);
    }

    [Fact]
    public void Reference_to_an_undefined_rule_is_an_error()
    {
        var ex = Assert.Throws<AntlrGrammarException>(() => Convert("""
            lexer grammar G;
            T : 'a' MISSING ;
            """));

        Assert.Contains("MISSING", ex.Message);
    }

    [Fact]
    public void R1C1_style_uses_the_R1C1_section_instead_of_the_A1_section()
    {
        var result = RolexGrammarConverter.Convert(SectionedGrammar, LexerStyle.R1C1);

        Assert.StartsWith("CELL = '((r))'\n", result.RolexGrammar);
    }

    [Fact]
    public void R1C1_style_drops_an_alternative_that_refers_to_a_rule_only_the_A1_section_defines()
    {
        var a1 = RolexGrammarConverter.Convert(SectionedGrammar, LexerStyle.A1);
        var r1c1 = RolexGrammarConverter.Convert(SectionedGrammar, LexerStyle.R1C1);

        Assert.Equal("CELL = '((a))'\nPREFIX = '((((c)):)|(p))'\n", a1.RolexGrammar);
        Assert.Empty(a1.Warnings);
        Assert.EndsWith("PREFIX = '((p))'\n", r1c1.RolexGrammar);
        Assert.Contains("COL", Assert.Single(r1c1.Warnings));
    }

    // Same layout as FormulaLexer.g4: the R1C1 section is commented out by a `/*` that the
    // `*/` of the next section header closes.
    private const string SectionedGrammar = """
        lexer grammar G;
        /* ---- Local A1 References ---- */
        CELL : 'a' ;
        fragment COL : 'c' ;
        /* ---- Local R1C1 References ---- */
        /*
        CELL : 'r' ;
        /* ---- Functions ---- */
        PREFIX : COL ':' | 'p' ;
        """;

    private static ConversionResult Convert(string grammar) => RolexGrammarConverter.Convert(grammar, LexerStyle.A1);
}
