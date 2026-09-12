using BenchmarkDotNet.Attributes;

namespace ClosedXML.Parser.Benchmarks;

/// <summary>
/// Check sheet names. A writer of formula text asks <see cref="NameUtils.ShouldQuote"/> for each
/// sheet prefix it writes.
/// </summary>
[MemoryDiagnoser]
public class NameUtilsBenchmarks
{
    [Params("Sheet1", "Jane's sheet", "Q1 2024 - Revenue (final)")]
    public string SheetName { get; set; } = null!;

    [Benchmark]
    public bool ShouldQuote() => NameUtils.ShouldQuote(SheetName.AsSpan());

    [Benchmark]
    public bool IsSheetNameValid() => NameUtils.IsSheetNameValid(SheetName.AsSpan());
}
