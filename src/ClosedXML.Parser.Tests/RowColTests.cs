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
    /// Every column of a sheet is written out, up to the <c>XFD</c> of column 16384. That is the
    /// widest column the type holds, so three letters are all a column ever spells.
    /// </summary>
    [Theory]
    [InlineData(1, "A")]
    [InlineData(26, "Z")]
    [InlineData(27, "AA")]
    [InlineData(702, "ZZ")]
    [InlineData(703, "AAA")]
    [InlineData(16384, "XFD")]
    public void Every_column_of_a_sheet_is_written(int columnValue, string letters)
    {
        var rowCol = new RowCol(Relative, 1, Relative, columnValue, A1);

        Assert.Equal(letters + "1", rowCol.GetDisplayStringA1());
    }

    /// <summary>
    /// An <em>A1</em> position is a position in a sheet, whether or not it is absolute, so both
    /// axes are held to what a sheet has: rows 1 to 1048576 and columns 1 to 16384.
    /// </summary>
    [Theory]
    [InlineData(Relative, 0, Relative, 1)]
    [InlineData(Relative, 1048577, Relative, 1)]
    [InlineData(Absolute, -1, Relative, 1)]
    [InlineData(Relative, int.MaxValue, Relative, 1)]
    [InlineData(Relative, 1, Relative, 0)]
    [InlineData(Relative, 1, Relative, 16385)]
    [InlineData(Relative, 1, Absolute, -1)]
    [InlineData(Relative, 1, Relative, int.MaxValue)]
    public void An_A1_position_outside_the_sheet_is_refused(
        ReferenceAxisType rowType, int rowValue, ReferenceAxisType columnType, int columnValue)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new RowCol(rowType, rowValue, columnType, columnValue, A1));
    }

    /// <summary>
    /// An absolute <em>R1C1</em> axis names a position in a sheet, the same as an <em>A1</em> one.
    /// </summary>
    [Theory]
    [InlineData(Absolute, 0, Relative, 0)]
    [InlineData(Absolute, 1048577, Relative, 0)]
    [InlineData(Relative, 0, Absolute, 0)]
    [InlineData(Relative, 0, Absolute, 16385)]
    public void An_absolute_R1C1_position_outside_the_sheet_is_refused(
        ReferenceAxisType rowType, int rowValue, ReferenceAxisType columnType, int columnValue)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new RowCol(rowType, rowValue, columnType, columnValue, R1C1));
    }

    /// <summary>
    /// A relative <em>R1C1</em> axis is an offset from the formula's own cell. The furthest one
    /// cell can be from another in a sheet is one short of the sheet, so an offset reaches
    /// 1048575 rows and 16383 columns either way and no further, which is exactly what the
    /// grammar admits.
    /// </summary>
    [Theory]
    [InlineData(1048576, 0)]
    [InlineData(-1048576, 0)]
    [InlineData(0, 16384)]
    [InlineData(0, -16384)]
    [InlineData(int.MaxValue, 0)]
    [InlineData(int.MinValue, 0)]
    public void A_relative_R1C1_offset_wider_than_a_sheet_is_refused(int rowValue, int columnValue)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new RowCol(Relative, rowValue, Relative, columnValue, R1C1));
    }

    /// <summary>
    /// A <see cref="FormulaModifier"/> shifting references is the one caller that legitimately
    /// builds a <see cref="RowCol"/> by hand, so it needs a way to tell an off-sheet shift from a
    /// valid one without catching an exception in the middle of a rewrite. Both the bounds and the
    /// way to test them are part of the public surface, not just visible to this test project.
    /// </summary>
    [Theory]
    [InlineData(nameof(RowCol.MinRow))]
    [InlineData(nameof(RowCol.MaxRow))]
    [InlineData(nameof(RowCol.MinCol))]
    [InlineData(nameof(RowCol.MaxCol))]
    public void The_bounds_of_a_sheet_are_public(string name)
    {
        var field = typeof(RowCol).GetField(name);

        Assert.NotNull(field);
        Assert.True(field.IsPublic, $"RowCol.{name} has to be public for a caller to read it.");
    }

    /// <summary>
    /// The bounds say what a sheet holds, which is fixed by the file format.
    /// </summary>
    [Fact]
    public void The_bounds_of_a_sheet_are_the_sheet()
    {
        Assert.Equal(1, RowCol.MinRow);
        Assert.Equal(1048576, RowCol.MaxRow);
        Assert.Equal(1, RowCol.MinCol);
        Assert.Equal(16384, RowCol.MaxCol);
    }

    /// <summary>
    /// <c>TryCreate</c> answers what the constructor would do and hands back what it would have
    /// built, so a caller neither repeats the rule nor pays for an exception to learn the answer.
    /// </summary>
    [Theory]
    [InlineData(Absolute, 1048576, Absolute, 16384, A1)]
    [InlineData(Relative, 1, Relative, 1, A1)]
    [InlineData(Relative, 1048575, Relative, 16383, R1C1)]
    [InlineData(Relative, -1048575, Relative, -16383, R1C1)]
    [InlineData(Relative, 0, None, 0, R1C1)]
    [InlineData(None, 0, Absolute, 7, A1)]
    public void TryCreate_builds_what_the_constructor_builds(
        ReferenceAxisType rowType, int rowValue, ReferenceAxisType columnType, int columnValue, ReferenceStyle style)
    {
        Assert.True(RowCol.TryCreate(rowType, rowValue, columnType, columnValue, style, out var rowCol));
        Assert.Equal(new RowCol(rowType, rowValue, columnType, columnValue, style), rowCol);
    }

    /// <summary>
    /// Everything the constructor refuses, for any of its three reasons, <c>TryCreate</c> refuses
    /// too — the two read one rule, so they can't drift apart.
    /// </summary>
    [Theory]
    [InlineData(Relative, 0, Relative, 1, A1)] // A position outside the sheet
    [InlineData(Relative, 1, Relative, 16385, A1)]
    [InlineData(Relative, int.MaxValue, Relative, 1, A1)]
    [InlineData(Absolute, 1048577, Relative, 0, R1C1)]
    [InlineData(Relative, 1048576, Relative, 0, R1C1)] // An offset wider than the sheet
    [InlineData(Relative, 0, Relative, -16384, R1C1)]
    [InlineData(Relative, int.MinValue, Relative, 0, R1C1)]
    [InlineData(None, 0, None, 0, A1)] // Neither axis is an axis
    [InlineData(None, 5, Relative, 1, A1)] // A None axis carrying a value
    [InlineData(Relative, 1, None, 5, A1)]
    public void TryCreate_refuses_what_the_constructor_refuses(
        ReferenceAxisType rowType, int rowValue, ReferenceAxisType columnType, int columnValue, ReferenceStyle style)
    {
        // ThrowsAny, because an out-of-sheet axis raises the ArgumentOutOfRangeException that
        // derives from ArgumentException and Assert.Throws matches the exact type.
        Assert.ThrowsAny<ArgumentException>(
            () => new RowCol(rowType, rowValue, columnType, columnValue, style));

        Assert.False(RowCol.TryCreate(rowType, rowValue, columnType, columnValue, style, out var rowCol));
        Assert.Equal(default, rowCol);
    }

    /// <summary>
    /// The furthest values the grammar can produce are held, because refusing them would refuse a
    /// formula Excel writes.
    /// </summary>
    [Fact]
    public void The_furthest_reference_the_grammar_produces_is_held()
    {
        Assert.Equal("$XFD$1048576", new RowCol(Absolute, 1048576, Absolute, 16384, A1).GetDisplayStringA1());
        Assert.Equal("R1048576C16384", new RowCol(Absolute, 1048576, Absolute, 16384, R1C1).GetDisplayStringR1C1());
        Assert.Equal("R[1048575]C[16383]", new RowCol(Relative, 1048575, Relative, 16383, R1C1).GetDisplayStringR1C1());
        Assert.Equal("R[-1048575]C[-16383]", new RowCol(Relative, -1048575, Relative, -16383, R1C1).GetDisplayStringR1C1());
    }

    /// <summary>
    /// The bounds the constructor holds an axis to are the ones the grammar admits, so no formula
    /// the parser can read trips them. These are the furthest references one can be written with.
    /// </summary>
    [Theory]
    [InlineData("R1048576C16384", 1, 1, "$XFD$1048576")]
    [InlineData("R[1048575]C[16383]", 1, 1, "XFD1048576")]
    [InlineData("R[-1048575]C[-16383]", 1048576, 16384, "A1")]
    public void The_furthest_reference_a_formula_holds_still_converts(string r1c1, int row, int col, string a1)
    {
        Assert.Equal(a1, FormulaConverter.ToA1(r1c1, row, col));
    }

    /// <summary>
    /// The loop of <see cref="RowCol.ToA1"/> now covers every offset the type holds, so a
    /// converted reference always lands in the sheet its remarks promise.
    /// </summary>
    [Theory]
    [InlineData(1048575, 16383, 1, 1)]
    [InlineData(-1048575, -16383, 1048576, 16384)]
    [InlineData(1048575, 16383, 1048576, 16384)]
    [InlineData(-1048575, -16383, 1, 1)]
    public void ToA1_lands_in_the_sheet_for_every_offset_the_type_holds(
        int rowOffset, int columnOffset, int anchorRow, int anchorCol)
    {
        var converted = new RowCol(Relative, rowOffset, Relative, columnOffset, R1C1).ToA1(anchorRow, anchorCol);

        Assert.InRange(converted.RowValue, 1, 1048576);
        Assert.InRange(converted.ColumnValue, 1, 16384);
    }
}