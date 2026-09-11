using static ClosedXML.Parser.ReferenceAxisType;

namespace ClosedXML.Parser.Tests;

public class ReferenceParserTests
{
    [Theory]
    [MemberData(nameof(ParseA1TestCases))]
    public void ParseA1_parses_cell_area_or_rowspan_or_colspan(string text, ReferenceArea expectedReference)
    {
        Assert.Equal(expectedReference, ReferenceParser.ParseA1(text));
    }

    [Fact]
    public void ParseA1_requires_argument()
    {
        Assert.Throws<ArgumentNullException>(() => ReferenceParser.ParseA1(null!));
    }

    [Fact]
    public void ParseA1_throws_on_non_references()
    {
        Assert.Throws<ParsingException>(() => ReferenceParser.ParseA1("HELLO"));
    }

    [Theory]
    [MemberData(nameof(ParseA1TestCases))]
    public void TryParseA1_parses_cell_area_or_rowspan_or_colspan(string text, ReferenceArea expectedReference)
    {
        var success = ReferenceParser.TryParseA1(text, out var area);
        Assert.True(success);
        Assert.Equal(expectedReference, area);
    }

    [Fact]
    public void TryParseA1_requires_argument()
    {
        Assert.Throws<ArgumentNullException>(() => ReferenceParser.TryParseA1(null!, out _));
    }

    [Fact]
    public void ParseA1_returns_false_on_non_references()
    {
        var success = ReferenceParser.TryParseA1("HELLO", out var area);
        Assert.False(success);
        Assert.Equal(default, area);
    }

    [Theory]
    [MemberData(nameof(ParseR1C1TestCases))]
    public void TryParseR1C1_parses_cell_area_or_rowspan_or_colspan(string text, ReferenceArea expectedReference)
    {
        var success = ReferenceParser.TryParseR1C1(text, out var area);
        Assert.True(success);
        Assert.Equal(expectedReference, area);
    }

    [Fact]
    public void TryParseR1C1_requires_argument()
    {
        Assert.Throws<ArgumentNullException>(() => ReferenceParser.TryParseR1C1(null!, out _));
    }

    [Fact]
    public void TryParseR1C1_returns_false_on_non_references()
    {
        var success = ReferenceParser.TryParseR1C1("HELLO", out var area);
        Assert.False(success);
        Assert.Equal(default, area);
    }

    /// <summary>
    /// A written axis number is absolute and starts at 1. Only a missing number (<c>R</c>,
    /// <c>C</c>) and a bracketed zero (<c>R[0]</c>, <c>C[0]</c>) mean an axis relative to the
    /// current cell. The R1C1 lexer admits a bare <c>C0</c>, so the reader has to reject it;
    /// a bare <c>R0</c> the lexer already refuses.
    /// </summary>
    [Theory]
    [InlineData("C0")]
    [InlineData("R1C0")]
    [InlineData("C0:C2")]
    [InlineData("R0")]
    [InlineData("R0C0")]
    [InlineData("R0C1")]
    [InlineData("R0:R2")]
    public void TryParseR1C1_rejects_a_written_axis_number_of_zero(string text)
    {
        var success = ReferenceParser.TryParseR1C1(text, out var area);
        Assert.False(success);
        Assert.Equal(default, area);
    }

    [Theory]
    [InlineData("R", Relative, 0, None, 0)]
    [InlineData("C", None, 0, Relative, 0)]
    [InlineData("RC", Relative, 0, Relative, 0)]
    [InlineData("R[0]", Relative, 0, None, 0)]
    [InlineData("C[0]", None, 0, Relative, 0)]
    [InlineData("R[0]C[0]", Relative, 0, Relative, 0)]
    public void TryParseR1C1_still_accepts_a_relative_zero_axis(string text, ReferenceAxisType rowType, int row, ReferenceAxisType colType, int col)
    {
        var success = ReferenceParser.TryParseR1C1(text, out var area);

        Assert.True(success);
        Assert.Equal(new ReferenceArea(new RowCol(rowType, row, colType, col, R1C1)), area);
    }

