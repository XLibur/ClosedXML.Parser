using System;
using System.Collections.Generic;
using System.Diagnostics;
using ClosedXML.Parser.Rolex;
using static ClosedXML.Parser.ReferenceAxisType;

namespace ClosedXML.Parser;

/// <summary>
/// Reads the meaning out of the tokens of a formula. A caller hands over a token and the formula it was
/// lexed from, and gets back what the token says, e.g. a workbook index and an unescaped sheet name, or a
/// reference area. No caller has to know how the text of a token is written, and a caller can't pass the
/// wrong slice of a formula. It also recognizes the patterns of tokens that several callers look for, e.g.
/// the tokens of a reference.
/// </summary>
/// <remarks>
/// The literal constants (numbers, strings, errors and logical values) are read by the parser, their
/// only reader.
/// </remarks>
internal static class TokenParser
{
    private const string REF_ERROR = "#REF!";

    /// <summary>
    /// Reads formulas written in the <see cref="ReferenceStyle.A1"/> reference style.
    /// </summary>
    internal static readonly IReferenceStyle A1Style = new A1ReferenceStyle();

    /// <summary>
    /// Reads formulas written in the <see cref="ReferenceStyle.R1C1"/> reference style.
    /// </summary>
    internal static readonly IReferenceStyle R1C1Style = new R1C1ReferenceStyle();

    /// <summary>
    /// Read a <see cref="Token.SINGLE_SHEET_PREFIX"/> token, e.g. <c>'[1]Jane''s'!</c>.
    /// </summary>
    internal static void ParseSingleSheetPrefix(ReadOnlySpan<char> formula, Token token, out int? index, out string sheetName)
    {
        Debug.Assert(token.SymbolId == Token.SINGLE_SHEET_PREFIX);

        // There can be whitespaces after exclamation mark at the end of a token.
        var input = Text(formula, token).TrimEnd();
        var isEscaped = input[0] == '\'';
        input = isEscaped
            ? input.Slice(1, input.Length - 3) // second sheet name ends with TICK EXCLAMATION_MARK ('!).
            : input.Slice(0, input.Length - 1); // only strip exclamation mark

        // Parse optional WORKBOOK_INDEX
        input = ExtractWorkbookIndex(input, out index);

        var sheetRangeSpan = input;
        if (!isEscaped)
        {
            // SHEET_NAME
            sheetName = sheetRangeSpan.ToString();
            return;
        }

        // The ending '! have been stripped from escape
        sheetName = GetEscapedSheetName(input);
    }

    /// <summary>
    /// Read a <see cref="Token.SHEET_RANGE_PREFIX"/> token, e.g. <c>'[1]first:second'!</c>.
    /// </summary>
    internal static void ParseSheetRangePrefix(ReadOnlySpan<char> formula, Token token, out int? index, out string firstSheetName, out string secondSheetName)
    {
        Debug.Assert(token.SymbolId == Token.SHEET_RANGE_PREFIX);

        var input = Text(formula, token);
        var isEscaped = input[0] == '\'';
        input = isEscaped
            ? input.Slice(1, input.Length - 3) // second sheet name ends with TICK EXCLAMATION_MARK ('!).
            : input.Slice(0, input.Length - 1); // only strip exclamation mark

        // Parse optional WORKBOOK_INDEX
        input = ExtractWorkbookIndex(input, out index);

        var sheetRangeSpan = input;
        if (!isEscaped)
        {
            // SHEET_NAME ':' SHEET_NAME
            var endIndex = sheetRangeSpan.IndexOf(':');
            firstSheetName = sheetRangeSpan.Slice(0, endIndex).ToString();
            secondSheetName = sheetRangeSpan.Slice(endIndex + 1).ToString();
            return;
        }

        // Parse SHEET_NAME_SPECIAL which can contain escaped tick (') as double tick
        firstSheetName = GetEscapedSheetName(ref input, ':'); // Even escaped sheet name can't contain :
        secondSheetName = GetEscapedSheetName(input);
    }

