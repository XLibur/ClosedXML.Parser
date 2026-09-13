namespace ClosedXML.Parser;

/// <summary>
/// Writes the specifier of a structured reference, i.e. everything from the first bracket after the
/// table name. Both the local and the external node write it here, so the two cannot drift apart.
/// </summary>
internal static class StructuredReferenceWriter
{
    /// <summary>
    /// The specifier of a structured reference, e.g. <c>[Column]</c>, <c>[#Totals]</c> or
    /// <c>[[#Data],[First]:[Last]]</c>.
    /// </summary>
    /// <remarks>
    /// Two rules, and a fuzzing run found both of them broken. A range of columns is written with a
    /// colon, the way it is read: a comma there is a list of two specifiers, which the parser
    /// refuses after a column. And the parser fills a missing last column in with the first, so the
    /// ordinary single-column reference arrives here with both set — writing both gave every
    /// <c>Table1[Column]</c> back as <c>Table1[[Column],[Column]]</c>, which does not parse.
    /// <para>
    /// A lone specifier stands on its own and keeps only its own brackets. Everything else — two
    /// specifiers, or a range of columns, which is two bracketed names with a colon between them —
    /// needs a pair of brackets around the whole thing.
    /// </para>
    /// </remarks>
    internal static string Specifier(StructuredReferenceArea area, string? firstColumn, string? lastColumn)
    {
        var regions = area.GetSpecifiers();

        // A node built by hand can carry a last column without a first one. The parser never
        // produces that, but dropping the column silently would be a worse answer than writing it.
        var first = firstColumn ?? lastColumn;
        var last = lastColumn ?? firstColumn;
        var isRange = first != last;
        var columns = first is null ? null : isRange ? $"[{first}]:[{last}]" : $"[{first}]";

        if (regions.Count == 0 && columns is null)
            return "[]";

        if (regions.Count == 1 && columns is null)
            return regions[0];

        if (regions.Count == 0 && !isRange)
            return columns!;

        var specifiers = new List<string>(regions.Count + 1);
        specifiers.AddRange(regions);
        if (columns is not null)
            specifiers.Add(columns);

        return $"[{string.Join(",", specifiers)}]";
    }
}
