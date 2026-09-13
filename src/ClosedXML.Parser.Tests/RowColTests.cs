using static ClosedXML.Parser.ReferenceAxisType;

namespace ClosedXML.Parser.Tests;

public class RowColTests
{
    [Fact]
    public void Default_struct_is_A1()
    {
        RowCol a = default;

        Assert.Equal(Relative, a.ColumnType);
        Assert.Equal(1, a.ColumnValue);
        Assert.Equal(Relative, a.RowType);
        Assert.Equal(1, a.RowValue);
        Assert.Equal(A1, a.Style);
    }

    [Theory]
    [InlineData("RC", 1, 1, "A1")]
    [InlineData("RC[-5]", 1, 4, "XFC1")]
    [InlineData("RC[-4]", 1, 4, "XFD1")]
    [InlineData("RC[-3]", 1, 4, "A1")]
    [InlineData("RC[2]", 1, 16382, "XFD1")]
    [InlineData("RC[3]", 1, 16382, "A1")]
    [InlineData("RC[4]", 1, 16382, "B1")]
    [InlineData("R[0]C", 1, 1, "A1")]
    [InlineData("R[-3]C", 4, 1, "A1")]
    [InlineData("R[-4]C", 4, 1, "A1048576")]
    [InlineData("R[-5]C", 4, 1, "A1048575")]
    [InlineData("R[1]C", 1048575, 1, "A1048576")]
    [InlineData("R[2]C", 1048575, 1, "A1")]
    public void ToA1_loops_for_out_of_bounds_reference(string r1c1, int row, int col, string a1)
    {
        // In GUI, Excel loops over, if user enters out-of-bounds reference to a formula.
        Assert.True(ReferenceParser.TryParseR1C1(r1c1, out var areaR1C1));
        var refR1C1 = areaR1C1.First;
        var refA1 = ReferenceParser.ParseA1(a1).First;
        Assert.Equal(refA1, refR1C1.ToA1(row, col));
    }

    /// <summary>
    /// A column above the <c>XFD</c> of a sheet is still written out. The constructor takes any
    /// <see cref="int"/> as a column, so a caller can hold one no sheet has, and writing it used
    /// to be the only thing that said so - by producing letters, not by refusing.
    /// </summary>
    /// <remarks>
    /// <c>ZZZ</c> is column 18278, the last one three letters spell, and <c>XFD</c> is 16384, so
    /// the three columns between them are out of a sheet and still three letters long. The buffer
    /// the letters are written through holds seven, which is what <see cref="int.MaxValue"/>
    /// spells.
    /// </remarks>
    [Theory]
    [InlineData(1, "A")]
    [InlineData(16384, "XFD")]
    [InlineData(16385, "XFE")]
    [InlineData(18278, "ZZZ")]
    [InlineData(18279, "AAAA")]
    [InlineData(475254, "ZZZZ")]
    [InlineData(475255, "AAAAA")]
    [InlineData(int.MaxValue, "FXSHRXW")]
    public void A_column_above_the_sheet_is_still_written(int columnValue, string letters)
    {
        var rowCol = new RowCol(Relative, 1, Relative, columnValue, A1);

        Assert.Equal(letters + "1", rowCol.GetDisplayStringA1());
    }

    /// <summary>
    /// A relative column converted with an anchor is written even when it lands above a sheet.
    /// <see cref="RowCol.ToA1"/> subtracts one sheet width rather than taking a modulo, which its
    /// comment says is enough for a reference the grammar produced, so an offset built by hand
    /// stays above the sheet and has to be written rather than refused.
    /// </summary>
    [Fact]
    public void A_relative_column_converted_above_the_sheet_is_still_written()
    {
        var relative = new RowCol(Relative, 1, Relative, 1_000_000, R1C1);

        var converted = relative.ToA1(1, 1);

        Assert.Equal(983_617, converted.ColumnValue);
        Assert.Equal("BCYAK2", converted.GetDisplayStringA1());
    }
}