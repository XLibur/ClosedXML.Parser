using System.Text;
using static ClosedXML.Parser.ReferenceAxisType;

namespace ClosedXML.Parser;

/// <summary>
/// <para>
/// One endpoint of a reference defined by row and column axis. It can be
/// <list type="bullet">
///   <item>
///   A single cell that is an intersection of row and a column
///   </item>
///   <item>
///   An entire row, e.g. <c><em>A</em>:B</c> or <c><em>R5</em>:R10</c>.
///   </item>
///   <item>
///   An entire column, e.g. <c><em>7</em>:14</c> or <c><em>C7</em>:C10</c>.
///   </item>
/// </list>
/// The content of values and thus their interpretation depends on the
/// <see cref="ReferenceArea"/> reference style, e.g. column 14 with
/// <see cref="Relative"/> can indicate <c>R[14]</c> or <c>X14</c> for A1
/// style.
/// </para>
/// <para>
/// Not all combinations are valid and the content of the reference corresponds
/// to a valid token in expected reference style (e.g. in R1C1, <c>R</c> is
/// a valid standalone reference, but there is no such possibility for A1).
/// </para>
/// </summary>
public readonly struct RowCol : IEquatable<RowCol>
{
    /// <summary>
    /// The first row of a sheet.
    /// </summary>
    public const int MinRow = 1;

    /// <summary>
    /// The last row of a sheet.
    /// </summary>
    public const int MaxRow = 1048576;

    /// <summary>
    /// The first column of a sheet.
    /// </summary>
    public const int MinCol = 1;

    /// <summary>
    /// The last column of a sheet, the <c>XFD</c> of <em>A1</em> notation.
    /// </summary>
    public const int MaxCol = 16384;

    // keep at 0, so default ctor creates is A1
    private readonly int _rowIndex;
    private readonly int _columnIndex;

    /// <summary>
    /// How to interpret the <see cref="ColumnValue"/> value.
    /// </summary>
    public ReferenceAxisType ColumnType { get; }

    /// <summary>
    /// Position of a column.
    /// </summary>
    public int ColumnValue => _columnIndex + 1;

    /// <summary>
    /// How to interpret the <see cref="RowValue"/> value.
    /// </summary>
    public ReferenceAxisType RowType { get; }

    /// <summary>
    /// Position of a row.
    /// </summary>
    public int RowValue => _rowIndex + 1;

    /// <summary>
    /// Does <c>RowCol</c> use <em>A1</em> semantic?
    /// </summary>
    public bool IsA1 => Style == A1;

    /// <summary>
    /// Does <c>RowCol</c> use <em>R1C1</em> semantic?
    /// </summary>
    public bool IsR1C1 => Style == R1C1;

    /// <summary>
    /// Reference style of the <c>RowCol</c>.
    /// </summary>
    public ReferenceStyle Style { get; }

    /// <summary>
    /// Is RowCol a part (start or end) of row span?
    /// </summary>
    public bool IsRow => ColumnType == None;

    /// <summary>
    /// Is RowCol a part (start or end) of column span?
    /// </summary>
    public bool IsColumn => RowType == None;

    /// <summary>
    /// Create a new <see cref="RowCol"/> with both row and columns specified.
    /// </summary>
    /// <param name="rowType">The type used to interpret the row position.</param>
    /// <param name="rowValue">The value for the row position.</param>
    /// <param name="columnType">The type used to interpret the column position.</param>
    /// <param name="columnValue">The value for the column position.</param>
    /// <param name="style">Semantic of the reference.</param>
    /// <exception cref="ArgumentException">Both axes are <see cref="None"/>, or a
    /// <see cref="None"/> axis carries a value other than zero.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An axis is outside a sheet. See the remarks.</exception>
    /// <remarks>
    /// An axis holds what a sheet has and nothing else, so a <c>RowCol</c> can't name a cell no
    /// sheet holds. A position — an <em>A1</em> axis of either type, or an <see cref="Absolute"/>
    /// <em>R1C1</em> one — is a row of 1 to 1048576 or a column of 1 to 16384. A
    /// <see cref="Relative"/> <em>R1C1</em> axis is an offset from the formula's own cell instead,
    /// and the furthest one cell of a sheet can be from another is one short of the sheet, so an
    /// offset reaches 1048575 rows or 16383 columns either way. Those are the same bounds the
    /// grammar admits, so every reference a formula can be parsed into is held.
    /// </remarks>
    public RowCol(ReferenceAxisType rowType, int rowValue, ReferenceAxisType columnType, int columnValue, ReferenceStyle style)
    {
        if (columnType == None && rowType == None)
            throw new ArgumentException("At least one of axis must be non-none.");

        var rowFault = CheckAxis(rowType, rowValue, style, MinRow, MaxRow);
        if (rowFault != AxisFault.Ok)
            throw AxisError(rowFault, rowValue, MinRow, MaxRow, "row", nameof(rowValue));

        var columnFault = CheckAxis(columnType, columnValue, style, MinCol, MaxCol);
        if (columnFault != AxisFault.Ok)
            throw AxisError(columnFault, columnValue, MinCol, MaxCol, "column", nameof(columnValue));

        ColumnType = columnType;
        _columnIndex = columnValue - 1;
        RowType = rowType;
        _rowIndex = rowValue - 1;
        Style = style;
    }

    /// <summary>
    /// Create a new <see cref="RowCol"/>, or answer that the arguments don't describe one, rather
    /// than raising the exception the constructor does.
    /// </summary>
    /// <remarks>
    /// A caller that builds a <c>RowCol</c> from a position it worked out itself — a
    /// <see cref="FormulaModifier"/> shifting a reference is the one that does — can land outside
    /// the sheet as an ordinary outcome, because a reference shifted off a sheet is a
    /// <c>#REF!</c> rather than a mistake. This answers that question without an exception, and
    /// reads the same rule the constructor does, so the two can't disagree.
    /// </remarks>
    /// <param name="rowType">The type used to interpret the row position.</param>
    /// <param name="rowValue">The value for the row position.</param>
    /// <param name="columnType">The type used to interpret the column position.</param>
    /// <param name="columnValue">The value for the column position.</param>
    /// <param name="style">Semantic of the reference.</param>
    /// <param name="rowCol">The created <c>RowCol</c>, or <c>default</c> when there isn't one.</param>
    /// <returns><c>true</c> when the arguments describe a <c>RowCol</c>, <c>false</c> otherwise.</returns>
    public static bool TryCreate(ReferenceAxisType rowType, int rowValue, ReferenceAxisType columnType,
        int columnValue, ReferenceStyle style, out RowCol rowCol)
    {
        if ((rowType == None && columnType == None) ||
            CheckAxis(rowType, rowValue, style, MinRow, MaxRow) != AxisFault.Ok ||
            CheckAxis(columnType, columnValue, style, MinCol, MaxCol) != AxisFault.Ok)
        {
            rowCol = default;
            return false;
        }

        rowCol = new RowCol(rowType, rowValue, columnType, columnValue, style);
        return true;
    }

    /// <summary>
    /// Why one axis can't be part of a <see cref="RowCol"/>.
    /// </summary>
    private enum AxisFault
    {
        /// <summary>The axis is one a sheet has.</summary>
        Ok,

        /// <summary>The axis is <see cref="None"/> and carries a value anyway.</summary>
        ValueOnNoneAxis,

        /// <summary>The axis names a row or a column that no sheet has.</summary>
        PositionOutsideSheet,

        /// <summary>The axis is an offset that reaches further than a sheet.</summary>
        OffsetWiderThanSheet,
    }

    /// <summary>
    /// Hold one axis to a sheet, so that no <c>RowCol</c> carries a row or a column a sheet
    /// doesn't have. See the remarks of the constructor for the bounds and why they are those.
    /// </summary>
    /// <param name="type">How the value of the axis is to be read.</param>
    /// <param name="value">The value of the axis.</param>
    /// <param name="style">Semantic of the reference the axis belongs to.</param>
    /// <param name="min">The first row or column of a sheet.</param>
    /// <param name="max">The last row or column of a sheet.</param>
    private static AxisFault CheckAxis(ReferenceAxisType type, int value, ReferenceStyle style, int min, int max)
    {
        if (type == None)
            return value == 0 ? AxisFault.Ok : AxisFault.ValueOnNoneAxis;

        // An R1C1 offset is counted from the formula's own cell, which is itself in the sheet, so
        // it stops one short of the sheet on each side. Everything else is a position in a sheet.
        if (type == Relative && style == R1C1)
            return value > -max && value < max ? AxisFault.Ok : AxisFault.OffsetWiderThanSheet;

        return value >= min && value <= max ? AxisFault.Ok : AxisFault.PositionOutsideSheet;
    }

    /// <summary>
    /// The exception the constructor raises for an axis <see cref="CheckAxis"/> refused.
    /// </summary>
    /// <param name="fault">Why the axis was refused.</param>
    /// <param name="value">The value of the axis.</param>
    /// <param name="min">The first row or column of a sheet.</param>
    /// <param name="max">The last row or column of a sheet.</param>
    /// <param name="axis">The name of the axis, for the message.</param>
    /// <param name="paramName">The name of the constructor parameter the value came from.</param>
    private static Exception AxisError(AxisFault fault, int value, int min, int max, string axis, string paramName)
    {
        switch (fault)
        {
            case AxisFault.ValueOnNoneAxis:
                return new ArgumentException("Value for `None` type must be zero.", paramName);

            case AxisFault.OffsetWiderThanSheet:
                return new ArgumentOutOfRangeException(paramName, value,
                    $"A relative R1C1 {axis} is an offset from the formula's own cell and can't " +
                    $"reach outside the sheet, so it is {1 - max} to {max - 1}.");

            default:
                return new ArgumentOutOfRangeException(paramName, value,
                    $"A {axis} of a sheet is {min} to {max}.");
        }
    }

    /// <summary>
    /// Create a new <see cref="RowCol"/> with both row and columns specified.
    /// </summary>
    /// <param name="rowAbs">Is the row reference absolute? If false, then relative.</param>
    /// <param name="rowValue">The value for the row position.</param>
    /// <param name="colAbs">Is the column reference absolute? If false, then relative.</param>
    /// <param name="columnValue">The value for the column position.</param>
    /// <param name="style">Semantic of the reference.</param>
    internal RowCol(bool rowAbs, int rowValue, bool colAbs, int columnValue, ReferenceStyle style)
        : this(rowAbs ? Absolute : Relative, rowValue, colAbs ? Absolute : Relative, columnValue, style)
    {
    }

    /// <summary>
    /// Create a new <see cref="RowCol"/> with both row and columns specified
    /// with relative values. Used mostly for A1 style.
    /// </summary>
    /// <param name="row">The relative position of the row.</param>
    /// <param name="column">The relative position of the column.</param>
    /// <param name="style">Semantic of the reference.</param>
    internal RowCol(int row, int column, ReferenceStyle style)
        : this(Relative, row, Relative, column, style)
    {
    }

    /// <summary>
    /// Compares two <see cref="RowCol"/> objects by value. The result specifies whether
    /// all properties of the two <see cref="RowCol"/> objects are equal.
    /// </summary>
    public static bool operator ==(RowCol lhs, RowCol rhs) => lhs.Equals(rhs);

    /// <summary>
    /// Compares two <see cref="RowCol"/> objects by value. The result specifies whether
    /// any property of the two <see cref="RowCol"/> objects is not equal.
    /// </summary>
    public static bool operator !=(RowCol lhs, RowCol rhs) => !(lhs == rhs);

    /// <summary>
    /// Get a reference in <em>A1</em> notation.
    /// </summary>
    /// <exception cref="InvalidOperationException">When <c>RowCol</c> doesn't use <em>A1</em> semantic.</exception>
    public string GetDisplayStringA1()
    {
        if (!IsA1)
            throw new InvalidOperationException("RowCol doesn't use A1 semantic.");

        var sb = new StringBuilder();
        AppendA1(sb);
        return sb.ToString();
    }

    /// <summary>
    /// Get a reference in <em>R1C1</em> notation.
    /// </summary>
    /// <exception cref="InvalidOperationException">When <c>RowCol</c> doesn't use <em>R1C1</em> semantic.</exception>
    public string GetDisplayStringR1C1()
    {
        if (!IsR1C1)
            throw new InvalidOperationException("RowCol doesn't use R1C1 semantic.");

        var sb = new StringBuilder();
        AppendR1C1(sb);
        return sb.ToString();
    }

    /// <summary>
    /// Convert <c>RowCol</c> to <em>R1C1</em>.
    /// </summary>
    /// <remarks>If <c>RowCol</c> already is in <em>R1C1</em>, return it directly.</remarks>
    /// <param name="anchorRow">A row coordinate that should be used as an anchor for relative R1C1 reference.</param>
    /// <param name="anchorCol">A column coordinate that should be used as an anchor for relative R1C1 reference.</param>
    /// <returns>RowCol with R1C1 semantic.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Row or col is out of valid row or column number.</exception>
    public RowCol ToR1C1(int anchorRow, int anchorCol)
    {
        if (anchorRow is < 1 or > MaxRow)
            throw new ArgumentOutOfRangeException(nameof(anchorRow));

        if (anchorCol is < 1 or > MaxCol)
            throw new ArgumentOutOfRangeException(nameof(anchorCol));

        if (IsR1C1)
            return this;

        var newRowPosition = ConvertAxis(RowType, RowValue, anchorRow);
        var newColPosition = ConvertAxis(ColumnType, ColumnValue, anchorCol);

        return new RowCol(RowType, newRowPosition, ColumnType, newColPosition, R1C1);

        static int ConvertAxis(ReferenceAxisType axisType, int axisValue, int anchorPosition)
        {
            return axisType switch
            {
                Relative => axisValue - anchorPosition,
                Absolute => axisValue,
                None => 0,
                _ => throw new NotSupportedException()
            };
        }
    }

    /// <summary>
    /// Convert <c>RowCol</c> to <em>A1</em>.
    /// </summary>
    /// <remarks>
    /// If <c>RowCol</c> already is in <em>A1</em>, return it directly. If converted <c>RowCol</c>
    /// is out of sheet bounds, loop it. An offset spans at most one sheet, which the constructor
    /// holds it to, so the loop always lands in the sheet.
    /// </remarks>
    /// <param name="anchorRow">A row coordinate that should be used as an anchor for relative <em>R1C1</em> reference.</param>
    /// <param name="anchorCol">A column coordinate that should be used as an anchor for relative <em>R1C1</em> reference.</param>
    /// <returns>RowCol with R1C1 semantic.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Row or col is out of valid row or column number.</exception>
    public RowCol ToA1(int anchorRow, int anchorCol)
    {
        var (newRowPosition, newColPosition) = ToA1Positions(anchorRow, anchorCol);

        // Modulo is expensive and because of the constructor's bounds, we can't go out of 1 range
        // on each side: an offset is at most one short of the sheet and an anchor is in it.
        if (RowType == Relative)
        {
            if (newRowPosition < 1)
                newRowPosition += MaxRow;
            if (newRowPosition > MaxRow)
                newRowPosition -= MaxRow;
        }

        if (ColumnType == Relative)
        {
            if (newColPosition < 1)
                newColPosition += MaxCol;
            if (newColPosition > MaxCol)
                newColPosition -= MaxCol;
        }

        return new RowCol(RowType, newRowPosition, ColumnType, newColPosition, A1);
    }

    internal RowCol? ToA1OrError(int anchorRow, int anchorCol)
    {
        var (newRowPosition, newColPosition) = ToA1Positions(anchorRow, anchorCol);
        if (RowType == Relative && newRowPosition is < 1 or > MaxRow)
            return null;

        if (ColumnType == Relative && newColPosition is < 1 or > MaxCol)
            return null;

        return new RowCol(RowType, newRowPosition, ColumnType, newColPosition, A1);
    }

    private (int Row, int Col) ToA1Positions(int anchorRow, int anchorCol)
    {
        if (anchorRow is < 1 or > MaxRow)
            throw new ArgumentOutOfRangeException(nameof(anchorRow));

        if (anchorCol is < 1 or > MaxCol)
            throw new ArgumentOutOfRangeException(nameof(anchorCol));

        if (IsA1)
            return (RowValue, ColumnValue);

        var newRowPosition = ConvertAxis(RowType, RowValue, anchorRow);
        var newColPosition = ConvertAxis(ColumnType, ColumnValue, anchorCol);

        return (newRowPosition, newColPosition);

        static int ConvertAxis(ReferenceAxisType axisType, int axisValue, int anchorPosition)
        {
            return axisType switch
            {
                Relative => axisValue + anchorPosition,
                Absolute => axisValue,
                None => 0,
                _ => throw new NotSupportedException()
            };
        }
    }

    internal void Append(StringBuilder sb)
    {
        if (IsA1)
            AppendA1(sb);
        else
            AppendR1C1(sb);
    }

    /// <inheritdoc cref="GetDisplayStringA1()"/>
    /// <param name="sb">String buffer where to write the output.</param>
    /// <exception cref="InvalidOperationException">When <c>RowCol</c> is not in <em>A1</em> notation.</exception>
    internal StringBuilder AppendA1(StringBuilder sb)
    {
        if (!IsA1)
            throw new InvalidOperationException("RowCol doesn't use A1 semantic.");

        switch (ColumnType)
        {
            case Absolute:
                sb.Append('$');
                AppendA1Column(sb);
                break;

            case Relative:
                AppendA1Column(sb);
                break;

            case None:
                break;

            default:
                throw new NotSupportedException();
        }

        switch (RowType)
        {
            case Absolute:
                sb.Append('$').AppendInvariant(RowValue);
                break;

            case Relative:
                sb.AppendInvariant(RowValue);
                break;

            case None:
                break;

            default:
                throw new NotSupportedException();
        }

        return sb;
    }

    /// <inheritdoc cref="GetDisplayStringR1C1()"/>
    /// <param name="sb">String buffer where to write the output.</param>
    /// <exception cref="InvalidOperationException">When <c>RowCol</c> is not in <em>A1</em> notation.</exception>
    internal void AppendR1C1(StringBuilder sb)
    {
        if (!IsR1C1)
            throw new InvalidOperationException("RowCol doesn't use R1C1 semantic.");

        AppendAxis(sb, 'R', RowType, RowValue);
        AppendAxis(sb, 'C', ColumnType, ColumnValue);

        static void AppendAxis(StringBuilder sb, char axis, ReferenceAxisType type, int position)
        {
            switch (type)
            {
                case Absolute:
                    sb.Append(axis).AppendInvariant(position);
                    break;

                case Relative when position != 0:
                    sb.Append(axis).Append('[').AppendInvariant(position).Append(']');
                    break;

                case Relative:
                    // position is always 0
                    sb.Append(axis);
                    break;

                case None:
                    break;

                default:
                    throw new NotSupportedException();
            }
        }
    }

    /// <summary>
    /// Write the column letters of the column, e.g. <c>XFD</c> for column 16384.
    /// </summary>
    /// <remarks>
    /// The letters are found from the last one back, so they are collected in a buffer and written
    /// in the order they are read.
    /// </remarks>
    private void AppendA1Column(StringBuilder sb)
    {
        // The constructor holds a column to the 16384 of a sheet, which is the three letters XFD,
        // and every other way here converts a column into that range, so three is the whole buffer.
        const int maxColumnLetters = 3;
        Span<char> letters = stackalloc char[maxColumnLetters];
        var columnIndex = ColumnValue;
        var i = maxColumnLetters;
        do
        {
            columnIndex -= 1;
            var index = columnIndex % 26;
            columnIndex -= index;
            columnIndex /= 26;
            letters[--i] = (char)('A' + index);
        } while (columnIndex > 0);

        for (; i < maxColumnLetters; ++i)
            sb.Append(letters[i]);
    }

    /// <summary>
    /// Check whether the <paramref name="obj"/> is of type <see cref="RowCol"/>
    /// and all values are same as this one.
    /// </summary>
    public override bool Equals(object? obj)
    {
        return obj is RowCol other && Equals(other);
    }

    /// <summary>
    /// Check whether the all values of <paramref name="other"/> are same as
    /// this one.
    /// </summary>
    public bool Equals(RowCol other)
    {
        return ColumnType == other.ColumnType &&
               ColumnValue == other.ColumnValue &&
               RowType == other.RowType &&
               RowValue == other.RowValue &&
               IsA1 == other.IsA1;
    }

    /// <summary>
    /// Returns a hash code for this <see cref="RowCol"/>.
    /// </summary>
    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = (int)ColumnType;
            hashCode = (hashCode * 397) ^ ColumnValue;
            hashCode = (hashCode * 397) ^ (int)RowType;
            hashCode = (hashCode * 397) ^ RowValue;
            hashCode = (hashCode * 397) ^ IsA1.GetHashCode();
            return hashCode;
        }
    }
}