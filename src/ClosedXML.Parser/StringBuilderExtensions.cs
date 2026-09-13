using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ClosedXML.Parser;

/// <summary>
/// Extension methods for building formulas.
/// </summary>
internal static class StringBuilderExtensions
{
    /// <summary>Length of <c>int.MinValue</c> (<c>-2147483648</c>), the longest <see cref="int"/> text.</summary>
    private const int MaxInt32Length = 11;

    /// <summary>
    /// Append an <see cref="int"/> formatted with the invariant culture.
    /// </summary>
    /// <remarks>
    /// A formula is a machine format, not a display one. <see cref="StringBuilder.Append(int)"/>
    /// formats with <see cref="CultureInfo.CurrentCulture"/>, so under a culture whose
    /// <see cref="NumberFormatInfo.NegativeSign"/> is the Unicode MINUS SIGN U+2212 (sv-SE,
    /// fi-FI, nb-NO, ...) a negative R1C1 offset would be written as <c>RC[−1]</c>. The
    /// readers only accept the ASCII hyphen-minus, so such output can't be parsed back.
    /// </remarks>
    public static StringBuilder AppendInvariant(this StringBuilder sb, int value)
    {
#if NETSTANDARD2_0
        return sb.Append(value.ToString(CultureInfo.InvariantCulture));
#else
        Span<char> buffer = stackalloc char[MaxInt32Length];
        return value.TryFormat(buffer, out var length, default, CultureInfo.InvariantCulture)
            ? sb.Append(buffer.Slice(0, length))
            : sb.Append(value.ToString(CultureInfo.InvariantCulture));
#endif
    }

    /// <summary>
    /// Append a <see cref="SheetPrefix"/>, which decides the quotes it needs.
    /// </summary>
    public static StringBuilder AppendPrefix(this StringBuilder sb, SheetPrefix prefix)
    {
        return prefix.Append(sb);
    }

    public static StringBuilder AppendEscapedSheetName(this StringBuilder sb, string sheetName)
    {
        var startIndex = sb.Length;
        return sb.Append(sheetName).Replace("'", "''", startIndex, sheetName.Length);
    }

    /// <summary>
    /// Append an item of a DDE link, enclosed in ticks and with a tick doubled (e.g. <c>'It''s'</c>).
    /// </summary>
    public static StringBuilder AppendDdeItem(this StringBuilder sb, string item)
    {
        sb.Append('\'');
        var startIndex = sb.Length;
        return sb.Append(item).Replace("'", "''", startIndex, item.Length).Append('\'');
    }

    public static StringBuilder AppendReferenceSeparator(this StringBuilder sb)
    {
        return sb.Append('!');
    }

    public static StringBuilder AppendBookIndex(this StringBuilder sb, int bookIndex)
    {
        return sb.Append('[').AppendInvariant(bookIndex).Append(']');
    }

    public static StringBuilder AppendFunction(this StringBuilder sb, ModContext ctx, SymbolRange range, ReadOnlySpan<char> functionName, IReadOnlyList<TransformedSymbol> arguments)
    {
        return sb.Append(functionName).AppendArguments(ctx, range, arguments);
    }

    public static StringBuilder AppendRef(this StringBuilder sb, ReferenceArea? reference)
    {
        return reference is null ? sb.Append("#REF!") : reference.Value.Append(sb);
    }

    public static StringBuilder AppendRef(this StringBuilder sb, RowCol? rowCol)
    {
        if (rowCol is null)
            return sb.Append("#REF!");

        rowCol.Value.Append(sb);
        return sb;
    }

    public static StringBuilder AppendStartFragment(this StringBuilder sb, ModContext ctx, SymbolRange symbolRange, TransformedSymbol nestedNode)
    {
        var formula = ctx.Formula;
        for (var i = symbolRange.Start; i < nestedNode.OriginalRange.Start; ++i)
            sb.Append(formula[i]);

        return sb;
    }

    public static StringBuilder AppendMiddleFragment(this StringBuilder sb, ModContext ctx, TransformedSymbol beforeNode, TransformedSymbol afterNode)
    {
        var formula = ctx.Formula;
        for (var i = beforeNode.OriginalRange.End; i < afterNode.OriginalRange.Start; ++i)
            sb.Append(formula[i]);

        return sb;
    }

    public static StringBuilder AppendEndFragment(this StringBuilder sb, ModContext ctx, SymbolRange symbolRange, TransformedSymbol nestedNode)
    {
        var formula = ctx.Formula;
        for (var i = nestedNode.OriginalRange.End; i < symbolRange.End; ++i)
            sb.Append(formula[i]);

        return sb;
    }

    public static StringBuilder AppendArguments(this StringBuilder sb, ModContext ctx, SymbolRange range, IReadOnlyList<TransformedSymbol> arguments)
    {
        if (arguments.Count == 0)
        {
            var braceIdx = GetStartBraceIndex(ctx, range, range.End);
            var braces = ctx.Formula.AsSpan().Slice(braceIdx, range.End - braceIdx);
            sb.Append(braces);
        }
        else
        {
            sb
                .AppendStartBrace(ctx, range, arguments[0])
                .AppendArguments(ctx, arguments)
                .AppendEndFragment(ctx, range, arguments[arguments.Count - 1]);
        }

        return sb;
    }

    /// <summary>
    /// Append what stands before the argument list of a call, as it was written: the sheet prefix or the
    /// book prefix, the name of the function or the called cell, and the whitespace among them. Used when
    /// an argument changed but the call itself didn't, so the call keeps its own text.
    /// </summary>
    public static StringBuilder AppendOriginalCallee(this StringBuilder sb, ModContext ctx, SymbolRange range, IReadOnlyList<TransformedSymbol> arguments)
    {
        var argumentsStart = arguments.Count == 0 ? range.End : arguments[0].OriginalRange.Start;
        var braceIdx = GetStartBraceIndex(ctx, range, argumentsStart);
        return sb.Append(ctx.Formula.AsSpan().Slice(range.Start, braceIdx - range.Start));
    }

    private static StringBuilder AppendStartBrace(this StringBuilder sb, ModContext ctx, SymbolRange range, TransformedSymbol firstNode)
    {
        var firstNodeStart = firstNode.OriginalRange.Start;
        var braceIdx = GetStartBraceIndex(ctx, range, firstNodeStart);
        for (var j = braceIdx; j < firstNodeStart; ++j)
            sb.Append(ctx.Formula[j]);

        return sb;
    }

    private static int GetStartBraceIndex(ModContext ctx, SymbolRange range, int nodeStart)
    {
        var formula = ctx.Formula;
        var braceIdx = nodeStart - 1;
        for (; braceIdx > range.Start; --braceIdx)
        {
            if (formula[braceIdx] == '(')
                return braceIdx;
        }

        throw new InvalidOperationException("No opening brace found.");
    }

    private static StringBuilder AppendArguments(this StringBuilder sb, ModContext ctx, IReadOnlyList<TransformedSymbol> arguments)
    {
        if (arguments.Count > 0)
            sb.Append(arguments[0].AsSpan());

        for (var i = 1; i < arguments.Count; ++i)
        {
            sb.AppendMiddleFragment(ctx, arguments[i - 1], arguments[i]);
            sb.Append(arguments[i].AsSpan());
        }

        return sb;
    }

#if NETSTANDARD2_0
    /// <summary>
    /// Compatibility method for NETStandard 2.0, which doesn't have methods with <c>Span</c> arguments.
    /// </summary>
    public static StringBuilder Append(this StringBuilder sb, ReadOnlySpan<char> span)
    {
        foreach (var c in span)
            sb.Append(c);

        return sb;
    }
#endif
}