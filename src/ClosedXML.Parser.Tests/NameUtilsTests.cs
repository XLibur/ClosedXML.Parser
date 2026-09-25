using System.Collections;
using System.Globalization;
using System.Text;
using Xunit.Abstractions;

namespace XLibur.Parser.Tests;

public class NameUtilsTests
{
    private readonly ITestOutputHelper _output;

    public NameUtilsTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// The first sheet of a bare 3D reference stands where nothing has said a sheet prefix has
    /// started yet, so a name that is also a cell has to be quoted there although
    /// <see cref="NameUtils.ShouldQuote"/> leaves it bare. The name is written without knowing the
    /// reference style of the formula it ends up in, so it has to be a name in both.
    /// </summary>
    [Theory]
    [InlineData("Sheet1", false)] // A name in both styles
    [InlineData("Jan", false)]
    [InlineData("LOG", false)] // A function name is still a name
    [InlineData("PWD1", true)] // A cell in A1: column PWD, row 1
    [InlineData("LOG10", true)]
    [InlineData("XFD1048576", true)] // The last cell of a sheet
    [InlineData("R1C1", true)] // A cell in R1C1
    [InlineData("C", true)] // A whole column in R1C1
    [InlineData("R", true)]
    [InlineData("My Sheet", true)] // Needs quotes on its own, so it needs them here
    [InlineData("Jane's", true)]
    [InlineData("TRUE", true)]
    public void Name_should_be_quoted_as_the_first_sheet_of_a_3d_reference_when_it_is_also_a_cell(string sheetName, bool shouldBeQuoted)
    {
        Assert.Equal(shouldBeQuoted, NameUtils.ShouldQuoteAsFirstSheet(sheetName.AsSpan()));
    }

    [Theory]
    [InlineData("A", false)] // First char - Letter-like
    [InlineData("Z", false)]
    [InlineData("㝳", false)]
    [InlineData("㏝", false)]
    [InlineData(" ", true)] // First char - non-letter-like
    [InlineData(".", true)]
    [InlineData("^", true)]
    [InlineData("!", true)]
    [InlineData("+", true)]
    [InlineData("0", true)]
    [InlineData("㏞", true)]
    [InlineData("😁", true)] // Symbol, non base multilingual plane
    [InlineData("AZ", false)] // Subsequent char - letter-like
    [InlineData("A0", false)] // Subsequent char - number-like
    [InlineData("A.", false)] // Subsequent char - symbol-like
    [InlineData("A!", true)] // Subsequent char - other
    [InlineData("A+A", true)]
    [InlineData("A😁", true)]
    public void Name_should_be_quoted_when_first_char_is_letter_and_rest_letter_number_or_symbol(string sheetName, bool shouldBeQuoted)
    {
        Assert.Equal(shouldBeQuoted, NameUtils.ShouldQuote(sheetName));
    }

    [Theory]
    // Unquoted, each of these made Excel report the workbook as corrupt and refuse to
    // open it. Excel's formula bar shows them without quotes, which is why a table
    // collected from the formula bar marked them as needing none.
    //
    // Which position is fatal differs per codepoint, and these cases pin that down
    // rather than assert a blanket rule: the separators and bidi controls only break a
    // name they start, while the isolates break one anywhere. The companion below
    // asserts the other half - that the tolerated positions stay unquoted.
    [InlineData("\u2028abc")] // U+2028 LINE SEPARATOR, first only
    [InlineData("\u2029abc")] // U+2029 PARAGRAPH SEPARATOR, first only
    [InlineData("\u202Aabc")] // U+202A LEFT-TO-RIGHT EMBEDDING, first only
    [InlineData("\u202Eabc")] // U+202E RIGHT-TO-LEFT OVERRIDE, first only
    [InlineData("\u303Eabc")] // U+303E WAVY DASH, first only
    [InlineData("\u303Dabc")] // U+303D PART ALTERNATION MARK, any position
    [InlineData("abc\u303D")]
    [InlineData("a\u303Dbc")]
    [InlineData("abc\u2065")] // U+2065 unassigned in Unicode, any position
    [InlineData("abc\u2066")] // U+2066 LEFT-TO-RIGHT ISOLATE, any position
    [InlineData("abc\u2069")] // U+2069 POP DIRECTIONAL ISOLATE, any position
    [InlineData("a\u2066bc")]
    public void Name_excel_cannot_read_unquoted_from_a_file_is_quoted(string sheetName)
    {
        Assert.True(NameUtils.ShouldQuote(sheetName));
    }

