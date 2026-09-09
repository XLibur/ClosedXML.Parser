using System;
using ClosedXML.Parser.Rolex;
using static ClosedXML.Parser.ReferenceAxisType;

namespace ClosedXML.Parser.Tests;

/// <summary>
/// A reference style is the one module that decides how a formula is lexed and how its
/// references are read. These tests drive an adapter directly, without a formula around it.
/// </summary>
public class ReferenceStyleTests
{
    [Fact]
    public void Each_style_lexes_with_its_own_table()
    {
        // R7C3 is a reference in R1C1 and a name in A1, so the two tables disagree about it.
        var a1 = RolexLexer.GetTokens("R7C3".AsSpan(), TokenParser.A1Style.DfaTable);
        var r1c1 = RolexLexer.GetTokens("R7C3".AsSpan(), TokenParser.R1C1Style.DfaTable);

        Assert.NotEqual(a1[0].SymbolId, r1c1[0].SymbolId);
    }

    [Theory]
    [InlineData("C7(", Relative, 7, Relative, 3)]
    [InlineData("$C$7(", Absolute, 7, Absolute, 3)]
    public void A1_reads_the_called_cell_of_a_cell_function(string token, ReferenceAxisType rowType, int row, ReferenceAxisType colType, int col)
    {
        var cell = TokenParser.A1Style.ParseCellFunction(token.AsSpan());

        Assert.Equal(A1, cell.Style);
        Assert.Equal(rowType, cell.RowType);
        Assert.Equal(row, cell.RowValue);
        Assert.Equal(colType, cell.ColumnType);
        Assert.Equal(col, cell.ColumnValue);
    }

    /// <summary>
    /// The R1C1 table emits a cell function token, but the called cell was always read as A1.
    /// <c>R7C3(</c> was read as the A1 cell <c>R7</c> and the <c>C3</c> was thrown away.
    /// </summary>
    [Theory]
    [InlineData("R7C3(", Absolute, 7, Absolute, 3)]
    [InlineData("R[7]C[3](", Relative, 7, Relative, 3)]
    [InlineData("RC(", Relative, 0, Relative, 0)]
    public void R1C1_reads_the_called_cell_of_a_cell_function(string token, ReferenceAxisType rowType, int row, ReferenceAxisType colType, int col)
    {
        var cell = TokenParser.R1C1Style.ParseCellFunction(token.AsSpan());

        Assert.Equal(R1C1, cell.Style);
        Assert.Equal(rowType, cell.RowType);
        Assert.Equal(row, cell.RowValue);
        Assert.Equal(colType, cell.ColumnType);
        Assert.Equal(col, cell.ColumnValue);
    }
}
