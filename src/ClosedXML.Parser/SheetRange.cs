using System;

namespace ClosedXML.Parser;

/// <summary>
/// The sheets a 3D reference spans, i.e. the <c>first:last</c> of <c>first:last!A1</c>. The
/// reference covers both of them and every sheet standing between them in the workbook. Which
/// sheets those are is tab order, and the parser reads a formula rather than a workbook, so it
/// knows the two names and nothing else.
/// </summary>
public readonly record struct SheetRange
{
    /// <summary>
    /// The sheets a 3D reference spans.
    /// </summary>
    /// <param name="firstSheet">The sheet the reference starts at.</param>
    /// <param name="lastSheet">The sheet the reference ends at.</param>
    /// <exception cref="ArgumentNullException">Either sheet is <c>null</c>.</exception>
    public SheetRange(string firstSheet, string lastSheet)
    {
        FirstSheet = firstSheet ?? throw new ArgumentNullException(nameof(firstSheet));
        LastSheet = lastSheet ?? throw new ArgumentNullException(nameof(lastSheet));
    }

    /// <summary>
    /// The sheet the reference starts at.
    /// </summary>
    public string FirstSheet { get; }

    /// <summary>
    /// The sheet the reference ends at.
    /// </summary>
    public string LastSheet { get; }
}
