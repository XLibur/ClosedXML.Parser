namespace ClosedXML.Parser.Tests.Rules;

public class FunctionCallRuleTests
{
    [Fact]
    public void Predefined_functions_are_recognized()
    {
        var expectedNode = new FunctionNode("SIN") { Children = new AstNode[] { new ValueNode("Number", 5.0) } };
        AssertFormula.SingleNodeParsed("SIN(5)", expectedNode);
    }

    [Fact]
    public void Function_can_have_whitespaces_around_braces()
    {
        var expectedNode = new FunctionNode("SIN") { Children = new AstNode[] { new ValueNode("Number", 5.0) } };
        AssertFormula.SingleNodeParsed("SIN(  5  )", expectedNode);
    }

    [Fact]
    public void Function_can_be_from_another_sheet()
    {
        var expectedNode = new FunctionNode("Sheet", "Func") { Children = new AstNode[] { new ValueNode("Number", 5.0) } };
        AssertFormula.SingleNodeParsed("Sheet!Func(5)", expectedNode);
    }

    [Fact]
    public void Function_can_be_from_another_workbook()
    {
        var expectedNode = new ExternalFunctionNode(2, null, "Func") { Children = new AstNode[] { new ValueNode("Number", 5.0) } };
        AssertFormula.SingleNodeParsed("[2]!Func(5)", expectedNode);
    }

    /// <summary>
    /// <c>LOG10</c> is a cell in A1 too, so <c>LOG10(</c> is lexed as a cell function. A cell function is a
    /// construct of a macro sheet, and Excel reads it as the function.
    /// </summary>
    [Theory]
    [InlineData("LOG10(5)", "LOG10")]
    [InlineData("log10( 5 )", "log10")]
    public void Log10_is_a_function_although_it_is_a_cell_too(string formula, string functionName)
    {
        var expectedNode = new FunctionNode(functionName) { Children = new AstNode[] { new ValueNode("Number", 5.0) } };
        AssertFormula.SingleNodeParsed(formula, expectedNode);
    }

    [Fact]
    public void Function_can_be_cell_function()
    {
        var expectedNode = new CellFunctionNode(new RowCol(true, 3, false, 2, A1)) { Children = new AstNode[] { new ValueNode("Number", 5.0) } };
        AssertFormula.SingleNodeParsed("B$3(5)", expectedNode);
    }
}