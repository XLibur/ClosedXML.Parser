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

    /// <summary>
    /// A formula that nests too deep is refused rather than left to run the stack out. The parser
    /// descends by recursion, so before the limit each of these took the whole process down with a
    /// <c>StackOverflowException</c>, which a caller can't catch: a host reading an untrusted
    /// workbook died instead of rejecting one formula.
    /// </summary>
    /// <remarks>
    /// Braces nest about seven stack frames per level, so they run out first — at 378 levels, a
    /// formula of 757 characters, well inside the 8192 a cell can hold. The other two shapes reach
    /// the same end by their own path: a unary operator re-enters its own method, and an argument
    /// re-enters the whole expression ladder.
    /// </remarks>
    [Theory]
    [InlineData("brace")]
    [InlineData("unary")]
    [InlineData("argument")]
    public void A_formula_that_nests_too_deep_is_a_parsing_error(string shape)
    {
        const int tooDeep = 5_000;
        var formula = shape switch
        {
            "brace" => new string('(', tooDeep) + "1" + new string(')', tooDeep),
            "unary" => new string('-', tooDeep) + "1",
            "argument" => string.Concat(Enumerable.Repeat("SUM(", tooDeep)) + "1" + new string(')', tooDeep),
            _ => throw new ArgumentOutOfRangeException(nameof(shape)),
        };

        Assert.Throws<ParsingException>(() => FormulaConverter.ToR1C1(formula, 1, 1));
        Assert.Throws<ParsingException>(() => FormulaConverter.ModifyA1(formula, "Sheet1", 1, 1, Identity));
    }

    /// <summary>
    /// Nesting that stays within the limit is still parsed, so the limit refuses only the formulas
    /// no workbook holds. Excel accepts at most 64 levels of nested functions.
    /// </summary>
    [Fact]
    public void A_formula_that_nests_within_the_limit_is_parsed()
    {
        const int deepEnough = 200;
        var formula = new string('(', deepEnough) + "1" + new string(')', deepEnough);

        Assert.Equal(formula, FormulaConverter.ToR1C1(formula, 1, 1));
    }

    /// <summary>
    /// A name or a text longer than any stack buffer is parsed, not crashed on. The grammar puts no
    /// length limit on either, and a scratch buffer sized from the token used to come off the stack,
    /// so a long enough one took the process down the same uncatchable way deep nesting did.
    /// </summary>
    [Theory]
    [InlineData("\"{0}\"")]
    [InlineData("Table1[[{0}]]")]
    public void A_token_longer_than_the_stack_buffer_is_parsed(string shape)
    {
        var formula = string.Format(shape, new string('a', 400_000));

        Assert.Equal(formula, FormulaConverter.ToR1C1(formula, 1, 1));
    }

    /// <summary>
    /// A sheet name that long is refused for its length, since no workbook holds one, but it is
    /// refused as a verdict on the formula rather than by running the stack out while reading it.
    /// </summary>
    [Fact]
    public void A_sheet_name_longer_than_the_stack_buffer_is_a_parsing_error()
    {
        var formula = "'" + new string('a', 400_000) + "'!A1";

        Assert.Throws<ParsingException>(() => FormulaConverter.ToR1C1(formula, 1, 1));
    }
}