    /// <summary>
    /// Malformed UTF-16 used to reach the shared lexer's surrogate handling and throw out of
    /// it - an out of range read for a trailing high surrogate, and an out of range argument
    /// to <c>char.ConvertToUtf32</c> for a mismatched one. A try-parse has to report that as
    /// a failure to parse.
    /// </summary>
    /// <remarks>
    /// The cases are built here rather than passed as <c>InlineData</c>. xUnit serializes
    /// theory arguments, a lone surrogate does not survive that round trip, and two cases
    /// that differ only in their surrogates end up with the same test id - so one of them is
    /// silently dropped and the rest never see malformed input at all.
    /// </remarks>
    [Fact]
    public void TryParse_returns_false_on_malformed_utf16()
    {
        const char high = '\uD83D';
        const char low = '\uDE00';
        (string Case, string Text)[] cases =
        {
            ("high surrogate alone", $"{high}"),
            ("low surrogate alone", $"{low}"),
            ("high surrogate after a reference", $"R1C1{high}"),
            ("high surrogate after an A1 reference", $"A1{high}"),
            ("high surrogate followed by a letter", $"{high}A"),
            ("high surrogate inside a reference", $"R{high}C"),
            ("two high surrogates", $"{high}{high}"),
            ("low surrogate before a high one", $"{low}{high}"),
        };

        foreach (var (name, text) in cases)
        {
            Assert.False(ReferenceParser.TryParseR1C1(text, out var r1c1), name);
            Assert.Equal(default, r1c1);

            Assert.False(ReferenceParser.TryParseA1(text, out var a1), name);
            Assert.Equal(default, a1);
        }
    }

    [Fact]
    public void TryParse_still_accepts_a_paired_surrogate_in_a_sheet_name()
    {
        var success = ReferenceParser.TryParseSheetA1("'\uD83D\uDE00'!A1", out var sheet, out var area);

        Assert.True(success);
        Assert.Equal("\uD83D\uDE00", sheet);
        Assert.Equal(new ReferenceArea(new RowCol(Relative, 1, Relative, 1, A1)), area);
    }

    [Fact]
    public void TryParseR1C1_reads_the_text_in_R1C1_not_A1()
    {
        // C7 is a cell in A1 and a whole column in R1C1.
        Assert.True(ReferenceParser.TryParseA1("C7", out var a1));
        Assert.True(ReferenceParser.TryParseR1C1("C7", out var r1c1));
        Assert.NotEqual(a1, r1c1);
        Assert.Equal(new ReferenceArea(new RowCol(None, 0, Absolute, 7, R1C1)), r1c1);
    }

    [Theory]
    [MemberData(nameof(ParseSheetA1TestCases))]
    public void TryParseSheetA1_accepts_area_or_rowspan_or_colspan_with_sheet(string text, string expectedSheet, ReferenceArea expectedArea)
    {
        var success = ReferenceParser.TryParseSheetA1(text, out var sheet, out var area);
        Assert.True(success);
        Assert.Equal(expectedSheet, sheet);
        Assert.Equal(expectedArea, area);
    }

    [Fact]
    public void TryParseSheetA1_cant_parse_workbook_index()
    {
        var success = ReferenceParser.TryParseSheetA1("[1]Sheet!A1", out _, out _);
        Assert.False(success);
    }

    [Fact]
    public void TryParseSheetA1_cant_parse_reference_without_sheet()
    {
        var success = ReferenceParser.TryParseSheetA1("A1", out _, out _);
        Assert.False(success);
    }

    [Fact]
    public void TryParseSheetA1_requires_argument()
    {
        Assert.Throws<ArgumentNullException>(() => ReferenceParser.TryParseSheetA1(null!, out _, out _));
    }

    [Theory]
    [InlineData("Sheet!Name", "Sheet", "Name")]
    [InlineData("'Hello World'!Name", "Hello World", "Name")]
    [InlineData("' John''s World! '!Name", " John's World! ", "Name")]
    public void TryParseSheetName_parses_sheet_and_name(string text, string expectedSheet, string expectedName)
    {
        var success = ReferenceParser.TryParseSheetName(text, out var sheet, out var name);

        Assert.True(success);
        Assert.Equal(expectedSheet, sheet);
        Assert.Equal(expectedName, name);
    }

    [Fact]
    public void TryParseSheetName_requires_text()
    {
        Assert.Throws<ArgumentNullException>(() => ReferenceParser.TryParseSheetName(null!, out _, out _));
    }

    [Theory]
    [InlineData("Name")]
    [InlineData("some_name")]
    [InlineData("A1")]
    public void TryParseSheetName_cant_parse_pure_name(string text)
    {
        var success = ReferenceParser.TryParseSheetName(text, out _, out _);
        Assert.False(success);
    }

    [Theory]
    [InlineData("Sheet!Name", "Sheet", "Name")]
    [InlineData("'Hello World'!Data", "Hello World", "Data")]
    [InlineData("' John''s World! '!Quarter1", " John's World! ", "Quarter1")]
    public void TryParseName_parses_sheet_and_name(string text, string expectedSheet, string expectedName)
    {
        var success = ReferenceParser.TryParseName(text, out var sheet, out var name);
        Assert.True(success);
        Assert.Equal(expectedSheet, sheet);
        Assert.Equal(expectedName, name);
    }

    [Fact]
    public void TryParseName_requires_text()
    {
        Assert.Throws<ArgumentNullException>(() => ReferenceParser.TryParseName(null!, out _, out _));
    }

    [Theory]
    [InlineData("Name")]
    [InlineData("some_name")]
    public void TryParseName_parses_name(string text)
    {
        var success = ReferenceParser.TryParseName(text, out var sheet, out var name);
        Assert.True(success);
        Assert.Null(sheet);
        Assert.Equal(text, name);
    }

