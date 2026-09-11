namespace ClosedXML.Parser.Tests.Rules;

/// <summary>
/// A bang name, e.g. <c>!SomeName</c>. [MS-XLSX] 2.2.2.1 forbids it in a cell formula, but the formula
/// of a defined name uses it for a name of the sheet the formula is evaluated in.
/// </summary>
public class BangNameRuleTests
{
    [Theory]
    [InlineData("!SomeName", "SomeName")]
    [InlineData("!_xlnm.Print_Area", "_xlnm.Print_Area")]
    [InlineData("!A1B", "A1B")]
    [InlineData("!Rate", "Rate")]
    [InlineData("!TRUE.X", "TRUE.X")]
    public void Bang_name_is_recognized_in_both_reference_styles(string formula, string name)
    {
        Assert.Equal(new BangNameNode(name), ParseA1(formula));
        Assert.Equal(new BangNameNode(name), ParseR1C1(formula));
        AssertFormula.CstParsed(formula);
    }

    [Fact]
    public void Bang_name_is_an_argument()
    {
        var expected = new FunctionNode("SUM") { Children = new AstNode[] { new BangNameNode("SomeName") } };
        Assert.Equal(expected, ParseA1("SUM(!SomeName)"));
        Assert.Equal(expected, ParseR1C1("SUM(!SomeName)"));
        AssertFormula.CstParsed("SUM(!SomeName)");
    }

    /// <summary>
    /// A reference such as <c>A1</c> is also a valid name, so a bang reference and a bang name match
    /// the same text. The bang reference must win.
    /// </summary>
    [Theory]
    [InlineData("!A1", "A1")]
    [InlineData("!$A$1", "$A$1")]
    [InlineData("!A1:B2", "A1:B2")]
    [InlineData("!A:A", "A:A")]
    public void Name_shaped_bang_reference_is_still_a_bang_reference_in_A1(string formula, string reference)
    {
        Assert.Equal(new BangReferenceNode(ReferenceParser.ParseA1(reference)), ParseA1(formula));
    }

    [Theory]
    [InlineData("!RC")]
    [InlineData("!R1C1")]
    [InlineData("!R")]
    [InlineData("!R[1]C:R[2]C[3]")]
    public void Name_shaped_bang_reference_is_still_a_bang_reference_in_R1C1(string formula)
    {
        Assert.IsType<BangReferenceNode>(ParseR1C1(formula));
    }

    /// <summary>
    /// A name can't be <c>TRUE</c> or <c>FALSE</c>. The lexer can't exclude them from the name after
    /// the bang, so the parsers refuse them.
    /// </summary>
    [Theory]
    [InlineData("!TRUE")]
    [InlineData("!false")]
    [InlineData("SUM(!True)")]
    public void Logical_constant_is_not_a_bang_name(string formula)
    {
        AssertParsingErrorContains(() => ParseA1(formula), "bang name");
        AssertParsingErrorContains(() => ParseR1C1(formula), "bang name");
        AssertFormula.CstNotParsed(formula);
    }

    /// <summary>
    /// [MS-XLSX] defines a bang name as a plain name. A structure reference is a production of its own,
    /// so a bang before a table name is not recognized.
    /// </summary>
    [Fact]
    public void Bang_structure_reference_is_not_recognized()
    {
        AssertFormula.CheckParsingErrorContains("!Sales[Amount]", "The expression `!Sales` was parsed, but the rest `[Amount]` wasn't.");
        AssertFormula.CstNotParsed("!Sales[Amount]");
    }

    private static AstNode ParseA1(string formula) =>
        FormulaParser<ScalarValue, AstNode, Ctx>.CellFormulaA1(formula, new Ctx(), new F());

    private static AstNode ParseR1C1(string formula) =>
        FormulaParser<ScalarValue, AstNode, Ctx>.CellFormulaR1C1(formula, new Ctx(), new F());

    private static void AssertParsingErrorContains(Func<AstNode> parse, string errorSubstring)
    {
        var ex = Assert.Throws<ParsingException>(parse);
        Assert.True(ex.Message.Contains(errorSubstring), $"Error message '{ex.Message}' doesn't contain '{errorSubstring}'.");
    }
}
