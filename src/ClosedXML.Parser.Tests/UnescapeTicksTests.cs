namespace ClosedXML.Parser.Tests;

/// <summary>
/// The one reader of a doubled apostrophe, shared by the formula parsers, the sheet prefix and the
/// Pratt prototype. It took the place of a <c>ToString().Replace("''", "'")</c> in each of them, so
/// it has to answer the way that did — including on the lone apostrophe no lexer hands it, because
/// a shared helper reached from a fuzzed entry point should not quietly lose a character.
/// </summary>
public class UnescapeTicksTests
{
    [Theory]
    [InlineData("")]
    [InlineData("Sheet1")]
    [InlineData("''")]
    [InlineData("''''")]
    [InlineData("a''b")]
    [InlineData("''a")]
    [InlineData("a''")]
    // An apostrophe standing on its own, which the lexers pair before any caller gets here.
    [InlineData("'")]
    [InlineData("'''")]
    [InlineData("Jane's")]
    [InlineData("a'")]
    public void Answers_the_way_replacing_in_the_string_did(string input)
    {
        Assert.Equal(input.Replace("''", "'"), TokenParser.UnescapeTicks(input.AsSpan()));
    }

    /// <summary>
    /// A name or an item is as long as the formula holding it, so past
    /// <see cref="TokenParser.MaxStackAllocChars"/> the scratch buffer comes from the heap instead of
    /// the stack. Both sides of that boundary read the same.
    /// </summary>
    [Theory]
    [InlineData(TokenParser.MaxStackAllocChars - 1)]
    [InlineData(TokenParser.MaxStackAllocChars)]
    [InlineData(TokenParser.MaxStackAllocChars + 1)]
    [InlineData(TokenParser.MaxStackAllocChars * 4)]
    public void Answers_the_same_on_either_side_of_the_stack_buffer(int length)
    {
        var input = new string('x', length - 2) + "''";

        Assert.Equal(input.Replace("''", "'"), TokenParser.UnescapeTicks(input.AsSpan()));
    }
}