    [Theory]
    // The other half of the rule above. Excel writes these bare and reads them back, so
    // quoting them would be a guess rather than a measurement - verified by opening a
    // workbook that stores each one unquoted.
    [InlineData("abc\u2028")] // U+2028 LINE SEPARATOR
    [InlineData("a\u2028bc")]
    [InlineData("abc\u2029")] // U+2029 PARAGRAPH SEPARATOR
    [InlineData("abc\u202A")] // U+202A LEFT-TO-RIGHT EMBEDDING
    [InlineData("a\u202Ebc")] // U+202E RIGHT-TO-LEFT OVERRIDE
    [InlineData("abc\u303E")] // U+303E WAVY DASH
    public void Name_excel_reads_unquoted_in_a_later_position_is_not_quoted(string sheetName)
    {
        Assert.False(NameUtils.ShouldQuote(sheetName));
    }

    [Theory]
    // Excel writes these quoted, though it still reads them unquoted. Quoting matches
    // what Excel itself stores, and costs nothing.
    [InlineData("abc～")] // FULLWIDTH TILDE, the character from issue #29
    [InlineData("abc　")] // IDEOGRAPHIC SPACE
    [InlineData("abc、")] // IDEOGRAPHIC COMMA
    [InlineData("abc（")] // FULLWIDTH LEFT PARENTHESIS
    [InlineData("abc￥")] // FULLWIDTH YEN SIGN
    [InlineData("‘abc")] // LEFT SINGLE QUOTATION MARK
    public void Name_excel_writes_quoted_is_quoted(string sheetName)
    {
        Assert.True(NameUtils.ShouldQuote(sheetName));
    }

    [Theory]
    [InlineData("TRUE")]
    [InlineData("FALSE")]
    [InlineData("true")]
    [InlineData("False")]
    public void Name_of_a_logical_literal_is_quoted(string sheetName)
    {
        // Every character here is fine on its own, so only the whole name gives it away.
        // Unquoted, Excel reads TRUE!A1 as a logical literal and refuses to open the file.
        Assert.True(NameUtils.ShouldQuote(sheetName));
    }

    [Theory]
    [InlineData("TRUEISH")]
    [InlineData("FALSEHOOD")]
    [InlineData("TRU")]
    public void Name_that_merely_starts_like_a_logical_literal_is_not_quoted(string sheetName)
    {
        Assert.False(NameUtils.ShouldQuote(sheetName));
    }

    [Theory]
    // Unquoted, each of these made Excel refuse to open the workbook (#62). Excel reads an R1C1
    // reference at the start of the name whatever the reference style of the file, so the name
    // stops being one token: R5 and then Z.
    [InlineData("R5Z")]
    [InlineData("C5A")]
    [InlineData("C05A")]
    [InlineData("R1CX")]
    [InlineData("RC1X")]
    [InlineData("R2C3X")]
    [InlineData("R1C1Z")]
    // Excel quotes the rest of the family the same way, whatever follows and in any case.
    [InlineData("r5z")]
    [InlineData("c1x")]
    [InlineData("Rc5x")]
    [InlineData("R5_")]
    [InlineData("R5.x")]
    [InlineData("R5Z9")]
    [InlineData("R01X")] // Row 1
    [InlineData("R1R")]
    [InlineData("C1C")]
    [InlineData("R2CX5")]
    [InlineData("R1C1R1C1")]
    [InlineData("R1C0X")] // R1 is enough, although C0 is no column
    [InlineData("R1C16385X")] // R1 is enough, although C16385 is past the last column
    [InlineData("R5C0X")]
    [InlineData("R1048576X")] // The last row
    [InlineData("C16384X")] // The last column
    public void Name_starting_with_an_R1C1_reference_is_quoted(string sheetName)
    {
        Assert.True(NameUtils.ShouldQuote(sheetName));
    }

    [Theory]
    // Excel quotes a name that is a reference, in either style. Unquoted these still load, but
    // quoting them is what Excel stores (#62).
    [InlineData("A1")]
    [InlineData("a1")]
    [InlineData("A01")]
    [InlineData("AB12")]
    [InlineData("CA1")]
    [InlineData("XR1")]
    [InlineData("LOG10")]
    [InlineData("XFD1048576")]
    [InlineData("xfd1048576")]
    [InlineData("R1C1")]
    [InlineData("R2C3")]
    [InlineData("R1")]
    [InlineData("RC1")]
    [InlineData("C05")]
    [InlineData("R2C")]
    [InlineData("R")]
    [InlineData("r")]
    [InlineData("C")]
    [InlineData("RC")]
    [InlineData("rc")]
    public void Name_that_is_a_reference_is_quoted(string sheetName)
    {
        Assert.True(NameUtils.ShouldQuote(sheetName));
    }

