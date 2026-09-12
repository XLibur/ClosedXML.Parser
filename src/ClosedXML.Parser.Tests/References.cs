using ClosedXML.Parser.Rolex;

namespace ClosedXML.Parser.Tests;

internal static class References
{
    /// <summary>
    /// Read the text of a reference with one or two corners (e.g. <c>R[6]:R[8]</c>). Each reference token is a
    /// corner, the way the parser reads the operands of a range. In R1C1, a span of rows or columns is a token of
    /// its own, so two of them aren't one reference for the parser, but they still make one area.
    /// </summary>
    public static ReferenceArea ReadCorners(IReferenceStyle style, string text)
    {
        var corners = RolexLexer.GetTokens(text.AsSpan(), style.DfaTable)
            .Where(token => token.SymbolId is Token.A1_CELL or Token.A1_SPAN_REFERENCE)
            .Select(token => style.ParseReference(text, token))
            .ToList();
        return corners.Count == 1 ? corners[0] : new ReferenceArea(corners[0].First, corners[1].First);
    }
}
