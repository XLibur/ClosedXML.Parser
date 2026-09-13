namespace ClosedXML.Parser.Tests.Rules;

public class RefIntersectionExpressionRuleTests
{
    [Theory]
    [MemberData(nameof(TestCases))]
    public void Has_one_or_more_elements_separated_by_space(string formula, AstNode expectedNode)
    {
        AssertFormula.SingleNodeParsed(formula, expectedNode);
    }

    [Fact]
    public void Intersection_operator_has_lower_priority_than_implicit_intersection()
    {
        var expectedNode =
            new UnaryNode(
                UnaryOperation.ImplicitIntersection,
                new BinaryNode(
                    BinaryOperation.Intersection,
                    new ReferenceNode(new ReferenceArea(new RowCol(1, 1, A1), new RowCol(10, 1, A1))),
                    new ReferenceNode(new ReferenceArea(5, 1, A1))));
        AssertFormula.SingleNodeParsed("@A1:A10 A5", expectedNode);
    }

    /// <summary>
    /// The lexer puts the whitespace after an operator into its token, e.g. the <c>)</c> of <c>(A1) B2</c> is lexed
    /// as <c>) </c>. There is no <c>SPACE</c> token for the space, but it is the intersection operator all the same.
    /// </summary>
    [Theory]
    [MemberData(nameof(SpaceInPreviousTokenTestCases))]
    public void Space_a_token_took_is_still_the_intersection_operator(string formula, AstNode expectedNode)
    {
        AssertFormula.SingleNodeParsed(formula, expectedNode);
        AssertFormula.CstParsed(formula);
    }

    /// <summary>
    /// The space a token took is the intersection operator only when a reference follows it.
    /// </summary>
    [Fact]
    public void Space_a_token_took_is_an_operator_only_before_a_reference()
    {
        var expectedNode =
            new BinaryNode(
                BinaryOperation.Addition,
                new ReferenceNode(new ReferenceArea(1, 1, A1)),
                new ReferenceNode(new ReferenceArea(2, 2, A1)));
        AssertFormula.SingleNodeParsed("(A1) + B2", expectedNode);
        AssertFormula.CstParsed("(A1) + B2");
    }

    public static IEnumerable<object[]> SpaceInPreviousTokenTestCases
    {
        get
        {
            // The CLOSE_BRACE token of a ref_atom_expression in braces.
            yield return new object[]
            {
                "(A1) B2",
                new BinaryNode(BinaryOperation.Intersection,
                    new ReferenceNode(new ReferenceArea(1, 1, A1)),
                    new ReferenceNode(new ReferenceArea(2, 2, A1)))
            };

            // The same in an argument, where the parser skips the union operator.
            yield return new object[]
            {
                "SUM((A1) B2)",
                new FunctionNode("SUM")
                {
                    Children = new AstNode[]
                    {
                        new BinaryNode(BinaryOperation.Intersection,
                            new ReferenceNode(new ReferenceArea(1, 1, A1)),
                            new ReferenceNode(new ReferenceArea(2, 2, A1)))
                    }
                }
            };

            // A range in the braces.
            yield return new object[]
            {
                "(A1:B2) B2",
                new BinaryNode(BinaryOperation.Intersection,
                    new ReferenceNode(new ReferenceArea(new RowCol(1, 1, A1), new RowCol(2, 2, A1))),
                    new ReferenceNode(new ReferenceArea(2, 2, A1)))
            };

            // A union in the braces.
            yield return new object[]
            {
                "(A1,B3) B2:C3",
                new BinaryNode(BinaryOperation.Intersection,
                    new BinaryNode(BinaryOperation.Union,
                        new ReferenceNode(new ReferenceArea(1, 1, A1)),
                        new ReferenceNode(new ReferenceArea(3, 2, A1))),
                    new ReferenceNode(new ReferenceArea(new RowCol(2, 2, A1), new RowCol(3, 3, A1))))
            };

            // The example of the comment that explains the backtracking in FormulaParser.
            yield return new object[]
            {
                "(((A1))) A1:B2",
                new BinaryNode(BinaryOperation.Intersection,
                    new ReferenceNode(new ReferenceArea(1, 1, A1)),
                    new ReferenceNode(new ReferenceArea(new RowCol(1, 1, A1), new RowCol(2, 2, A1))))
            };

            // The SPILL token of a spill range.
            yield return new object[]
            {
                "A1# B2",
                new BinaryNode(BinaryOperation.Intersection,
                    new UnaryNode(UnaryOperation.SpillRange, new ReferenceNode(new ReferenceArea(1, 1, A1))),
                    new ReferenceNode(new ReferenceArea(2, 2, A1)))
            };

            // The CLOSE_BRACE token of a call of a function that returns a reference.
            yield return new object[]
            {
                "INDEX(A1:B2,1,1) B2",
                new BinaryNode(BinaryOperation.Intersection,
                    new FunctionNode("INDEX")
                    {
                        Children = new AstNode[]
                        {
                            new ReferenceNode(new ReferenceArea(new RowCol(1, 1, A1), new RowCol(2, 2, A1))),
                            new ValueNode(1),
                            new ValueNode(1)
                        }
                    },
                    new ReferenceNode(new ReferenceArea(2, 2, A1)))
            };

            // The INTRA_TABLE_REFERENCE token of a structured reference.
            yield return new object[]
            {
                "[Col] B2",
                new BinaryNode(BinaryOperation.Intersection,
                    new StructureReferenceNode(null, StructuredReferenceArea.None, "Col", "Col"),
                    new ReferenceNode(new ReferenceArea(2, 2, A1)))
            };
        }
    }

    public static IEnumerable<object[]> TestCases
    {
        get
        {
            // ref_intersection_expression : ref_range_expression
            yield return new object[]
            {
                "A1",
                new ReferenceNode(new ReferenceArea(1, 1, A1))
            };

            // ref_intersection_expression : ref_range_expression SPACE ref_range_expression
            yield return new object[]
            {
                "A1 A2",
                new BinaryNode(BinaryOperation.Intersection,
                    new ReferenceNode(new ReferenceArea(1, 1, A1)),
                    new ReferenceNode(new ReferenceArea(2, 1, A1)))
            };

            // ref_intersection_expression : ref_range_expression SPACE ref_range_expression
            yield return new object[]
            {
                " A1   A2   A3  ",
                new BinaryNode(BinaryOperation.Intersection,
                    new BinaryNode(BinaryOperation.Intersection,
                        new ReferenceNode(new ReferenceArea(1, 1, A1)),
                        new ReferenceNode(new ReferenceArea(2, 1, A1))),
                    new ReferenceNode(new ReferenceArea(3, 1, A1)))
            };
        }
    }
}