    [Theory]
    // Excel stores each of these bare (#62).
    [InlineData("A1B")] // An A1 cell at the start doesn't matter; only an R1C1 one does
    [InlineData("A01B")]
    [InlineData("AB12C")]
    [InlineData("LOG10X")]
    [InlineData("RX")] // R, C and RC need a number to start a reference
    [InlineData("CX")]
    [InlineData("RCX")]
    [InlineData("R0X")] // No row 0
    [InlineData("C0X")]
    [InlineData("RC0X")]
    [InlineData("R0C5X")] // A bad row isn't passed over to read the column
    [InlineData("R0C5")]
    [InlineData("R1048577X")] // Past the last row
    [InlineData("C16385X")] // Past the last column
    [InlineData("C99999X")]
    [InlineData("C99999999999X")]
    [InlineData("R9999999999C5X")]
    [InlineData("R99999999999999999999X")]
    [InlineData("XFE1")] // Past the last column, so no cell
    [InlineData("A1048577")] // Past the last row
    [InlineData("A0")]
    [InlineData("AAAA1")]
    [InlineData("ÀR1")]
    public void Name_that_only_resembles_a_reference_is_not_quoted(string sheetName)
    {
        Assert.False(NameUtils.ShouldQuote(sheetName));
    }

    [Theory]
    [InlineData("", "it's", "'it''s'")]
    [InlineData("SUM(", "it's", "SUM('it''s'")]
    [InlineData("SUM(Alpha!A1,", "it's", "SUM(Alpha!A1,'it''s'")]
    [InlineData("SUM(", "a'b'c", "SUM('a''b''c'")]
    [InlineData("=", "a'", "='a'''")]
    public void Escaped_name_doubles_apostrophes_regardless_of_builder_content(string prefix, string sheet, string expected)
    {
        // The apostrophes to double start where the sheet name was appended, not at index 1
        // of the builder. Every current caller passes an empty builder, which hid the
        // difference; a caller that appends a formula fragment first does not.
        var sb = new StringBuilder(prefix);

        NameUtils.EscapeName(sb, sheet);

        Assert.Equal(expected, sb.ToString());
    }

    [Fact]
    public void Empty_name_is_not_valid_sheet_name()
    {
        Assert.False(NameUtils.IsSheetNameValid(string.Empty));
    }

    [Fact]
    public void Sheet_name_can_have_at_most_31_chars()
    {
        Assert.True(NameUtils.IsSheetNameValid(new string('A', 31)));
        Assert.False(NameUtils.IsSheetNameValid(new string('A', 32)));
    }

    [Theory]
    [InlineData("*", false)]
    [InlineData("/", false)]
    [InlineData(":", false)]
    [InlineData("?", false)]
    [InlineData("[", false)]
    [InlineData("\\", false)]
    [InlineData("]", false)]
    [InlineData("name", true)]
    [InlineData(" ", true)]
    public void Sheet_name_without_forbidden_chars(string name, bool isValid)
    {
        Assert.Equal(isValid, NameUtils.IsSheetNameValid(name));
    }

    /// <summary>
    /// The collected quoting tables have no answer for an apostrophe in the first position, because
    /// Excel can't make a sheet name that starts with one, so the name came back needing no quotes
    /// and was written bare. <see cref="NameUtils.ShouldQuote"/> answers for a DDE application and
    /// topic as well, and those can start with an apostrophe, so it has to say the text needs quotes
    /// rather than have the writer emit it unquoted.
    /// </summary>
    [Theory]
    [InlineData("'leading", true)]
    [InlineData("'", true)]
    [InlineData("'both'", true)]
    [InlineData("trailing'", true)]
    [InlineData("Jane's", true)]
    [InlineData("plain", false)]
    public void Name_starting_with_an_apostrophe_should_be_quoted(string name, bool shouldBeQuoted)
    {
        Assert.Equal(shouldBeQuoted, NameUtils.ShouldQuote(name));
    }

    /// <summary>
    /// Excel refuses a sheet name that starts or ends with an apostrophe, and the library has its own
    /// reason to agree: a sheet prefix is quoted with apostrophes, so one at either end has nowhere to
    /// go. A leading one is worse than unreadable - <see cref="NameUtils.ShouldQuote"/> says such a
    /// name needs no quotes, so it is written bare, as <c>'leading!</c>, which no lexer reads at all.
    /// An apostrophe anywhere else is ordinary and is doubled inside the quotes.
    /// </summary>
    [Theory]
    [InlineData("'leading", false)]
    [InlineData("trailing'", false)]
    [InlineData("'both'", false)]
    [InlineData("'", false)]
    [InlineData("''", false)]
    [InlineData("Jane's", true)]
    [InlineData("a'b", true)]
    [InlineData("a''b", true)]
    public void Sheet_name_cant_start_or_end_with_an_apostrophe(string name, bool isValid)
    {
        Assert.Equal(isValid, NameUtils.IsSheetNameValid(name));
    }

    /// <summary>
    /// Excel refuses a sheet name containing any of these, so the data files leave them
    /// out entirely rather than record an answer that could never be exercised.
    /// </summary>
    private static readonly int[] ForbiddenInSheetName = { '*', '/', ':', '?', '[', '\\', ']' };

