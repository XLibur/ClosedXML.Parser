namespace ClosedXML.Parser.Tests.Rules;

public class DdeReferenceRuleTests
{
    [Theory]
    [InlineData("[1]!'id1?req?AAPL_STK_SMART_USD_~/'", 1, "id1?req?AAPL_STK_SMART_USD_~/")]
    [InlineData("[2]!'NGc5 cls'", 2, "NGc5 cls")]
    [InlineData("[1]!'NBP,LAST,1'", 1, "NBP,LAST,1")]
    [InlineData("[3]!'It''s'", 3, "It's")]
    public void Item_after_book_prefix_is_recognized(string formula, int workbookIndex, string item)
    {
        AssertFormula.SingleNodeParsed(formula, new ExternalDynamicDataExchangeNode(workbookIndex, item));
        AssertFormula.CstParsed(formula);
    }

    [Theory]
    [InlineData("Sdemo123|tik!'id1?req?AAPL_STK_SMART_USD_~/'", "Sdemo123", "tik", "id1?req?AAPL_STK_SMART_USD_~/")]
    [InlineData("'My App|Topic 1'!'a''b'", "My App", "Topic 1", "a'b")]
    [InlineData("App|Top|ic!'x'", "App", "Top|ic", "x")]
    public void Item_after_application_and_topic_is_recognized(string formula, string application, string topic, string item)
    {
        AssertFormula.SingleNodeParsed(formula, new DynamicDataExchangeNode(application, topic, item));
        AssertFormula.CstParsed(formula);
    }

    [Theory]
    [InlineData("[1]!'id1?req'")]
    [InlineData("Sdemo123|tik!'id1?req'")]
    public void Item_is_read_the_same_in_both_reference_styles(string formula)
    {
        var a1 = FormulaParser<ScalarValue, AstNode, Ctx>.CellFormulaA1(formula, new Ctx(), new F());
        var r1c1 = FormulaParser<ScalarValue, AstNode, Ctx>.CellFormulaR1C1(formula, new Ctx(), new F());
        Assert.Equal(a1, r1c1);
    }

    [Fact]
    public void Item_is_an_operand()
    {
        var node = FormulaParser<ScalarValue, AstNode, Ctx>.CellFormulaA1("[1]!'SGKR,LA'/100", new Ctx(), new F());

        var division = Assert.IsType<BinaryNode>(node);
        Assert.Equal(new ExternalDynamicDataExchangeNode(1, "SGKR,LA"), division.Children[0]);
    }

    [Theory]
    [InlineData("Sheet1!'item'")]
    [InlineData("|tik!'item'")]
    [InlineData("App|!'item'")]
    [InlineData("[1]App|tik!'item'")]
    public void Item_needs_book_prefix_or_application_and_topic(string formula)
    {
        AssertFormula.CheckParsingErrorContains(formula, "dynamic data exchange");
    }

    [Theory]
    [InlineData("'item'")]
    [InlineData("A1+'item'")]
    public void Item_without_prefix_is_not_a_reference(string formula)
    {
        Assert.Throws<ParsingException>(() => FormulaParser<ScalarValue, AstNode, Ctx>.CellFormulaA1(formula, new Ctx(), new F()));
    }

    [Theory]
    [InlineData("a|b!SomeName", "a|b")]
    [InlineData("'a|b'!SomeName", "a|b")]
    public void Sheet_name_with_bar_is_still_a_sheet(string formula, string sheet)
    {
        AssertFormula.SingleNodeParsed(formula, new SheetNameNode(sheet, "SomeName"));
    }
}
