namespace ClosedXML.Parser.Tests.Rules;

/// <summary>
/// The implicit intersection operator <c>@</c> binds looser than the range and the intersection operators, so
/// wherever <c>@</c> starts an operand, its operand is the rest of the intersection. Excel displays a legacy
/// implicit intersection that way, e.g. <c>ABS(@A1:A10)</c> takes the implicit intersection of the whole range.
/// </summary>
public class RefImplicitExpressionRuleTests
{
    [Fact]
    public void Implicit_intersection_operator_has_lower_priority_than_range()
    {
        var expectedNode =
            new UnaryNode(
                UnaryOperation.ImplicitIntersection,
                new BinaryNode(
                    BinaryOperation.Range,
                    new ReferenceNode(new ReferenceArea(new RowCol(1, 1, A1), new RowCol(2, 1, A1))),
                    new ReferenceNode(new ReferenceArea(3, 1, A1))));
        AssertFormula.SingleNodeParsed("@A1:A2:A3", expectedNode);
    }

    [Fact]
    public void Implicit_intersection_can_be_an_argument()
    {
        var expectedNode = new FunctionNode("SUM")
        {
            Children = new AstNode[]
            {
                new UnaryNode(
                    UnaryOperation.ImplicitIntersection,
                    new ReferenceNode(new ReferenceArea(new RowCol(1, 1, A1), new RowCol(4, 1, A1))))
            }
        };
        AssertFormula.SingleNodeParsed("SUM(@A1:A4)", expectedNode);
        AssertFormula.CstParsed("SUM(@A1:A4)");
    }

    [Fact]
    public void Implicit_intersection_after_range_operator_takes_the_rest_of_the_range()
    {
        var expectedNode =
            new BinaryNode(
                BinaryOperation.Range,
                new ReferenceNode(new ReferenceArea(3, 4, A1)),
                new UnaryNode(
                    UnaryOperation.ImplicitIntersection,
                    new ReferenceNode(new ReferenceArea(new RowCol(1, 1, A1), new RowCol(2, 3, A1)))));
        AssertFormula.SingleNodeParsed("D3:@A1:C2", expectedNode);
        AssertFormula.CstParsed("D3:@A1:C2");
    }

    /// <summary>
    /// The lexer puts the space before <c>@</c> into the <c>INTERSECT</c> token, so there is no <c>SPACE</c>
    /// token, but the space is still the intersection operator.
    /// </summary>
    [Fact]
    public void Implicit_intersection_can_follow_intersection_operator()
    {
        var expectedNode =
            new BinaryNode(
                BinaryOperation.Intersection,
                new ReferenceNode(new ReferenceArea(new RowCol(1, 1, A1), new RowCol(2, 2, A1))),
                new UnaryNode(
                    UnaryOperation.ImplicitIntersection,
                    new ReferenceNode(new ReferenceArea(new RowCol(1, 3, A1), new RowCol(9, 3, A1)))));
        AssertFormula.SingleNodeParsed("A1:B2 @C1:C9", expectedNode);
        AssertFormula.CstParsed("A1:B2 @C1:C9");
    }

    [Fact]
    public void Implicit_intersection_after_intersection_operator_takes_the_rest_of_the_intersection()
    {
        var expectedNode =
            new BinaryNode(
                BinaryOperation.Intersection,
                new ReferenceNode(new ReferenceArea(1, 1, A1)),
                new UnaryNode(
                    UnaryOperation.ImplicitIntersection,
                    new BinaryNode(
                        BinaryOperation.Intersection,
                        new ReferenceNode(new ReferenceArea(1, 2, A1)),
                        new ReferenceNode(new ReferenceArea(1, 3, A1)))));
        AssertFormula.SingleNodeParsed("A1 @B1 C1", expectedNode);
        AssertFormula.CstParsed("A1 @B1 C1");
    }

    [Fact]
    public void Implicit_intersection_binds_tighter_than_union()
    {
        var expectedNode =
            new BinaryNode(
                BinaryOperation.Union,
                new BinaryNode(
                    BinaryOperation.Range,
                    new ReferenceNode(new ReferenceArea(3, 4, A1)),
                    new UnaryNode(
                        UnaryOperation.ImplicitIntersection,
                        new ReferenceNode(new ReferenceArea(new RowCol(1, 1, A1), new RowCol(2, 3, A1))))),
                new ReferenceNode(new ReferenceArea(1, 5, A1)));
        AssertFormula.SingleNodeParsed("D3:@A1:C2,E1", expectedNode);
        AssertFormula.CstParsed("D3:@A1:C2,E1");
    }

    /// <summary>
    /// <c>@</c> is a prefix operator. Without a space before it, there is no intersection operator to join it to
    /// the reference before it.
    /// </summary>
    [Theory]
    [InlineData("A1@B1", "A1", "@B1")]
    [InlineData("A1:B2@C1:C9", "A1:B2", "@C1:C9")]
    public void At_sign_without_space_before_it_does_not_follow_a_reference(string formula, string parsed, string rest)
    {
        AssertFormula.CheckParsingErrorContains(formula, $"The expression `{parsed}` was parsed, but the rest `{rest}` wasn't.");
        AssertFormula.CstNotParsed(formula);
    }

    /// <summary>
    /// The parser reads a reference in braces as an expression first and then goes back to the reference rules
    /// with the braces already read. An <c>@</c> after them can't be their prefix.
    /// </summary>
    [Theory]
    [InlineData("(A1) @:B1", "the rest `@:B1` wasn't.")]
    [InlineData("SUM((A1) @:B1)", "Unexpected token INTERSECT")]
    public void At_sign_after_reference_in_braces_is_not_its_prefix(string formula, string error)
    {
        AssertFormula.CheckParsingErrorContains(formula, error);
        AssertFormula.CstNotParsed(formula);
    }

    /// <summary>
    /// A closing brace and the spill operator take the whitespace after them into their token, so there is no space
    /// before the <c>@</c> that could be the intersection operator.
    /// </summary>
    [Theory]
    [InlineData("(A1) @B1")]
    [InlineData("A1# @B1")]
    public void Space_after_closing_brace_or_spill_is_not_intersection_operator(string formula)
    {
        AssertFormula.CheckParsingErrorContains(formula, "the rest `@B1` wasn't.");
        AssertFormula.CstNotParsed(formula);
    }
}
