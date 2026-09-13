using ClosedXML.Parser.Rolex;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;

namespace ClosedXML.Parser;

/// <summary>
/// A utility class that parses various types of references.
/// </summary>
public static class ReferenceParser
{
    /// <summary>
    /// <para>
    /// Try to parse <paramref name="text"/> as a sheet reference (<c>Sheet!A5</c>) or a local
    /// reference (<c>A1</c>). If the <paramref name="text"/> is a local reference, the output
    /// value of the <paramref name="sheetName"/> is <c>null</c>.
    /// </para>
    /// <para>
    /// Unlike the <see cref="TryParseA1(string,out ReferenceArea)"/> or <see cref="TryParseSheetA1(string, out string, out ReferenceArea)"/>,
    /// this method can parse both sheet reference or local reference.
    /// </para>
    /// </summary>
    /// <param name="text">Text to parse.</param>
    /// <param name="sheetName">The unescaped name of a sheet for sheet reference, <c>null</c> for local reference.</param>
    /// <param name="area">The parsed reference area.</param>
    /// <returns><c>true</c> if parsing was a success, <c>false</c> otherwise.</returns>
    [PublicAPI]
    public static bool TryParseA1(string text, out string? sheetName, out ReferenceArea area)
    {
        if (text is null)
            throw new ArgumentNullException(nameof(text));

        sheetName = null;
        var tokens = RolexLexer.GetTokens(text.AsSpan(), TokenParser.A1Style.DfaTable);
        if (TryParse(tokens, text, TokenParser.A1Style, out area))
            return true;

        if (TryParseSheetA1(tokens, text, out sheetName, out area))
            return true;

        return false;
    }

    /// <summary>
    /// Parses area reference in A1 form. The possibilities are
    /// <list type="bullet">
    ///   <item>Cell (e.g. <c>F8</c>).</item>
    ///   <item>Area (e.g. <c>B2:$D7</c>).</item>
    ///   <item>Colspan (e.g. <c>$D:$G</c>).</item>
    ///   <item>Rowspan (e.g. <c>14:$15</c>).</item>
    /// </list>
    /// Doesn't allow any whitespaces or extra values inside.
    /// </summary>
    /// <param name="text">Text to parse.</param>
    /// <param name="area">Parsed area.</param>
    /// <returns><c>true</c> if parsing was a success, <c>false</c> otherwise.</returns>
    [PublicAPI]
    public static bool TryParseA1(string text, out ReferenceArea area)
    {
        if (text is null)
            throw new ArgumentNullException(nameof(text));

        var tokens = RolexLexer.GetTokens(text.AsSpan(), TokenParser.A1Style.DfaTable);
        return TryParse(tokens, text, TokenParser.A1Style, out area);
    }

    /// <summary>
    /// Parses area reference in R1C1 form. The possibilities are
    /// <list type="bullet">
    ///   <item>Cell (e.g. <c>R7C3</c>, <c>R[-1]C</c>).</item>
    ///   <item>Area (e.g. <c>R1C1:R[2]C[2]</c>).</item>
    ///   <item>Colspan (e.g. <c>C2:C[4]</c>).</item>
    ///   <item>Rowspan (e.g. <c>R3:R[5]</c>).</item>
    /// </list>
    /// Doesn't allow any whitespaces or extra values inside. The reference is read as
    /// written, so a relative axis keeps its offset and is not resolved against a cell.
    /// </summary>
    /// <param name="text">Text to parse.</param>
    /// <param name="area">Parsed area.</param>
    /// <returns><c>true</c> if parsing was a success, <c>false</c> otherwise.</returns>
    [PublicAPI]
    public static bool TryParseR1C1(string text, out ReferenceArea area)
    {
        if (text is null)
            throw new ArgumentNullException(nameof(text));

        var tokens = RolexLexer.GetTokens(text.AsSpan(), TokenParser.R1C1Style.DfaTable);
        return TryParse(tokens, text, TokenParser.R1C1Style, out area);
    }

    /// <summary>
    /// Parses area reference in A1 form. The possibilities are
    /// <list type="bullet">
    ///   <item>Cell (e.g. <c>F8</c>).</item>
    ///   <item>Area (e.g. <c>B2:$D7</c>).</item>
    ///   <item>Colspan (e.g. <c>$D:$G</c>).</item>
    ///   <item>Rowspan (e.g. <c>14:$15</c>).</item>
    /// </list>
    /// Doesn't allow any whitespaces or extra values inside.
    /// </summary>
    /// <exception cref="ParsingException">Invalid input.</exception>
    [PublicAPI]
    public static ReferenceArea ParseA1(string text)
    {
        if (!TryParseA1(text, out var area))
            throw new ParsingException($"Unable to parse '{text}'.");

        return area;
    }

