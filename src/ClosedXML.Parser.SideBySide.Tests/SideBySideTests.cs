using System.Reflection;
using Xunit;

namespace XLibur.Parser.SideBySide.Tests;

/// <summary>
/// ClosedXML and XLibur each load their own parser into one process. The upstream parser and
/// this fork used to share an assembly identity, so whichever loaded first was the only one, and
/// the other library failed with a <see cref="TypeLoadException"/> the first time it built a
/// formula parser (issue #59).
/// </summary>
public class SideBySideTests
{
    [Fact]
    public void Parsers_have_distinct_assembly_names()
    {
        var upstream = typeof(global::ClosedXML.Parser.ReferenceArea).Assembly.GetName();
        var fork = typeof(global::XLibur.Parser.ReferenceArea).Assembly.GetName();

        Assert.Equal("ClosedXML.Parser", upstream.Name);
        Assert.Equal("XLibur.ClosedXML.Parser", fork.Name);
    }

    [Fact]
    public void Both_parsers_parse_a_reference()
    {
        var upstream = global::ClosedXML.Parser.ReferenceParser.ParseA1("B3");
        var fork = global::XLibur.Parser.ReferenceParser.ParseA1("B3");

        Assert.Equal(3, upstream.First.RowValue);
        Assert.Equal(3, fork.First.RowValue);
    }

    [Fact]
    public void ClosedXML_evaluates_a_formula()
    {
        using var workbook = new global::ClosedXML.Excel.XLWorkbook();
        var sheet = workbook.AddWorksheet("Sheet1");
        sheet.Cell("A1").Value = 2;
        sheet.Cell("A2").FormulaA1 = "A1*3";

        Assert.Equal(6, sheet.Cell("A2").Value.GetNumber());
        AssertLoadedParser(typeof(global::ClosedXML.Excel.XLWorkbook).Assembly, "ClosedXML.Parser");
    }

    // XLibur 0.600.0 was compiled against the parser's old assembly name, so it binds to the
    // upstream parser and fails. Remove the skip once XLibur.Bundle is on a release built
    // against XLibur.ClosedXML.Parser with the new name, and bump its version in the project.
    [Fact(Skip = "Needs an XLibur release built against the renamed parser assembly (issue #59).")]
    public void XLibur_evaluates_a_formula()
    {
        using var workbook = new global::XLibur.Excel.XLWorkbook();
        var sheet = workbook.AddWorksheet("Sheet1");
        sheet.Cell("A1").Value = 2;
        sheet.Cell("A2").FormulaA1 = "A1*3";

        Assert.Equal(6, sheet.Cell("A2").Value.GetNumber());
        AssertLoadedParser(typeof(global::XLibur.Excel.XLWorkbook).Assembly, "XLibur.ClosedXML.Parser");
    }

    private static void AssertLoadedParser(Assembly library, string parserName)
    {
        Assert.Contains(library.GetReferencedAssemblies(), reference => reference.Name == parserName);
    }
}
