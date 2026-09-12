using ClosedXML.Parser.Visualizer.Parsing;

namespace ClosedXML.Parser.Visualizer.Tests;

public class ParseErrorPositionTests
{
    [Theory]
    [InlineData("SUM(B5,", 7)]
    [InlineData("1+", 2)]
    [InlineData("SUM(B5))", 7)]
    [InlineData("\"abc", 0)]
    [InlineData("\"position 9\"+", 13)]
    public void Finds_the_position_in_the_message_of_the_parser(string formula, int expected)
    {
        var result = Assert.IsType<FailedParse>(FormulaAnalyzer.Parse(formula, ReferenceStyle.A1));

        Assert.Equal(expected, result.Position);
    }

    [Theory]
    [InlineData("x", "Unable to parse 'x'.", null)]
    [InlineData("A1", "Found an unpaired surrogate 0xD800 at 1.", 1)]
    [InlineData("A1", "Unable to find closing square bracket for token from position 1.", 1)]
    [InlineData("A1", "An unterminated literal (delimiter \") found at position 0.", 0)]
    [InlineData("A1", "Unable to determine token for 'A1' at index 1.", 1)]
    [InlineData("A1", "Error at char 99 of 'A1': Unexpected token.", 2)]
    [InlineData("1+A1", "The formula `1+A1` wasn't parsed correctly. The expression `1+` was parsed, but the rest `A1` wasn't.", 2)]
    [InlineData("at 5", "Error at char 4 of 'at 5': Unexpected token.", 4)]
    public void Reads_each_form_of_message(string formula, string message, int? expected)
    {
        Assert.Equal(expected, ParseErrorPosition.Find(formula, message));
    }
}
