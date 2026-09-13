using System;
using System.Diagnostics;
using System.Text;
using ClosedXML.Parser.Rolex;

namespace ClosedXML.Parser;

/// <summary>
/// The sheet prefix of a reference, i.e. the part naming the sheet a reference points into, up to
/// and including the <c>!</c>. It covers every shape the prefix is written in: a single sheet
/// (<c>Sheet1!</c>, <c>'New York'!</c>), a 3D reference (<c>first:last!</c>), either of them behind
/// a book prefix (<c>[1]Sheet1!</c>), the link of a dynamic data exchange reference
/// (<c>Sdemo123|tik!</c>), the empty prefix of a bang reference (<c>!</c>) and the <c>#REF!</c> of a
/// deleted sheet.
/// <para>
/// Reading and writing are both here, because they are one decision seen from two sides: whether a
/// name needs quotes decides how it is written, and how it was written decides where the name ends.
/// A prefix written from a value reads back as the same value, shape by shape.
/// </para>
/// </summary>
internal readonly record struct SheetPrefix
{
    private const string REF_ERROR = "#REF!";

    private SheetPrefix(int? bookIndex, string? firstSheet, string? lastSheet, bool isDeleted, bool isDdeLink)
    {
        BookIndex = bookIndex;
        FirstSheet = firstSheet;
        LastSheet = lastSheet;
        IsDeleted = isDeleted;
        IsDdeLink = isDdeLink;
    }

    /// <summary>
    /// The empty prefix of a bang reference (<c>!A1</c>), which points into the workbook scope
    /// rather than a named sheet.
    /// </summary>
    internal static SheetPrefix Bang => default;

    /// <summary>
    /// The prefix of a reference whose sheet has been deleted, written <c>#REF!</c>.
    /// </summary>
    internal static SheetPrefix Deleted => new(null, null, null, true, false);

    /// <summary>
    /// The prefix of a reference into one sheet, of this workbook or of the workbook at
    /// <paramref name="bookIndex"/>.
    /// </summary>
    internal static SheetPrefix Sheet(string sheet, int? bookIndex = null) => new(bookIndex, sheet, null, false, false);

    /// <summary>
    /// The prefix of a 3D reference, i.e. one spanning the sheets from <paramref name="firstSheet"/>
    /// to <paramref name="lastSheet"/>.
    /// </summary>
    internal static SheetPrefix Range(string firstSheet, string lastSheet, int? bookIndex = null) => new(bookIndex, firstSheet, lastSheet, false, false);

    /// <summary>
    /// The prefix of a dynamic data exchange reference, i.e. the application and the topic its link
    /// reads from, written <c>application|topic!</c>.
    /// </summary>
    internal static SheetPrefix DdeLink(string application, string topic) => new(null, application + '|' + topic, null, false, true);

    /// <summary>
    /// Index of the workbook the sheet is in, or <c>null</c> when it is in this workbook. A sheet
    /// behind a book prefix belongs to another workbook and is never renamed.
    /// </summary>
    internal int? BookIndex { get; }

    /// <summary>
    /// The sheet, or the first sheet of a 3D reference. <c>null</c> for <see cref="Bang"/> and
    /// <see cref="Deleted"/>.
    /// </summary>
    internal string? FirstSheet { get; }

    /// <summary>
    /// The last sheet of a 3D reference, otherwise <c>null</c>.
    /// </summary>
    internal string? LastSheet { get; }

    /// <summary>
    /// Is this the prefix of a reference whose sheet has been deleted?
    /// </summary>
    internal bool IsDeleted { get; }

    /// <summary>
    /// Is this the link of a dynamic data exchange reference? It is written bare where a sheet name
    /// of the same text would be quoted.
    /// </summary>
    internal bool IsDdeLink { get; }

    /// <summary>
    /// Is this the empty prefix of a bang reference?
    /// </summary>
    internal bool IsBang => FirstSheet is null && !IsDeleted;

    /// <summary>
    /// Is this the prefix of a 3D reference?
    /// </summary>
    internal bool IsRange => LastSheet is not null;

    /// <summary>
    /// Read a <see cref="Token.SINGLE_SHEET_PREFIX"/> token, e.g. <c>'[1]Jane''s'!</c>.
    /// </summary>
    internal static SheetPrefix ReadSingle(ReadOnlySpan<char> formula, Token token)
    {
        Debug.Assert(token.SymbolId == Token.SINGLE_SHEET_PREFIX);

        // There can be whitespaces after exclamation mark at the end of a token.
        var input = Text(formula, token).TrimEnd();
        var isEscaped = input[0] == '\'';
        input = isEscaped
            ? input.Slice(1, input.Length - 3) // second sheet name ends with TICK EXCLAMATION_MARK ('!).
            : input.Slice(0, input.Length - 1); // only strip exclamation mark

        // Parse optional WORKBOOK_INDEX
        input = ExtractWorkbookIndex(input, out var bookIndex);

        // The ending '! have been stripped from escape
        var sheet = isEscaped ? GetEscapedSheetName(input) : input.ToString();
        return Sheet(sheet, bookIndex);
    }

    /// <summary>
    /// Read the workbook index of a <see cref="Token.BOOK_PREFIX"/> token, e.g. <c>[1]</c>. A book
    /// prefix stands on its own, without a sheet, so it is a number rather than a prefix.
    /// </summary>
    internal static int ReadBookPrefix(ReadOnlySpan<char> formula, Token token)
    {
        Debug.Assert(token.SymbolId == Token.BOOK_PREFIX);
        ExtractWorkbookIndex(Text(formula, token), out var index);
        Debug.Assert(index.HasValue);
        return index!.Value;
    }

    /// <summary>
    /// Read a <see cref="Token.SHEET_RANGE_PREFIX"/> token, e.g. <c>'[1]first:second'!</c>.
    /// </summary>
    internal static SheetPrefix ReadRange(ReadOnlySpan<char> formula, Token token)
    {
        Debug.Assert(token.SymbolId == Token.SHEET_RANGE_PREFIX);

        var input = Text(formula, token);
        var isEscaped = input[0] == '\'';
        input = isEscaped
            ? input.Slice(1, input.Length - 3) // second sheet name ends with TICK EXCLAMATION_MARK ('!).
            : input.Slice(0, input.Length - 1); // only strip exclamation mark

        // Parse optional WORKBOOK_INDEX
        input = ExtractWorkbookIndex(input, out var bookIndex);

        if (!isEscaped)
        {
            // SHEET_NAME ':' SHEET_NAME
            var endIndex = input.IndexOf(':');
            return Range(input.Slice(0, endIndex).ToString(), input.Slice(endIndex + 1).ToString(), bookIndex);
        }

        // Parse SHEET_NAME_SPECIAL which can contain escaped tick (') as double tick
        var firstSheet = GetEscapedSheetName(ref input, ':'); // Even escaped sheet name can't contain :
        return Range(firstSheet, GetEscapedSheetName(input), bookIndex);
    }

    /// <summary>
    /// Read the prefix as the application and the topic of a DDE link (e.g. <c>Sdemo123|tik!</c>).
    /// It must have no workbook index and a non-empty part on each side of the first <c>|</c>.
    /// </summary>
    internal bool TryGetDdeLink(out string application, out string topic)
    {
        var name = FirstSheet;
        var separatorIndex = name?.IndexOf('|') ?? -1;
        if (name is null || BookIndex is not null || IsRange || separatorIndex <= 0 || separatorIndex == name.Length - 1)
        {
            application = string.Empty;
            topic = string.Empty;
            return false;
        }

        application = name.Substring(0, separatorIndex);
        topic = name.Substring(separatorIndex + 1);
        return true;
    }

    /// <summary>
    /// Write the prefix, with the quotes the sheet names need and the <c>!</c> that ends it.
    /// </summary>
    internal StringBuilder Append(StringBuilder sb)
    {
        if (IsDeleted)
            return sb.Append(REF_ERROR);

        if (IsBang)
            return sb.AppendReferenceSeparator();

        var firstSheet = FirstSheet!;
        if (IsDdeLink)
            return AppendDdeLink(sb, firstSheet);

        if (!IsRange)
        {
            if (BookIndex is null)
                return NameUtils.EscapeName(sb, firstSheet).AppendReferenceSeparator();

            if (!NameUtils.ShouldQuote(firstSheet.AsSpan()))
                return sb.AppendBookIndex(BookIndex.Value).Append(firstSheet).AppendReferenceSeparator();

            return sb
                .Append('\'')
                .AppendBookIndex(BookIndex.Value)
                .AppendEscapedSheetName(firstSheet)
                .Append('\'')
                .AppendReferenceSeparator();
        }

        // A quote covers the whole prefix, both sheets and the book index, so either sheet needing
        // one quotes the lot. The first sheet of a bare 3D reference has a rule of its own on top,
        // because nothing before it says a sheet prefix has started.
        // A book prefix has already said a sheet prefix has started, so the first sheet behind one
        // is judged like any other name.
        var lastSheet = LastSheet!;
        var quoteFirstSheet = BookIndex is null
            ? NameUtils.ShouldQuoteAsFirstSheet(firstSheet.AsSpan())
            : NameUtils.ShouldQuote(firstSheet.AsSpan());

        if (!quoteFirstSheet && !NameUtils.ShouldQuote(lastSheet.AsSpan()))
        {
            if (BookIndex is not null)
                sb.AppendBookIndex(BookIndex.Value);

            return sb.Append(firstSheet).Append(':').Append(lastSheet).AppendReferenceSeparator();
        }

        sb.Append('\'');
        if (BookIndex is not null)
            sb.AppendBookIndex(BookIndex.Value);

        return sb
            .AppendEscapedSheetName(firstSheet)
            .Append(':')
            .AppendEscapedSheetName(lastSheet)
            .Append('\'')
            .AppendReferenceSeparator();
    }

    /// <remarks>
    /// Excel writes the link bare, although <see cref="NameUtils.ShouldQuote"/> would quote it as a
    /// sheet name, because of the <c>|</c>. The link stays bare whenever the lexer reads it back as
    /// the same link, and is quoted only when it wouldn't be (e.g. with a space).
    /// </remarks>
    private static StringBuilder AppendDdeLink(StringBuilder sb, string link)
    {
        var prefix = link + '!';
        if (ReadsBackAsSheetPrefix(prefix, link))
            return sb.Append(prefix);

        return sb.Append('\'').AppendEscapedSheetName(link).Append('\'').AppendReferenceSeparator();
    }

    /// <summary>
    /// Does <paramref name="prefix"/> read back as the sheet prefix of <paramref name="name"/>, with
    /// no workbook index?
    /// </summary>
    private static bool ReadsBackAsSheetPrefix(string prefix, string name)
    {
        var tokens = RolexLexer.GetTokensA1(prefix.AsSpan());
        if (tokens.Count != 2 || tokens[0].SymbolId != Token.SINGLE_SHEET_PREFIX || tokens[0].Length != prefix.Length)
            return false;

        var readBack = ReadSingle(prefix.AsSpan(), tokens[0]);
        return readBack.BookIndex is null && readBack.FirstSheet == name;
    }

    private static ReadOnlySpan<char> Text(ReadOnlySpan<char> formula, Token token) => formula.Slice(token.StartIndex, token.Length);

    private static ReadOnlySpan<char> ExtractWorkbookIndex(ReadOnlySpan<char> input, out int? wbIndex)
    {
        if (input[0] != '[')
        {
            wbIndex = null;
            return input;
        }

        var i = 0;
        var number = 0;
        var c = input[++i];
        do
        {
            number = number * 10 + c - '0';
            c = input[++i];
        } while (c != ']');

        wbIndex = number;
        return input.Slice(i + 1);
    }

    private static string GetEscapedSheetName(ref ReadOnlySpan<char> input, char endChar)
    {
        Span<char> buffer = input.Length <= TokenParser.MaxStackAllocChars
            ? stackalloc char[TokenParser.MaxStackAllocChars]
            : new char[input.Length];
        var bufferIdx = 0;
        var inputIdx = 0;
        do
        {
            if (input[inputIdx] == '\'')
                inputIdx++;

            buffer[bufferIdx++] = input[inputIdx++];
        } while (input[inputIdx] != endChar);

        input = input.Slice(inputIdx + 1);
        return buffer.Slice(0, bufferIdx).ToString();
    }

    private static string GetEscapedSheetName(ReadOnlySpan<char> input)
    {
        Span<char> buffer = input.Length <= TokenParser.MaxStackAllocChars
            ? stackalloc char[TokenParser.MaxStackAllocChars]
            : new char[input.Length];
        var bufferIdx = 0;
        var inputIdx = 0;
        do
        {
            if (input[inputIdx] == '\'')
                inputIdx++;

            buffer[bufferIdx++] = input[inputIdx++];
        } while (input.Length > inputIdx);

        return buffer.Slice(0, bufferIdx).ToString();
    }
}
