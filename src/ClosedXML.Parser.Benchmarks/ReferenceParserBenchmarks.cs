using BenchmarkDotNet.Attributes;

namespace ClosedXML.Parser.Benchmarks;

/// <summary>
/// Parse single references and names, the way a caller reads a cell address or a defined name.
/// </summary>
[MemoryDiagnoser]
public class ReferenceParserBenchmarks
{
    [Benchmark]
    public bool TryParseA1Cell() => ReferenceParser.TryParseA1("B7", out _);

    [Benchmark]
    public bool TryParseA1Area() => ReferenceParser.TryParseA1("$B$2:$D$10", out _);

    [Benchmark]
    public bool TryParseA1ColumnSpan() => ReferenceParser.TryParseA1("$D:$G", out _);

    /// <summary>
    /// Text that is not a reference. A caller that does not know what the text is gets this path.
    /// </summary>
    [Benchmark]
    public bool TryParseA1NotReference() => ReferenceParser.TryParseA1("SUM(A1)", out _);

    [Benchmark]
    public bool TryParseA1SheetArea() => ReferenceParser.TryParseA1("'Jane''s sheet'!$A$1:$C$10", out _, out _);

    [Benchmark]
    public bool TryParseR1C1Area() => ReferenceParser.TryParseR1C1("R[-1]C[2]:R5C8", out _);

    [Benchmark]
    public bool TryParseSheetName() => ReferenceParser.TryParseName("Sheet1!Profit", out _, out _);
}
