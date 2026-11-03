namespace XLibur.Parser.Tests;

/// <summary>
/// Excel refuses a table name that starts like an R1C1 reference: <c>c</c>, <c>r</c>, <c>rc</c>,
/// <c>C2</c>, <c>C2x</c>, <c>R5Z</c>, <c>C05A</c>, <c>R1C1</c> and <c>R2D2</c> all fail in the Table
/// Name box, while <c>Cx</c> is accepted. A table name can't be quoted, so such a name has no spelling
/// an R1C1 formula can be written in: <c>c[a:c]</c> reads as the relative column <c>C[</c>. The A1 parser
/// would take the name, and the formula would then not convert to R1C1 or parse back (#70, #71). The
/// parser refuses it, because no workbook can hold a table by that name.
/// </summary>
public class TableNameValidationTests
{
    private const string Refusal = "doesn't name a table";

    [Theory]
    [InlineData("c[a]")]
    [InlineData("C[a]")]
    [InlineData("r[a]")]
    [InlineData("rc[a]")]
    [InlineData("c[a:c]")]
    [InlineData("c[é:c]")] // #71: the fuzzer's input shape, a non-ASCII char after the bracket
    [InlineData("c[中:c]")]
    [InlineData("c[𐐀:c]")]
    [InlineData("C2x[a]")]
    [InlineData("R5Z[a]")]
    [InlineData("C05A[[#Data],[a]]")]
    [InlineData("R2D2[[#Data],[a]:[b]]")]
    [InlineData("[1]!c[a]")]
    [InlineData("[1]!R5Z[a]")]
    [InlineData("SUM(c[a])")]
    public void A_table_name_that_starts_like_an_R1C1_reference_is_refused(string formula)
    {
        AssertFormula.CheckParsingErrorContains(formula, Refusal);
    }

    [Theory]
    [InlineData("Cx[a]")]
    [InlineData("Table1[a]")]
    [InlineData("Table1[[#Data],[a]:[b]]")]
    [InlineData("SUM(Cx[a])")]
    [InlineData("[1]!Cx[a]")]
    [InlineData("[1]!Table1[a]")]
    public void A_table_name_Excel_accepts_still_parses(string formula)
    {
        AssertFormula.CheckParses(formula);
    }

    [Fact]
    public void The_error_points_at_the_table_name()
    {
        AssertFormula.CheckParsingErrorContains("1+c[a]", "Error at char 2 of");
    }
}