    /// <summary>
    /// Read the workbook index of a <see cref="Token.BOOK_PREFIX"/> token, e.g. <c>[1]</c>.
    /// </summary>
    internal static int ParseBookPrefix(ReadOnlySpan<char> formula, Token token)
    {
        Debug.Assert(token.SymbolId == Token.BOOK_PREFIX);
        ExtractWorkbookIndex(Text(formula, token), out var index);
        Debug.Assert(index.HasValue);
        return index!.Value;
    }

    /// <summary>
    /// Read a <see cref="Token.NAME"/> token, i.e. a defined name or a name of a table.
    /// </summary>
    internal static string ParseName(ReadOnlySpan<char> formula, Token token)
    {
        Debug.Assert(token.SymbolId == Token.NAME);
        return Text(formula, token).ToString();
    }

    /// <summary>
    /// Read the name of a function from a token of a function name with its opening brace, e.g. <c>SUM (</c>.
    /// </summary>
    internal static ReadOnlySpan<char> ParseFunctionName(ReadOnlySpan<char> formula, Token token)
    {
        Debug.Assert(token.SymbolId is Token.USER_DEFINED_FUNCTION_NAME or Token.REF_FUNCTION_LIST or Token.CELL_FUNCTION_LIST);
        return FunctionName(Text(formula, token));
    }

    /// <summary>
    /// Does a <see cref="Token.CELL_FUNCTION_LIST"/> token name a function rather than a cell? <c>LOG10</c> is a
    /// cell in A1 too, but a cell function is a construct of a macro sheet, and Excel reads <c>LOG10(</c> as the
    /// function. No other function has a name that is also a cell.
    /// </summary>
    internal static bool IsFunctionNamedLikeCell(ReadOnlySpan<char> formula, Token token)
    {
        Debug.Assert(token.SymbolId == Token.CELL_FUNCTION_LIST);
        return FunctionName(Text(formula, token)).Equals("LOG10".AsSpan(), StringComparison.OrdinalIgnoreCase);
    }

    private static ReadOnlySpan<char> FunctionName(ReadOnlySpan<char> functionNameWithBrace)
    {
        // In most cases, there won't be any whitespace
        var endPosition = functionNameWithBrace[functionNameWithBrace.Length - 1] == '('
            ? functionNameWithBrace.Length - 1
            : functionNameWithBrace.LastIndexOf('(');
        return functionNameWithBrace.Slice(0, endPosition);
    }

