namespace ClosedXML.Parser.Tests;

/// <summary>
/// What <see cref="FormulaConverter"/> throws when it will not do the job. Every one of its methods
/// documents <see cref="ParsingException"/> for a formula it cannot parse, and text the parser
/// refuses has to arrive as that type however it was reached — an <c>ArgumentException</c> names a
/// parameter the caller never passed, so it reads as a bug in the library rather than as a verdict
/// on the formula.
/// </summary>
/// <remarks>
/// The empty formula came out of the first fuzzing run of the <c>modify</c> target: the fuzzer's
/// very first input is zero bytes.
/// </remarks>
public class FormulaConverterRefusalTests
{
    private static readonly FormulaModifier Identity = new();

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("   \r\n ")]
    public void A_formula_that_is_only_whitespace_is_a_parsing_error(string formula)
    {
        Assert.Throws<ParsingException>(() => FormulaConverter.ModifyA1(formula, "Sheet1", 1, 1, Identity));
        Assert.Throws<ParsingException>(() => FormulaConverter.ModifyR1C1(formula, "Sheet1", 1, 1, Identity));
        Assert.Throws<ParsingException>(() => FormulaConverter.ToR1C1(formula, 1, 1));
        Assert.Throws<ParsingException>(() => FormulaConverter.ToA1(formula, 1, 1));
    }

    /// <summary>
    /// A null formula is a fault in the call, not in the formula, so it keeps an argument exception.
    /// </summary>
    [Fact]
    public void A_null_formula_is_an_argument_error()
    {
        Assert.Throws<ArgumentNullException>(() => FormulaConverter.ModifyA1(null!, "Sheet1", 1, 1, Identity));
        Assert.Throws<ArgumentNullException>(() => FormulaConverter.ToR1C1(null!, 1, 1));
    }

    [Theory]
    [InlineData(0, 1, "row")]
    [InlineData(1048577, 1, "row")]
    [InlineData(1, 0, "col")]
    [InlineData(1, 16385, "col")]
    public void An_anchor_outside_the_sheet_names_the_parameter_it_means(int row, int col, string parameter)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => FormulaConverter.ModifyA1("A1", "Sheet1", row, col, Identity));

        Assert.Equal(parameter, exception.ParamName);
    }
}
