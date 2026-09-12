using ClosedXML.Parser.Visualizer.Diagram;

namespace ClosedXML.Parser.Visualizer.Parsing;

public static class FormulaAnalyzer
{
    /// <summary>
    /// Parse a cell formula and build its diagram.
    /// </summary>
    public static ParseResult Parse(string formula, ReferenceStyle style)
    {
        try
        {
            var factory = new RangeRecordingFactory();
            var root = style == ReferenceStyle.R1C1
                ? FormulaParser<ScalarValue, AstNode, Ctx>.CellFormulaR1C1(formula, new Ctx(), factory)
                : FormulaParser<ScalarValue, AstNode, Ctx>.CellFormulaA1(formula, new Ctx(), factory);
            return new ParsedFormula(DiagramBuilder.Build(root, factory.Ranges, style));
        }
        catch (ParsingException e)
        {
            return new FailedParse(e.Message, ParseErrorPosition.Find(formula, e.Message));
        }
        catch (Exception e)
        {
            // Any other exception is a bug in the parser. Show it, because this page is where
            // people find such bugs.
            return new FailedParse($"The parser failed with {e.GetType().Name}: {e.Message}", null);
        }
    }
}
