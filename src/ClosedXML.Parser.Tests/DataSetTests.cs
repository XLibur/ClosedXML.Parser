using System.Diagnostics;
using Xunit.Abstractions;

namespace ClosedXML.Parser.Tests;

public class DataSetTests
{
    private readonly ITestOutputHelper _output;

    public DataSetTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Enron_data_set_is_parseable()
    {
        Assert_formulas_parsed_or_not_as_expected(
            "./data/enron/formulas.csv",
            new[]
            {
                "./data/enron/invalid-external-cell-reference.csv",
                "./data/enron/known-fails.csv",
            });
    }

    [Fact]
    public void Euses_data_set_is_parseable()
    {
        Assert_formulas_parsed_or_not_as_expected(
            "./data/euses/formulas.csv",
            new[]
            {
                "./data/euses/invalid-external-cell-reference.csv",
                "./data/euses/known-fails.csv",
            });
    }

    [Fact]
    public void Contributions_data_set_is_parseable()
    {
        Assert_formulas_parsed_or_not_as_expected("./data/contributions/formulas.csv", Array.Empty<string>());
    }

    private void Assert_formulas_parsed_or_not_as_expected(string input, string[] badFormulaPaths)
    {
        var badFormulas = new HashSet<string>();
        foreach (var badFormulaPath in badFormulaPaths)
            badFormulas.UnionWith(DataSets.ReadCsv(badFormulaPath));

        // Read to memory before the parsing to measure only parsing.
        var formulas = DataSets.ReadCsv(input).ToList();
        var sw = Stopwatch.StartNew();
        var formulaCount = 0;
        foreach (var formula in formulas)
        {
            formulaCount++;
            string? error = null;
            try
            {
                _ = FormulaParser<ScalarValue, AstNode, Ctx>.CellFormulaA1(formula, new Ctx(), new F());
            }
            catch (Exception e)
            {
                error = e.Message;
            }

            // Assert outside of the try block. An assert inside it throws, the catch would take that for
            // a parsing failure, and a formula listed as failing would pass even when it was parsed.
            if (badFormulas.Contains(formula))
                Assert.True(error is not null, $"Formula '{formula}' is listed as failing, but it was parsed.");
            else
                Assert.True(error is null, $"Parsing formula '{formula}' failed: {error}");
        }

        sw.Stop();
        var averageLength = formulas.Sum(x => x.Length) / (double)formulas.Count;
        _output.WriteLine($"Parsed {formulaCount} formulas (Average length {averageLength:F1}) in {sw.ElapsedMilliseconds}ms ({sw.ElapsedMilliseconds * 1000d / formulaCount:N3}μs/formula).");
    }
}