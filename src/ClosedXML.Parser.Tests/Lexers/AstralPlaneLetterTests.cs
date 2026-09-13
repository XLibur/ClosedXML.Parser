using static ClosedXML.Parser.ReferenceAxisType;

namespace ClosedXML.Parser.Tests.Lexers;

/// <summary>
/// A letter from an astral plane — one above the BMP, written as a surrogate pair — in the places
/// a formula can hold one. The live lexer reads a name from a codepoint range rather than from a
/// Unicode category, so it always accepted these; the tests are here to keep it that way and to
/// say what the other pieces do with the letter once it is read.
/// </summary>
public class AstralPlaneLetterTests
{
    private const string Deseret = "\U00010400"; // DESERET CAPITAL LETTER LONG I
    private const string CjkExtB = "\U00020000"; // CJK ideograph extension B

    [Theory]
    [InlineData(Deseret)]
    [InlineData(CjkExtB)]
    [InlineData("name" + Deseret)]
    [InlineData(Deseret + "name")]
    public void A_name_can_hold_a_letter_from_an_astral_plane(string name)
    {
        AssertFormula.SingleNodeParsed(name, new NameNode(name));
    }

    [Theory]
    [InlineData(Deseret)]
    [InlineData(CjkExtB)]
    public void A_sheet_name_can_hold_a_letter_from_an_astral_plane(string sheet)
    {
        // The name is quoted because ShouldQuote quotes anything holding a surrogate: what Excel
        // does with a codepoint above the BMP was never collected, so the safe answer is taken.
        Assert.True(NameUtils.ShouldQuote(sheet.AsSpan()));

        AssertFormula.SingleNodeParsed(
            $"'{sheet}'!A1",
            new SheetReferenceNode(sheet, new ReferenceArea(new RowCol(Relative, 1, Relative, 1, A1))));
    }
}