    /// <summary>
    /// An apostrophe is refused only in the first position, so it is left out of the first
    /// table and kept in the other. <see cref="NameUtils.ShouldQuote"/> answers for it before
    /// it reaches the mask, because a quoted name is what a leading apostrophe has to be
    /// written as and Excel can't be asked.
    /// </summary>
    private const int ForbiddenFirstInSheetName = '\'';

    [Theory]
    [InlineData("ident-sheet-first.txt")]
    [InlineData("ident-sheet-next.txt")]
    public void Quotation_data_holds_one_row_per_codepoint_a_sheet_name_can_contain(string path)
    {
        // The comparison below only reaches codepoints the file actually lists, so on its
        // own a truncated or partly deleted file passes while the masks keep stale values.
        // Pin the coverage down first.
        var isFirst = path == "ident-sheet-first.txt";
        var codepoints = ReadQuotationData(path).Select(entry => entry.Codepoint).ToList();
        var expected = Enumerable.Range(1, 0xFFFF)
            .Where(cp => !ForbiddenInSheetName.Contains(cp) && !(isFirst && cp == ForbiddenFirstInSheetName));

        Assert.Equal(codepoints.Count, codepoints.Distinct().Count());
        Assert.Equal(expected, codepoints.OrderBy(codepoint => codepoint));
    }

    [Fact]
    public void Quotation_bitmasks_match_the_data_they_were_generated_from()
    {
        // The bitmasks in NameUtils are a compiled copy of the two data files. Nothing
        // rebuilds them automatically, so without this the two can drift apart and the
        // only symptom is a workbook Excel refuses to open. The names are checked for their
        // characters alone, because "a1" or "Ra" is quoted for its shape (#62), not for a character.
        var mismatches = new List<string>();
        foreach (var (codepoint, shouldQuote) in ReadQuotationData("ident-sheet-first.txt"))
        {
            if (NameUtils.ShouldQuoteForCharacters(((char)codepoint) + "a") != shouldQuote)
                mismatches.Add($"first U+{codepoint:X4}");
        }

        foreach (var (codepoint, shouldQuote) in ReadQuotationData("ident-sheet-next.txt"))
        {
            // ShouldQuote answers true for any surrogate before it reaches the mask,
            // because a codepoint outside the BMP is always quoted.
            if (codepoint is >= 0xD800 and <= 0xDFFF)
                continue;

            if (NameUtils.ShouldQuoteForCharacters("a" + (char)codepoint) != shouldQuote)
                mismatches.Add($"next U+{codepoint:X4}");
        }

        Assert.Empty(mismatches);
    }

    private static IEnumerable<(int Codepoint, bool ShouldQuote)> ReadQuotationData(string path)
    {
        foreach (var line in File.ReadAllLines(path))
        {
            if (line.Length == 0)
                continue;

            yield return (int.Parse(line[..4], NumberStyles.HexNumber), line[5..] switch
            {
                "YES" => true,
                "NO" => false,
                _ => throw new NotSupportedException(line)
            });
        }
    }

    [Theory(Skip = "Used to generate bitmask for the quotation.")]
    [InlineData(@"ident-sheet-first.txt")]
    [InlineData(@"ident-sheet-next.txt")]
    public void Generate_sheet_quotation_data(string path)
    {
        // Files were generated by driving Excel over COM: name a sheet after each codepoint,
        // reference it from another sheet, save as .xlsx and read the stored formula back out
        // of the XML. Reading the formula from the *formula bar* instead, as the original
        // AutoHotKey collection did, produces a different and more permissive answer - Excel
        // displays ABC～!A1 but stores 'ABC～'!A1 - and a table built that way writes formulas
        // Excel then refuses to load. See tools/sheet-quotation for the scripts.
        // There is no obvious pattern. Codepoints from Unicode 5.2+ are always quoted,
        // at least for BMP. Other than that, MS folk creativity at work.
        // The invalid code points for sheet name are not in the file: * 2A,/ 2F,: 3A,? 3F,[ 5B,\ 5C,] 5D
        var codepointQuoted = new BitArray(0x10000);
        foreach (var line in File.ReadAllLines(path))
        {
            var codepoint = int.Parse(line[..4], NumberStyles.HexNumber);
            var isQuoted = line[5..] switch
            {
                "YES" => true,
                "NO" => false,
                _ => throw new NotSupportedException(line)
            };
            codepointQuoted[codepoint] = isQuoted;
        }

        var intArray = new int[65536 / 32];
        codepointQuoted.CopyTo(intArray, 0);
        _output.WriteLine(string.Join(", ", intArray.Select(x => "0x" + x.ToString("X8"))));
    }
}