    [Theory]
    [InlineData("A1")]
    [InlineData("$BC$1")]
    [InlineData("Sheet!A1")]
    [InlineData("14")]
    [InlineData("\"Text\"")]
    public void TryParseName_cant_parse_anything_but_name(string text)
    {
        var success = ReferenceParser.TryParseName(text, out _, out _);
        Assert.False(success);
    }

    [Theory]
    [MemberData(nameof(ParseA1TestCases))]
    public void TryParseA1_unified_can_parse_local_reference(string text, ReferenceArea expectedReference)
    {
        var success = ReferenceParser.TryParseA1(text, out var sheet, out var reference);
        Assert.True(success);
        Assert.Null(sheet);
        Assert.Equal(expectedReference, reference);
    }

    [Theory]
    [MemberData(nameof(ParseSheetA1TestCases))]
    public void TryParseA1_unified_can_parse_sheet_reference(string text, string expectedSheet, ReferenceArea expectedReference)
    {
        var success = ReferenceParser.TryParseA1(text, out var sheet, out var reference);
        Assert.True(success);
        Assert.Equal(expectedSheet, sheet);
        Assert.Equal(expectedReference, reference);
    }

    [Theory]
    [InlineData("Sheet!Name")]
    [InlineData("Name")]
    [InlineData("1")]
    public void TryParseA1_unified_cant_parse_anything_but_reference(string text)
    {
        var success = ReferenceParser.TryParseA1(text, out _, out _);
        Assert.False(success);
    }

    [Fact]
    public void TryParseA1_unified_requires_argument()
    {
        Assert.Throws<ArgumentNullException>(() => ReferenceParser.TryParseA1(null!, out _, out _));
    }

    public static IEnumerable<object[]> ParseSheetA1TestCases
    {
        get
        {
            yield return new object[]
            {
                "Sheet!$C$2",
                "Sheet",
                new ReferenceArea(new RowCol(Absolute, 2, Absolute, 3, A1)),
            };
            yield return new object[]
            {
                "' ''John''s'' Shop! '!C2",
                " 'John's' Shop! ",
                new ReferenceArea(new RowCol(Relative, 2, Relative, 3, A1)),
            };
            yield return new object[]
            {
                "Sheet!A1:B2",
                "Sheet",
                new ReferenceArea(new RowCol(Relative, 1, Relative, 1, A1), new RowCol(Relative, 2, Relative, 2, A1)),
            };
            yield return new object[]
            {
                "'Some Sheet'!C:D",
                "Some Sheet",
                new ReferenceArea(new RowCol(None, 0, Relative, 3, A1), new RowCol(None, 0, Relative, 4, A1)),
            };
            yield return new object[]
            {
                "'!!WARN'!10:$15",
                "!!WARN",
                new ReferenceArea(new RowCol(Relative, 10, None, 0, A1), new RowCol(Absolute, 15, None, 0, A1)),
            };
        }
    }

    public static IEnumerable<object[]> ParseR1C1TestCases
    {
        get
        {
            yield return new object[]
            {
                "R7C3",
                new ReferenceArea(new RowCol(Absolute, 7, Absolute, 3, R1C1)),
            };
            yield return new object[]
            {
                "R[-1]C",
                new ReferenceArea(new RowCol(Relative, -1, Relative, 0, R1C1)),
            };
            yield return new object[]
            {
                "C75",
                new ReferenceArea(new RowCol(None, 0, Absolute, 75, R1C1)),
            };
            yield return new object[]
            {
                "R1C1:R[2]C[2]",
                new ReferenceArea(new RowCol(Absolute, 1, Absolute, 1, R1C1), new RowCol(Relative, 2, Relative, 2, R1C1)),
            };
        }
    }

    public static IEnumerable<object[]> ParseA1TestCases
    {
        get
        {
            yield return new object[]
            {
                "$C$2",
                new ReferenceArea(new RowCol(Absolute, 2, Absolute, 3, A1)),
            };
            yield return new object[]
            {
                "AB123",
                new ReferenceArea(new RowCol(Relative, 123, Relative, 28, A1)),
            };
            yield return new object[]
            {
                "$C$2:E7",
                new ReferenceArea(new RowCol(Absolute, 2, Absolute, 3, A1), new RowCol(Relative, 7, Relative, 5, A1)),
            };
            yield return new object[]
            {
                "$C:F",
                new ReferenceArea(new RowCol(None, 0, Absolute, 3, A1), new RowCol(None, 0, Relative, 6, A1)),
            };
            yield return new object[]
            {
                "10:$15",
                new ReferenceArea(new RowCol(Relative, 10, None, 0, A1), new RowCol(Absolute, 15, None, 0, A1)),
            };
        }
    }
}
