using Xunit;

namespace ClosedXML.Parser.Tests.Lexers;

public class IntraTableReferenceTokenTests
{
    [Theory]
    [MemberData(nameof(Data))]
    public void Token_data_are_extracted_and_unescaped(string tokenText, StructuredReferenceArea expectedArea, string expectedFirstColumn, string expectedLastColumn)
    {
        AssertFormula.AssertTokenType(tokenText, FormulaLexer.INTRA_TABLE_REFERENCE);
        TokenParser.ParseIntraTableReference(tokenText, new Token(Token.INTRA_TABLE_REFERENCE, 0, tokenText.Length), out var area, out var firstColumn, out var lastColumn);

        Assert.Equal(expectedArea, area);
        Assert.Equal(expectedFirstColumn, firstColumn);
        Assert.Equal(expectedLastColumn, lastColumn);
    }

    public static IEnumerable<object?[]> Data
    {
        get
        {
            // Portions area
            // INTRA_TABLE_REFERENCE : KEYWORD
            yield return new object?[] { "[#All]", StructuredReferenceArea.All, null, null };
            yield return new object?[] { "[#Data]", StructuredReferenceArea.Data, null, null };
            yield return new object?[] { "[#Headers]", StructuredReferenceArea.Headers, null, null };
            yield return new object?[] { "[#Totals]", StructuredReferenceArea.Totals, null, null };
            yield return new object?[] { "[#This Row]", StructuredReferenceArea.ThisRow, null, null };

            // Empty simple column, per grammar, the SIMPLE_COLUMN_NAME is optional
            // INTRA_TABLE_REFERENCE : '[' SIMPLE_COLUMN_NAME? ']' 
            yield return new object?[] { "[]", StructuredReferenceArea.None, null, null };

            // Simple column
            // INTRA_TABLE_REFERENCE : '[' SIMPLE_COLUMN_NAME? ']' 
            yield return new object?[] { "[Col]", StructuredReferenceArea.None, "Col", null };
            yield return new object?[] { "[Name with space]", StructuredReferenceArea.None, "Name with space", null };

            // Escaped characters
            // INTRA_TABLE_REFERENCE : '[' SIMPLE_COLUMN_NAME? ']'
            // where column name is a possible value of a ESCAPE_COLUMN_CHARACTER
            yield return new object?[] { "['[']]", StructuredReferenceArea.None, "[]", null };
            yield return new object?[] { "['''#]", StructuredReferenceArea.None, "'#", null };
            yield return new object?[] { "['[']'''#]", StructuredReferenceArea.None, "[]'#", null };

            // An escaped character after a space, where the item of an inner reference starts.
            // A tick-escaped '#' opens a column whose second character is a '#', the same shape a
            // keyword has, so only the opening bracket tells `[[#Data]]` from `[ '#]`.
            yield return new object?[] { "[ '#]", StructuredReferenceArea.None, "#", null };
            yield return new object?[] { "[ '#t]", StructuredReferenceArea.None, "#t", null };
            yield return new object?[] { "[ '[]", StructuredReferenceArea.None, "[", null };
            yield return new object?[] { "[ '']", StructuredReferenceArea.None, "'", null };
            yield return new object?[] { "[[#Data], '#]", StructuredReferenceArea.Data, "#", null };

            // A colon is an ordinary column character, so it separates a range only when there is a
            // name on both sides of it. The two sides are each a COLUMN and a COLUMN can't be empty,
            // so a colon with nothing on one side belongs to the name.
            yield return new object?[] { "[:b]", StructuredReferenceArea.None, ":b", null };
            yield return new object?[] { "[a:]", StructuredReferenceArea.None, "a:", null };
            yield return new object?[] { "[:]", StructuredReferenceArea.None, ":", null };
            // One separator, two columns: the second name runs to the bracket, colons and all.
            yield return new object?[] { "[a:b:c]", StructuredReferenceArea.None, "a", "b:c" };
            yield return new object?[] { "[[#Headers],[#Data], '#]", StructuredReferenceArea.Headers | StructuredReferenceArea.Data, "#", null };

            // INTRA_TABLE_REFERENCE : SPACED_LBRACKET INNER_REFERENCE SPACED_RBRACKET
            // where inner reference is `COLUMN_RANGE : COLUMN(':' COLUMN)?`
            yield return new object?[] { "[[First]]", StructuredReferenceArea.None, "First", null };
            yield return new object?[] { "[[First]:[Last]]", StructuredReferenceArea.None, "First", "Last" };
            yield return new object?[] { "[[First]:Last]", StructuredReferenceArea.None, "First", "Last" };
            yield return new object?[] { "[First:[Last]]", StructuredReferenceArea.None, "First", "Last" };
            yield return new object?[] { "[First:Last]", StructuredReferenceArea.None, "First", "Last" };

            // fragment INNER_REFERENCE : KEYWORD_LIST SPACED_COMMA COLUMN_RANGE
            // where KEYWORD_LIST is just a KEYWORD
            yield return new object?[] { "[[#All],[First]]", StructuredReferenceArea.All, "First", null };
            yield return new object?[] { "[[#Data],[First]:[Last]]", StructuredReferenceArea.Data, "First", "Last" };
            yield return new object?[] { "[[#Headers],[First]:Last]", StructuredReferenceArea.Headers, "First", "Last" };
            yield return new object?[] { "[[#Totals],First:[Last]]", StructuredReferenceArea.Totals, "First", "Last" };
            yield return new object?[] { "[[#This Row],First:Last]", StructuredReferenceArea.ThisRow, "First", "Last" };

            // fragment INNER_REFERENCE : KEYWORD_LIST SPACED_COMMA COLUMN_RANGE
            // where KEYWORD_LIST | '[#Headers]' SPACED_COMMA '[#Data]' | '[#Data]' SPACED_COMMA '[#Totals]'
            yield return new object?[] { "[[#Headers],[#Data],[Col]]", StructuredReferenceArea.Headers | StructuredReferenceArea.Data, "Col", null };
            yield return new object?[] { "[[#Headers],[#Data],[First col]:[Last col]]", StructuredReferenceArea.Headers | StructuredReferenceArea.Data, "First col", "Last col" };
            yield return new object?[] { "[[#Headers],[#Data],First:Last]", StructuredReferenceArea.Headers | StructuredReferenceArea.Data, "First", "Last" };
            yield return new object?[] { "[[#Headers],[#Data],[First]:Last]", StructuredReferenceArea.Headers | StructuredReferenceArea.Data, "First", "Last" };
            yield return new object?[] { "[[#Headers],[#Data],First:[Last]]", StructuredReferenceArea.Headers | StructuredReferenceArea.Data, "First", "Last" };

            // fragment INNER_REFERENCE : KEYWORD_LIST
            // where the keyword list is the whole inner reference, with no column range after it
            yield return new object?[] { "[[#All]]", StructuredReferenceArea.All, null, null };
            yield return new object?[] { "[[#Data]]", StructuredReferenceArea.Data, null, null };
            yield return new object?[] { "[[#Headers]]", StructuredReferenceArea.Headers, null, null };
            yield return new object?[] { "[[#Totals]]", StructuredReferenceArea.Totals, null, null };
            yield return new object?[] { "[[#This Row]]", StructuredReferenceArea.ThisRow, null, null };

            // fragment INNER_REFERENCE : KEYWORD_LIST
            // where KEYWORD_LIST | '[#Headers]' SPACED_COMMA '[#Data]' | '[#Data]' SPACED_COMMA '[#Totals]'
            yield return new object?[] { "[[#Headers],[#Data]]", StructuredReferenceArea.Headers | StructuredReferenceArea.Data, null, null };
            yield return new object?[] { "[[#Data],[#Totals]]", StructuredReferenceArea.Data | StructuredReferenceArea.Totals, null, null };

            // spaces are ignored
            yield return new object?[] { "[  [#Headers]  ,  [#Data]  ,  [First col]:[Last col]  ]", StructuredReferenceArea.Headers | StructuredReferenceArea.Data, "First col", "Last col" };
            yield return new object?[] { "[  [#All]  ]", StructuredReferenceArea.All, null, null };
            yield return new object?[] { "[  [#Headers]  ,  [#Data]  ]", StructuredReferenceArea.Headers | StructuredReferenceArea.Data, null, null };
        }
    }

