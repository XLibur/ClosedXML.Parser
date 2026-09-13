namespace ClosedXML.Parser;

/// <summary>
/// Writes the sheet prefix of a display string, i.e. the part naming the sheet a node points into,
/// up to and including the <c>!</c>. Every node that carries a sheet writes its prefix here, so a
/// sheet name that needs quotes gets them once rather than in each node.
/// </summary>
/// <remarks>
/// The library writes a prefix of its own for a formula, but that type is internal, so the rule is
/// here as well. It is the same rule: quote when <see cref="NameUtils.ShouldQuote"/> says so, and
/// double an apostrophe inside the name.
/// </remarks>
internal static class SheetPrefixWriter
{
    /// <summary>
    /// The prefix of a reference into one sheet, of this workbook or of the workbook at
    /// <paramref name="workbookIndex"/>, e.g. <c>Sheet1!</c> or <c>'[2]Jane''s'!</c>. A
    /// <paramref name="sheet"/> of <c>null</c> is a book prefix with no sheet, <c>[2]!</c>.
    /// </summary>
    internal static string Sheet(string? sheet, int? workbookIndex = null)
    {
        var book = Book(workbookIndex);
        if (sheet is null)
            return $"{book}!";

        // A quote wraps the whole prefix, the book index included: '[2]Jane''s'!
        return NameUtils.ShouldQuote(sheet)
            ? $"'{book}{Escape(sheet)}'!"
            : $"{book}{sheet}!";
    }

    /// <summary>
    /// The prefix of a 3D reference, i.e. one spanning the sheets from
    /// <paramref name="firstSheet"/> to <paramref name="lastSheet"/>, e.g. <c>'My Jan:Dec'!</c>.
    /// </summary>
    internal static string Range(string firstSheet, string lastSheet, int? workbookIndex = null)
    {
        var book = Book(workbookIndex);

        // One quote covers both sheets, so either of them needing one quotes the pair.
        return NameUtils.ShouldQuote(firstSheet) || NameUtils.ShouldQuote(lastSheet)
            ? $"'{book}{Escape(firstSheet)}:{Escape(lastSheet)}'!"
            : $"{book}{firstSheet}:{lastSheet}!";
    }

    private static string Book(int? workbookIndex) => workbookIndex is null ? string.Empty : $"[{workbookIndex}]";

    private static string Escape(string sheet) => sheet.Replace("'", "''");
}
