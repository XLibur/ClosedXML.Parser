using BenchmarkDotNet.Attributes;

namespace ClosedXML.Parser.Benchmarks;

/// <summary>
/// Convert and modify the formulas of a data set sample. Each of these parses the formula with a
/// <see cref="FormulaModifier"/> and writes the formula text again.
/// </summary>
[MemoryDiagnoser]
public class FormulaConverterBenchmarks
{
    private static readonly RenameSheetModifier s_renameSheet = new("Sheet1", "Renamed sheet");

    private string[] _formulasA1 = null!;
    private string[] _formulasR1C1 = null!;

    [Params(DataSetName.Enron, DataSetName.Euses)]
    public DataSetName DataSet { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var sample = FormulaSample.Load(DataSet);
        _formulasA1 = sample.A1;
        _formulasR1C1 = sample.R1C1;
    }

    [Benchmark(OperationsPerInvoke = FormulaSample.Size)]
    public string ToR1C1()
    {
        var converted = string.Empty;
        foreach (var formula in _formulasA1)
            converted = FormulaConverter.ToR1C1(formula, FormulaSample.AnchorRow, FormulaSample.AnchorCol);

        return converted;
    }

    [Benchmark(OperationsPerInvoke = FormulaSample.Size)]
    public string ToA1()
    {
        var converted = string.Empty;
        foreach (var formula in _formulasR1C1)
            converted = FormulaConverter.ToA1(formula, FormulaSample.AnchorRow, FormulaSample.AnchorCol);

        return converted;
    }

    [Benchmark(OperationsPerInvoke = FormulaSample.Size)]
    public string ModifyA1RenameSheet()
    {
        var modified = string.Empty;
        foreach (var formula in _formulasA1)
            modified = FormulaConverter.ModifyA1(formula, "Sheet", FormulaSample.AnchorRow, FormulaSample.AnchorCol, s_renameSheet);

        return modified;
    }

    private sealed class RenameSheetModifier : FormulaModifier
    {
        private readonly string _oldName;
        private readonly string _newName;

        public RenameSheetModifier(string oldName, string newName)
        {
            _oldName = oldName;
            _newName = newName;
        }

        protected override string? ModifySheet(ModContext ctx, string sheetName)
        {
            return string.Equals(sheetName, _oldName, StringComparison.OrdinalIgnoreCase) ? _newName : sheetName;
        }
    }
}
