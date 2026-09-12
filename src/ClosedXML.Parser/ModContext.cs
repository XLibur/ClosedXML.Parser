using System;

namespace ClosedXML.Parser;

/// <summary>
/// Where a modified formula is. <see cref="FormulaConverter"/> passes it to every method of a
/// <see cref="FormulaModifier"/>.
/// </summary>
public class ModContext
{
    internal ModContext(string formula, string sheet, int row, int col, bool isA1, FormulaModifier modifier)
    {
        if (string.IsNullOrWhiteSpace(formula))
            throw new ArgumentException(nameof(formula));

        if (row is < 1 or > RowCol.MaxRow)
            throw new ArgumentOutOfRangeException(nameof(row));

        if (col is < 1 or > RowCol.MaxCol)
            throw new ArgumentOutOfRangeException(nameof(row));

        Formula = formula;
        Sheet = sheet;
        Row = row;
        Col = col;
        IsA1 = isA1;
        Modifier = modifier;
    }

    /// <summary>
    /// The original formula without any modifications. The modification writes the formula again from it, so a
    /// modifier gets the parts of the formula, not the text.
    /// </summary>
    internal string Formula { get; }

    /// <summary>
    /// Name of the sheet the formula is on.
    /// </summary>
    public string Sheet { get; }

    /// <summary>
    /// Absolute row number in a sheet.
    /// </summary>
    public int Row { get; }

    /// <summary>
    /// Absolute column number in a sheet.
    /// </summary>
    public int Col { get; }

    /// <summary>
    /// Is the formula written in the A1 reference style? If not, it is written in R1C1, and so are the references
    /// passed to the modifier.
    /// </summary>
    public bool IsA1 { get; }

    /// <summary>
    /// The modifier of the formula.
    /// </summary>
    internal FormulaModifier Modifier { get; }
}