    /// <summary>
    /// Try to parse a A1 reference that has a sheet (e.g. <c>'Data values'!A$1:F10</c>).
    /// If <paramref name="text"/> contains only reference without a sheet or anything
    /// else (e.g. <c>A1</c>), return <c>false</c>.
    /// </summary>
    /// <remarks>
    /// The method doesn't accept
    /// <list type="bullet">
    ///   <item>Sheet names, e.g. <c>Sheet!name</c>.</item>
    ///   <item>External sheet references, e.g. <c>[1]Sheet!A1</c>.</item>
    ///   <item>Sheet errors, e.g. <c>Sheet5!$REF!</c>.</item>
    /// </list>
    /// </remarks>
    /// <param name="text">Text to parse.</param>
    /// <param name="sheetName">Name of the sheet, unescaped (e.g. the sheetName will contain <c>Jane's</c> for <c>'Jane''s'!A1</c>).</param>
    /// <param name="area">Parsed reference.</param>
    /// <returns><c>true</c> if parsing was a success, <c>false</c> otherwise.</returns>
    [PublicAPI]
    public static bool TryParseSheetA1(string text, out string sheetName, out ReferenceArea area)
    {
        if (text is null)
            throw new ArgumentNullException(nameof(text));

        var tokens = RolexLexer.GetTokens(text.AsSpan(), TokenParser.A1Style.DfaTable);
        return TryParseSheetA1(tokens, text, out sheetName, out area);
    }

    /// <summary>
    /// <para>
    /// Try to parse <paramref name="text"/> as a name (e.g. <c>Name</c>) or a sheet name
    /// (<c>Sheet!Name</c>). If the <paramref name="text"/> is only a name, the output value of the
    /// <paramref name="sheetName"/> is <c>null</c>.
    /// </para>
    /// </summary>
    /// <param name="text">Text to parse.</param>
    /// <param name="sheetName">The unescaped name of a sheet for sheet name, <c>null</c> for a name.</param>
    /// <param name="name">The parsed name.</param>
    /// <returns><c>true</c> if parsing was a success, <c>false</c> otherwise.</returns>
    [PublicAPI]
    public static bool TryParseName(string text, out string? sheetName, out string name)
    {
        if (text is null)
            throw new ArgumentNullException(nameof(text));

        var tokens = RolexLexer.GetTokens(text.AsSpan(), TokenParser.A1Style.DfaTable);
        if (tokens.Count == 2 &&
            tokens[0].SymbolId == Token.NAME &&
            tokens[1].SymbolId == Token.EofSymbolId)
        {
            sheetName = null;
            name = text;
            return true;
        }

        return TryParseSheetName(tokens, text, out sheetName, out name);
    }

    /// <summary>
    /// Try to parse a text as a sheet name (e.g. <c>Sheet!Name</c>). Doesn't accept pure name
    /// without sheet (e.g. <c>name</c>).
    /// </summary>
    /// <param name="text">Text to parse.</param>
    /// <param name="sheetName">Parsed sheet name, unescaped.</param>
    /// <param name="name">Parsed defined name.</param>
    /// <returns><c>true</c> if parsing was a success, <c>false</c> otherwise.</returns>
    [PublicAPI]
    public static bool TryParseSheetName(string text, out string sheetName, out string name)
    {
        if (text is null)
            throw new ArgumentNullException(nameof(text));

        var tokens = RolexLexer.GetTokens(text.AsSpan(), TokenParser.A1Style.DfaTable);
        return TryParseSheetName(tokens, text, out sheetName, out name);
    }

    private static bool TryParse(List<Token> tokens, string text, IReferenceStyle style, out ReferenceArea area)
    {
        return IsWholeReference(tokens, 0, text, style, out area);
    }

    private static bool TryParseSheetA1(List<Token> tokens, string text, out string sheetName, out ReferenceArea area)
    {
        if (tokens[0].SymbolId != Token.SINGLE_SHEET_PREFIX)
        {
            sheetName = string.Empty;
            area = default;
            return false;
        }

        var prefix = SheetPrefix.ReadSingle(text.AsSpan(), tokens[0]);
        sheetName = prefix.FirstSheet!;
        if (prefix.BookIndex is not null ||
            !NameUtils.IsSheetNameValid(sheetName.AsSpan()) ||
            !IsWholeReference(tokens, 1, text, TokenParser.A1Style, out area))
        {
            sheetName = string.Empty;
            area = default;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Are the tokens from <paramref name="index"/> a reference and nothing else?
    /// </summary>
    private static bool IsWholeReference(List<Token> tokens, int index, string text, IReferenceStyle style, out ReferenceArea area)
    {
        if (TokenParser.TryReadReference(style, text.AsSpan(), tokens, ref index, out area) &&
            tokens[index].SymbolId == Token.EofSymbolId)
            return true;

        // A reference followed by more text isn't a reference, so don't give out the reference at its start.
        area = default;
        return false;
    }

    private static bool TryParseSheetName(List<Token> tokens, string text, out string sheetName, out string name)
    {
        var isValid = tokens.Count switch
        {
            3 => tokens[0].SymbolId == Token.SINGLE_SHEET_PREFIX &&
                 tokens[1].SymbolId == Token.NAME &&
                 tokens[2].SymbolId == Token.EofSymbolId,
            _ => false,
        };
        if (!isValid)
        {
            sheetName = string.Empty;
            name = string.Empty;
            return false;
        }

        var prefix = SheetPrefix.ReadSingle(text.AsSpan(), tokens[0]);
        sheetName = prefix.FirstSheet!;
        if (prefix.BookIndex is not null || !NameUtils.IsSheetNameValid(sheetName.AsSpan()))
        {
            sheetName = string.Empty;
            name = string.Empty;
            return false;
        }

        name = TokenParser.ParseName(text.AsSpan(), tokens[1]);
        return true;
    }
}
