using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using CsvHelper;
using CsvHelper.Configuration;

namespace ClosedXML.Parser.Benchmarks;

/// <summary>
/// A data set of formulas from real workbooks. The data set tests check that each of its
/// formulas parses, except the ones listed in its <c>known-fails.csv</c>.
/// </summary>
public enum DataSetName
{
    Enron,
    Euses,
}

/// <summary>
/// A sample of the formulas of a data set, in both reference styles. A benchmark that runs over
/// the sample declares <see cref="Size"/> as its <c>OperationsPerInvoke</c>, so its results are
/// per formula.
/// </summary>
internal sealed class FormulaSample
{
    public const int Size = 10_000;

    /// <summary>
    /// The row of the cell that the R1C1 formulas are relative to.
    /// </summary>
    public const int AnchorRow = 1000;

    /// <summary>
    /// The column of the cell that the R1C1 formulas are relative to.
    /// </summary>
    public const int AnchorCol = 100;

    private FormulaSample(string[] a1, string[] r1c1)
    {
        A1 = a1;
        R1C1 = r1c1;
    }

    public string[] A1 { get; }

    /// <summary>
    /// The formulas of <see cref="A1"/>, in the same order, converted to R1C1 at the anchor cell.
    /// </summary>
    public string[] R1C1 { get; }

    public static FormulaSample Load(DataSetName dataSet)
    {
        var directory = Path.Combine(GetDataDirectory(), dataSet.ToString().ToLowerInvariant());
        var knownFails = new HashSet<string>(ReadCsv(Path.Combine(directory, "known-fails.csv")));
        var formulas = ReadCsv(Path.Combine(directory, "formulas.csv"))
            .Where(formula => !knownFails.Contains(formula))
            .ToList();

        // Take a formula from each of Size equal slices of the data set. Consecutive formulas
        // often come from one workbook, so the first Size formulas would cover few workbooks.
        var a1 = new string[Size];
        var r1c1 = new string[Size];
        var stride = formulas.Count / Size;
        var index = 0;
        for (var i = 0; i < Size; i++)
        {
            index = Math.Max(index, i * stride);
            string? r1c1Formula;
            while (!TryConvertToR1C1(formulas[index], out r1c1Formula))
            {
                if (++index == formulas.Count)
                    throw new InvalidOperationException($"The {dataSet} data set has fewer than {Size} formulas that every benchmark can run on.");
            }

            a1[i] = formulas[index];
            r1c1[i] = r1c1Formula;
            index++;
        }

        return new FormulaSample(a1, r1c1);
    }

    /// <summary>
    /// Every benchmark must be able to run on each formula of the sample, so the formula must
    /// parse in A1 and, converted to R1C1, parse in R1C1 too.
    /// </summary>
    private static bool TryConvertToR1C1(string formulaA1, [NotNullWhen(true)] out string? formulaR1C1)
    {
        try
        {
            _ = FormulaParser<ScalarValue, AstNode, Ctx>.CellFormulaA1(formulaA1, new Ctx(), new F());
            formulaR1C1 = FormulaConverter.ToR1C1(formulaA1, AnchorRow, AnchorCol);
            _ = FormulaConverter.ToA1(formulaR1C1, AnchorRow, AnchorCol);
            return true;
        }
        catch (ParsingException)
        {
            formulaR1C1 = null;
            return false;
        }
    }

    private static IEnumerable<string> ReadCsv(string path)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = false };
        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, config);
        while (csv.Read())
            yield return csv.GetField(0)!;
    }

    private static string GetDataDirectory()
    {
        return typeof(FormulaSample).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "DataDirectory")
            .Value!;
    }
}
