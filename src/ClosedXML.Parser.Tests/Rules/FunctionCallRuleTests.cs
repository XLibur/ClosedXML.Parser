namespace XLibur.Parser.Tests.Rules;

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

    [Fact]
    public void Function_call_can_be_called()
    {
        var lambda = new FunctionNode("LAMBDA")
        {
            Children = new AstNode[]
            {
                new NameNode("x"),
                new BinaryNode(BinaryOperation.Multiplication) { Children = new AstNode[] { new NameNode("x"), new ValueNode("Number", 2.0) } }
            }
        };
        var expectedNode = new ExpressionCallNode { Children = new AstNode[] { lambda, new ValueNode("Number", 3.0) } };
        AssertFormula.SingleNodeParsed("LAMBDA(x,x*2)(3)", expectedNode);
    }

    [Fact]
    public void Expression_in_braces_can_be_called()
    {
        var expectedNode = new ExpressionCallNode
        {
            Children = new AstNode[]
            {
                new FunctionNode("LAMBDA") { Children = new AstNode[] { new NameNode("x"), new NameNode("x") } },
                new ValueNode("Number", 3.0)
            }
        };
        AssertFormula.SingleNodeParsed("(LAMBDA(x,x))(3)", expectedNode);
    }

    [Fact]
    public void Call_can_be_called_again()
    {
        var inner = new ExpressionCallNode
        {
            Children = new AstNode[]
            {
                new FunctionNode("LAMBDA") { Children = new AstNode[] { new NameNode("x"), new FunctionNode("LAMBDA") { Children = new AstNode[] { new NameNode("y"), new NameNode("y") } } } },
                new ValueNode("Number", 1.0)
            }
        };
        var expectedNode = new ExpressionCallNode { Children = new AstNode[] { inner, new ValueNode("Number", 2.0) } };
        AssertFormula.SingleNodeParsed("LAMBDA(x,LAMBDA(y,y))(1)(2)", expectedNode);
    }

    [Theory]
    [InlineData("LAMBDA(x,x*2)(3)")]
    [InlineData("_xlfn.LAMBDA(_xlpm.x,_xlpm.x*2)(3)")]
    [InlineData("LAMBDA(x,x)()")]
    [InlineData("LAMBDA(x,x)(,)")]
    [InlineData("(LAMBDA(x,x))(3)")]
    [InlineData("LAMBDA(x,LAMBDA(y,y))(1)(2)")]
    [InlineData("1+LAMBDA(x,x)(3)*2")]
    [InlineData("SUM(LAMBDA(x,x)(3),4)")]
    [InlineData("Sheet!Func(1)(2)")]
    [InlineData("[1]!Func(1)(2)")]
    [InlineData("IF(TRUE,LAMBDA(x,x),LAMBDA(x,-x))(3)")]
    [InlineData("CHOOSE(1,LAMBDA(x,x))(1)(2)")]
    [InlineData("1+IF(A1,f,g)(3)*2")]
    // A reference function that isn't called goes on as a reference.
    [InlineData("IF(TRUE,A1,B1):C3")]
    [InlineData("IF(TRUE,A1,B1) (C1)")]
    [InlineData("SUM(INDEX(A1:B2,1):B2)")]
    public void Both_parsers_accept_a_called_expression(string formula)
    {
        AssertFormula.CstParsed(formula);
        FormulaParser<ScalarValue, AstNode, Ctx>.CellFormulaA1(formula, new Ctx(), new F());
    }

    /// <summary>
    /// Whitespace before the brace makes it an intersection, so a value is not called.
    /// </summary>
    [Theory]
    [InlineData("LAMBDA(x,x) (3)")]
    [InlineData("(LAMBDA(x,x)) (3)")]
    [InlineData("5(3)")]
    [InlineData("\"a\"(3)")]
    public void Only_adjacent_braces_call_a_function_or_nested_expression(string formula)
    {
        AssertFormula.CstNotParsed(formula);
        Assert.Throws<ParsingException>(() => FormulaParser<ScalarValue, AstNode, Ctx>.CellFormulaA1(formula, new Ctx(), new F()));
    }
}
