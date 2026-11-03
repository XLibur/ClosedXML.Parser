using System.Text.RegularExpressions;
using XLibur.Parser.Pratt;

namespace XLibur.Parser.Tests;

/// <summary>
/// <see cref="ParsingException.Position"/> is the offset a message names, so a caller reads it as data
/// instead of parsing the message text.
/// </summary>
public class ParsingExceptionPositionTests
{
    [Theory]
    [InlineData("1+")]
    [InlineData("1+'a")]
    [InlineData("1+\"abc")]
    [InlineData("1+#NOTANERROR")]
    [InlineData("1+c[a]")]
    [InlineData("1+Sheet1!")]
    [InlineData("A1+[abc")]
    [InlineData("Table1[[#Data], ]")]
    public void Position_equals_the_offset_named_in_the_message(string formula)
    {
        var ex = Assert.Throws<ParsingException>(() => FormulaParser<object, object, object?>.CellFormulaA1(formula, null, new NoOpFactory()));

        var named = Regex.Match(ex.Message, @"(?:char|position|index) (\d+)");
        Assert.True(named.Success, ex.Message);
        Assert.Equal(int.Parse(named.Groups[1].Value), ex.Position);
    }

    [Theory]
    [InlineData("\"\"", 1)]
    [InlineData("1+\"ab\"", 5)]
    public void Position_of_an_invalid_text_character_is_where_the_character_is(string input, int expected)
    {
        var lexer = new Lexer(input);

        var ex = Assert.Throws<ParsingException>(() =>
        {
            while (lexer.Consume().Type != TokenType.Eof)
            {
            }
        });

        Assert.Equal(expected, ex.Position);
    }

    [Fact]
    public void Position_of_unparsed_rest_is_where_the_rest_starts()
    {
        var ex = Assert.Throws<ParsingException>(() => FormulaParser<object, object, object?>.CellFormulaA1("1+2)", null, new NoOpFactory()));

        Assert.Equal(3, ex.Position);
    }

    [Fact]
    public void Position_of_a_whole_text_refusal_is_zero()
    {
        var ex = Assert.Throws<ParsingException>(() => ReferenceParser.ParseA1("nope!"));

        Assert.Equal(0, ex.Position);
    }
}