    /// <summary>
    /// Read a <see cref="Token.BANG_NAME"/> token, e.g. <c>!SomeName</c>. A name can't be <c>TRUE</c> or
    /// <c>FALSE</c>, but the lexer can't exclude them from the name after the bang.
    /// </summary>
    /// <param name="formula">The formula the token was lexed from.</param>
    /// <param name="token">The token.</param>
    /// <param name="name">The name after the bang, even if it isn't a valid name.</param>
    /// <returns><c>true</c> if the name is a valid name.</returns>
    internal static bool TryParseBangName(ReadOnlySpan<char> formula, Token token, out string name)
    {
        Debug.Assert(token.SymbolId == Token.BANG_NAME);
        var nameText = Text(formula, token).Slice(1);
        name = nameText.ToString();
        return !nameText.Equals("TRUE".AsSpan(), StringComparison.OrdinalIgnoreCase) &&
               !nameText.Equals("FALSE".AsSpan(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Is there a space before the <c>@</c> of an <see cref="Token.INTERSECT"/> token? The lexer puts the
    /// whitespace before <c>@</c> into the token, so after a reference, the space is the intersection operator.
    /// A line break alone is not, the same as for a <see cref="Token.SPACE"/> token.
    /// </summary>
    internal static bool IsSpaceBeforeAt(ReadOnlySpan<char> formula, Token token)
    {
        Debug.Assert(token.SymbolId == Token.INTERSECT);
        var text = Text(formula, token);
        return text.Slice(0, text.IndexOf('@')).IndexOf(' ') >= 0;
    }

    /// <summary>
    /// Read a reference from the tokens at <paramref name="index"/>.
    /// <code>
    /// a1_reference
    ///     : A1_CELL
    ///     | A1_CELL COLON A1_CELL
    ///     | A1_SPAN_REFERENCE
    ///     ;
    /// </code>
    /// Both DFA tables emit these token IDs for a reference, so the pattern is the same in either style.
    /// </summary>
    /// <param name="style">The reference style the tokens were lexed in.</param>
    /// <param name="formula">The formula the tokens were lexed from.</param>
    /// <param name="tokens">The tokens of the formula. The last one is an end of file or an error token.</param>
    /// <param name="index">The index of the first token of the reference. When a reference is read, it is moved to the token after it.</param>
    /// <param name="area">The read reference.</param>
    /// <returns><c>true</c> if the tokens at <paramref name="index"/> are a reference.</returns>
    internal static bool TryReadReference(IReferenceStyle style, ReadOnlySpan<char> formula, List<Token> tokens, ref int index, out ReferenceArea area)
    {
        var first = tokens[index];
        if (first.SymbolId == Token.A1_SPAN_REFERENCE)
        {
            area = style.ParseReference(formula, first);
            index++;
            return true;
        }

        if (first.SymbolId != Token.A1_CELL)
        {
            area = default;
            return false;
        }

        area = style.ParseReference(formula, first);
        index++;
        if (index + 1 < tokens.Count && tokens[index].SymbolId == Token.COLON && tokens[index + 1].SymbolId == Token.A1_CELL)
        {
            var secondCell = style.ParseReference(formula, tokens[index + 1]);
            area = new ReferenceArea(area.First, secondCell.First);
            index += 2;
        }

        return true;
    }

    /// <summary>
    /// Read a <see cref="Token.BANG_REFERENCE"/> token, e.g. <c>!$A$1</c>.
    /// </summary>
    /// <returns><c>false</c> for a bang reference to a deleted cell (<c>!#REF!</c>), which has no reference to read.</returns>
    internal static bool TryParseBangReference(IReferenceStyle style, ReadOnlySpan<char> formula, Token token, out ReferenceArea area)
    {
        Debug.Assert(token.SymbolId == Token.BANG_REFERENCE);
        if (Text(formula, token).Slice(1).Equals(REF_ERROR.AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            area = default;
            return false;
        }

        area = style.ParseReference(formula, token);
        return true;
    }

    /// <summary>
    /// Read a <see cref="Token.INTRA_TABLE_REFERENCE"/> token, e.g. <c>[[#Data],[First]:[Last]]</c>.
    /// </summary>
    internal static void ParseIntraTableReference(ReadOnlySpan<char> formula, Token token, out StructuredReferenceArea area, out string? firstColumn, out string? lastColumn)
    {
        Debug.Assert(token.SymbolId == Token.INTRA_TABLE_REFERENCE);
        var input = Text(formula, token);

        // Skip first char, it's always '['
        var i = 1;
        if (input[i] == '#')
        {
            // Pattern is a KEYWORD
            area = GetArea(input, i);
            firstColumn = null;
            lastColumn = null;
            return;
        }

        if (input[i] != '[' && input[i] != ' ')
        {
            // Pattern is '[]', '[First]' or '[First:Last]' or '[First:[Last]]'
            // because simple column can't start with a space.
            area = StructuredReferenceArea.None;
            if (input[i] == ']')
            {
                // Pattern is '[]', i.e. whole table.
                firstColumn = null;
                lastColumn = null;
                return;
            }

            // Read simple column
            i = GetStructuredName(input, i, out firstColumn);
            if (i < input.Length && input[i] == ':')
                GetStructuredName(input, i + 1, out lastColumn);
            else
                lastColumn = null;

            return;
        }

        // Pattern is SPACED_LBRACKET INNER_REFERENCE SPACED_RBRACKET

        // Skip potential whitespaces at the beginning of a structured reference (SPACED_LBRACKET)
        i = SkipWhitespaces(input, i);
        area = StructuredReferenceArea.None;
        if (input[i + 1] == '#')
        {
            // Inner reference contains a keyword.
            var listItem = GetArea(input, ++i);
            i += GetLength(listItem) + 1;
            area |= listItem;

            // `INNER_REFERENCE : KEYWORD_LIST`, i.e. the keyword list is the whole inner
            // reference and no column range follows it (e.g. '[[#All]]').
            if (IsEndOfInnerReference(input, i))
            {
                firstColumn = null;
                lastColumn = null;
                return;
            }

            i = SkipComma(input, i);
        }

        if (input[i + 1] == '#')
        {
            // Item is a keyword list, either
            // * '[#Headers]' SPACED_COMMA '[#Data]'
            // * '[#Data]' SPACED_COMMA '[#Totals]'
            var listItem = GetArea(input, ++i);
            i += GetLength(listItem) + 1;
            area |= listItem;

            // As above, for a two keyword list (e.g. '[[#Headers],[#Data]]').
            if (IsEndOfInnerReference(input, i))
            {
                firstColumn = null;
                lastColumn = null;
                return;
            }

            i = SkipComma(input, i);
        }

        // KEYWORD_LIST can contain at most two item specifiers.
        // After keyword list, we get either a COLUMN or a COLUMN:COLUMN
        i = GetStructuredName(input, i, out firstColumn);
        if (i < input.Length && input[i] == ':')
            GetStructuredName(input, i + 1, out lastColumn);
        else
            lastColumn = null;
    }

    /// <summary>
    /// Read a <see cref="Token.DDE_ITEM"/> token. A tick inside is doubled, same as in a quoted sheet name.
    /// </summary>
    internal static string ParseDdeItem(ReadOnlySpan<char> formula, Token token)
    {
        Debug.Assert(token.SymbolId == Token.DDE_ITEM);

        // Strip the enclosing ticks. The lexer guarantees there is at least one character between them
        // and that the ticks inside come in pairs. Unlike a sheet name, an item has no length limit, so
        // it isn't unescaped through a stack buffer.
        var input = Text(formula, token);
        return input.Slice(1, input.Length - 2).ToString().Replace("''", "'");
    }

    /// <summary>
    /// Read a <see cref="Token.SINGLE_SHEET_PREFIX"/> token as the application and the topic of a DDE link
    /// (e.g. <c>Sdemo123|tik!</c>). It must have no workbook index and a non-empty part on each side of the
    /// first <c>|</c>.
    /// </summary>
    internal static bool TryParseDdeLinkPrefix(ReadOnlySpan<char> formula, Token token, out string application, out string topic)
    {
        ParseSingleSheetPrefix(formula, token, out var index, out var name);
        var separatorIndex = name.IndexOf('|');
        if (index is not null || separatorIndex <= 0 || separatorIndex == name.Length - 1)
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
    /// Does <paramref name="prefix"/> read back as the sheet prefix of <paramref name="name"/>, with no
    /// workbook index? A writer uses it to find out whether a prefix can stay unquoted.
    /// </summary>
    internal static bool ReadsBackAsSheetPrefix(string prefix, string name)
    {
        var tokens = RolexLexer.GetTokensA1(prefix.AsSpan());
        if (tokens.Count != 2 || tokens[0].SymbolId != Token.SINGLE_SHEET_PREFIX || tokens[0].Length != prefix.Length)
            return false;

        ParseSingleSheetPrefix(prefix.AsSpan(), tokens[0], out var workbookIndex, out var sheetName);
        return workbookIndex is null && sheetName == name;
    }

    private static ReadOnlySpan<char> Text(ReadOnlySpan<char> formula, Token token) => formula.Slice(token.StartIndex, token.Length);

    /// <summary>
    /// The text of a token of a reference, without the <c>!</c> of a bang reference.
    /// </summary>
    private static ReadOnlySpan<char> ReferenceText(ReadOnlySpan<char> formula, Token token)
    {
        Debug.Assert(token.SymbolId is Token.A1_CELL or Token.A1_SPAN_REFERENCE or Token.BANG_REFERENCE);
        var text = Text(formula, token);
        return token.SymbolId == Token.BANG_REFERENCE ? text.Slice(1) : text;
    }

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
        Span<char> buffer = stackalloc char[input.Length];
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
        Span<char> buffer = stackalloc char[input.Length];
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

    /// <summary>
    /// Parse the text of a reference in R1C1 mode.
    /// </summary>
    /// <param name="token">The text of a reference.</param>
    private static ReferenceArea ParseR1C1Reference(ReadOnlySpan<char> token)
    {
        var i = 0;
        var rowCol1 = ParseR1C1Reference(token, ref i);
        if (i == token.Length)
            return new ReferenceArea(rowCol1, rowCol1);

        if (token[i++] != ':')
            throw Bug();

        var rowCol2 = ParseR1C1Reference(token, ref i);
        return new ReferenceArea(rowCol1, rowCol2);
    }

    private static RowCol ParseR1C1Reference(ReadOnlySpan<char> token, ref int i)
    {
        if (token[i] is 'C' or 'c')
        {
            // Token is COLUMN. ROW must be after column, so there can't be one.
            var loneCol = ReadR1C1Axis(token, ref i);
            return new RowCol(None, 0, loneCol.Type, loneCol.Value, R1C1);
        }

        // It must be a row.
        if (token[i] is not ('R' or 'r'))
            throw Bug();

        var row = ReadR1C1Axis(token, ref i);

        // Token is ROW. Either it has ended or it is followed by :
        if (i == token.Length || token[i] is not ('C' or 'c'))
            return new RowCol(row.Type, row.Value, None, 0, R1C1);

        // Token is ROW COLUMN
        var col = ReadR1C1Axis(token, ref i);
        return new RowCol(row.Type, row.Value, col.Type, col.Value, R1C1);
    }

    /// <summary>
    /// Read the axis value. Can work for row or column.
    /// </summary>
    /// <param name="token">The span of a token.</param>
    /// <param name="currentIdx">Index where is <c>C</c>/<c>R</c>.</param>
    private static (ReferenceAxisType Type, int Value) ReadR1C1Axis(ReadOnlySpan<char> token, ref int currentIdx)
    {
        // There are three possibilities: C only, C[-14] and C123
        var i = currentIdx + 1;
        if (token.Length == i)
        {
            // We are at the end of a formula and the only thing that was left was C/R, an alias for C[0]/R[0]
            currentIdx = i;
            return (Relative, 0);
        }

        if (token[i] == '[')
        {
            // Axis is relative
            ++i;
            var isNegative = token[i] == '-';
            if (isNegative)
                ++i; // Skip sign character

            // Axis is relative and thus must have a position. No need to check
            // length in the loop, because corresponding there must be ]
            var position = 0;
            do
            {
                position = position * 10 + (token[i++] - '0');
            } while (token[i] >= '0' && token[i] <= '9');

            // Index is at the last character that has to be ']'
            currentIdx = ++i;
            return (Relative, isNegative ? -position : position);
        }

        // Axis is absolute or relative [0] without explicit number.
        var numberStart = i;
        var absoluteNumber = 0;
        while (i < token.Length && token[i] >= '0' && token[i] <= '9')
            absoluteNumber = absoluteNumber * 10 + (token[i++] - '0');

        currentIdx = i;

        // There is no number after 'C'/'R' => it's a shorthand for `C[0]`/`R[0]`
        if (i == numberStart)
            return (Relative, 0);

        // A written number is absolute, and rows and columns are numbered from 1. Only the
        // shorthand above and a bracketed `[0]` mean a relative zero. The grammar no longer
        // admits a bare `C0`, so a token should never reach here with one; this stays as a
        // guard, and counting the digits is what makes an absent number and a written zero
        // distinguishable at all.
        if (absoluteNumber == 0)
            throw new ParsingException(
                "An R1C1 axis number of 0 is not valid. Rows and columns are numbered from 1; " +
                "use 'R'/'C' or 'R[0]'/'C[0]' for an axis relative to the current cell.");

        return (Absolute, absoluteNumber);
    }

    /// <summary>
    /// Extract info about cell reference from the text of a reference in A1 mode.
    /// </summary>
    private static ReferenceArea ParseA1Reference(ReadOnlySpan<char> input)
    {
        // The point of this method is to be fast, not pretty. It assumes that input has
        // already been checked by lexer and thus will never be incorrect.
        var i = 0;
        var abs1 = IsAbsolute(input, i);
        if (abs1)
            i++;

        var colStart = IsLetter(input[i]);
        if (!colStart)
        {
            // A1_ROW ':' A1_ROW
            var row1 = ReadRow(input, ref i);
            i++; // Skip ':'
            var absRow2 = IsAbsolute(input, i);
            if (absRow2)
                i++; // Skip '$'

            var row2 = ReadRow(input, ref i);
            return new ReferenceArea(
                new RowCol(abs1 ? Absolute : Relative, row1, None, 0, A1),
                new RowCol(absRow2 ? Absolute : Relative, row2, None, 0, A1));
        }

        var col = ReadColumn(input, ref i);
        if (input[i] == ':')
        {
            // A1_COLUMN ':' A1_COLUMN
            i++; // Skip ':'
            var absCol2 = IsAbsolute(input, i);
            if (absCol2)
                i++;

            var col2 = ReadColumn(input, ref i);
            return new ReferenceArea(
                new RowCol(None, 0, abs1 ? Absolute : Relative, col, A1),
                new RowCol(None, 0, absCol2 ? Absolute : Relative, col2, A1));
        }

        var secondAbsolute = IsAbsolute(input, i);
        if (secondAbsolute)
        {
            // Skip $
            i++;
        }

        // A1_CELL | A1_AREA : A1_CELL ':' A1_CELL
        var row = ReadRow(input, ref i);

        var cell = new RowCol(secondAbsolute, row, abs1, col, A1);
        if (i == input.Length)
        {
            // A1_CELL
            return new ReferenceArea(cell, cell);
        }

        // A1_AREA, e.g. the reference of a bang reference `!A1:B2`
        i++; // Skip ':'
        var secondCell = ReadA1Cell(input, ref i);
        return new ReferenceArea(cell, secondCell);
    }

    private static RowCol ReadA1Cell(ReadOnlySpan<char> input, ref int i)
    {
        var colAbs = IsAbsolute(input, i);
        if (colAbs)
            i++;

        var col = ReadColumn(input, ref i);
        var rowAbs = IsAbsolute(input, i);
        if (rowAbs)
            i++;

        var row = ReadRow(input, ref i);
        return new RowCol(rowAbs, row, colAbs, col, A1);
    }

    private static bool IsAbsolute(ReadOnlySpan<char> input, int startIdx) => input[startIdx] == '$';

    // Call only when first char is column
    private static int ReadColumn(ReadOnlySpan<char> input, ref int startIdx)
    {
        var column = 0;
        var i = startIdx;

        do
        {
            var c = input[i];
            var letter = c < 'a' // A is before a
                ? c - 'A' + 1
                : c - 'a' + 1;
            column = column * 26 + letter;
            i++;
        } while (i < input.Length && IsLetter(input[i]));

        startIdx = i;
        return column;
    }

    private static int ReadRow(ReadOnlySpan<char> input, ref int startIdx)
    {
        var row = 0;
        var i = startIdx;
        do
        {
            var digit = input[i] - '0';
            row = row * 10 + digit;
            i++;
        } while (i < input.Length && input[i] >= '0' && input[i] <= '9');

        startIdx = i;
        return row;
    }

    /// <summary>
    /// Has the inner reference ended at <paramref name="i"/>, i.e. is only the SPACED_RBRACKET
    /// left? That is the `INNER_REFERENCE : KEYWORD_LIST` alternative, a keyword list with no
    /// column range after it.
    /// </summary>
    private static bool IsEndOfInnerReference(ReadOnlySpan<char> input, int i)
    {
        i = SkipWhitespaces(input, i);
        return i >= input.Length || input[i] == ']';
    }

    private static int SkipComma(ReadOnlySpan<char> input, int i)
    {
        // comma might be wrapped in whitespaces.
        i = SkipWhitespaces(input, i);

        Debug.Assert(input[i] == ',');
        i++;
        i = SkipWhitespaces(input, i);
        return i;
    }

    private static int SkipWhitespaces(ReadOnlySpan<char> input, int i)
    {
        for (; i < input.Length; i++)
        {
            if (!IsWhiteSpace(input[i]))
                break;
        }

        return i;
    }

    /// <summary>
    /// Read a structured name until the end bracket or column
    /// </summary>
    /// <param name="input">Input span.</param>
    /// <param name="startIdx">First index of expected name. It will either contain a bracket or first letter of column name.</param>
    /// <param name="columnName">Parsed name.</param>
    private static int GetStructuredName(ReadOnlySpan<char> input, int startIdx, out string columnName)
    {
        Span<char> buffer = stackalloc char[input.Length];
        var bufferIdx = 0;
        var i = startIdx + (input[startIdx] == '[' ? 1 : 0);
        var c = input[i];
        for (; c is not ']' and not ':'; c = input[++i])
        {
            if (c == '\'')
                c = input[++i];

            buffer[bufferIdx++] = c;
        }

        columnName = buffer.Slice(0, bufferIdx).ToString();
        return i + (c == ']' ? 1 : 0); // char after last bracket
    }

    private static StructuredReferenceArea GetArea(ReadOnlySpan<char> input, int i)
    {
        // Tokenizer has taken care that input can contain only valid values = only first two chars is enough.
        var item = input[i + 1] switch
        {
            'A' => StructuredReferenceArea.All,
            'a' => StructuredReferenceArea.All,
            'D' => StructuredReferenceArea.Data,
            'd' => StructuredReferenceArea.Data,
            'H' => StructuredReferenceArea.Headers,
            'h' => StructuredReferenceArea.Headers,
            'T' => input[i + 2] switch
            {
                'O' => StructuredReferenceArea.Totals,
                'o' => StructuredReferenceArea.Totals,
                'H' => StructuredReferenceArea.ThisRow,
                'h' => StructuredReferenceArea.ThisRow,
                _ => throw new NotSupportedException()
            },
            _ => throw new NotSupportedException()
        };
        return item;
    }

    private static int GetLength(StructuredReferenceArea item)
    {
        return item switch
        {
            StructuredReferenceArea.All => 4,
            StructuredReferenceArea.Data => 5,
            StructuredReferenceArea.Headers => 8,
            StructuredReferenceArea.ThisRow => 9,
            StructuredReferenceArea.Totals => 7,
            _ => throw new InvalidOperationException()
        };
    }

    private static bool IsWhiteSpace(char c)
    {
        return c is ' ' or '\n' or '\r';
    }

    private static bool IsLetter(char c) => (c is >= 'A' and <= 'Z') || (c is >= 'a' and <= 'z');

    private static Exception Bug()
    {
        throw new InvalidOperationException("Bug in token parser. Token doesn't have expected format.");
    }

    private sealed class A1ReferenceStyle : IReferenceStyle
    {
        public DfaEntry[] DfaTable => RolexA1Dfa.DfaTable;

        public ReferenceArea ParseReference(ReadOnlySpan<char> formula, Token token) => ParseA1Reference(ReferenceText(formula, token));

        public RowCol ParseCellFunction(ReadOnlySpan<char> formula, Token token)
        {
            Debug.Assert(token.SymbolId == Token.CELL_FUNCTION_LIST);
            var i = 0;
            return ReadA1Cell(Text(formula, token), ref i);
        }
    }

    private sealed class R1C1ReferenceStyle : IReferenceStyle
    {
        public DfaEntry[] DfaTable => RolexR1C1Dfa.DfaTable;

        public ReferenceArea ParseReference(ReadOnlySpan<char> formula, Token token) => ParseR1C1Reference(ReferenceText(formula, token));

        public RowCol ParseCellFunction(ReadOnlySpan<char> formula, Token token)
        {
            Debug.Assert(token.SymbolId == Token.CELL_FUNCTION_LIST);
            var i = 0;
            return ParseR1C1Reference(Text(formula, token), ref i);
        }
    }
}