    /// <summary>
    /// A bracket holding nothing but whitespace is not a structured reference. The grammar has no
    /// alternative for one — a simple column name has to start and end with a non-space — and the
    /// ANTLR lexer refuses <c>[ ]</c> outright.
    /// </summary>
    /// <remarks>
    /// The Rolex lexer accepts it as a whole <c>INTRA_TABLE_REFERENCE</c> token, which is a
    /// divergence from ANTLR in its own right; the refusal therefore happens when the token is read.
    /// A fuzzing run found this as an <see cref="IndexOutOfRangeException"/> out of a three
    /// character formula, because reading the token peeked one character past its end.
    /// <para>
    /// Only a space counts: <c>[\t]</c> is a column named after a tab, because the whitespace of a
    /// structured reference is the space character alone.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData("[ ]")]
    [InlineData("[  ]")]
    [InlineData("Table1[ ]")]
    [InlineData("SUM(Table1[ ])")]
    [InlineData("[[#Data], ]")]
    [InlineData("[[#Headers],[#Data], ]")]
    [InlineData("Table1[[#Data], ]")]
    public void A_bracket_holding_only_whitespace_is_refused(string formula)
    {
        Assert.Throws<ParsingException>(
            () => FormulaParser<ScalarValue, AstNode, Ctx>.CellFormulaA1(formula, new Ctx(), new F()));
    }
}