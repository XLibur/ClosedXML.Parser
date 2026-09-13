using System;
using System.Text;

namespace ClosedXML.Parser;

/// <summary>
/// A symbol that represents a transformed symbol value. Should be used during AST transformation.
/// </summary>
internal readonly struct TransformedSymbol
{
    private readonly string _formulaText;

    private readonly SymbolRange _range;

    /// <summary>
    /// The text that replaced symbol or null, if there was no change.
    /// </summary>
    private readonly string? _transformedText;

    private TransformedSymbol(string formulaText, string? transformedText, SymbolRange range) : this()
    {
        _formulaText = formulaText;
        _transformedText = transformedText;
        _range = range;
    }

    /// <summary>
    /// Range of the symbol in original formula.
    /// </summary>
    internal SymbolRange OriginalRange => _range;

    /// <summary>
    /// Length of the transformed symbol.
    /// </summary>
    internal int Length => _transformedText?.Length ?? _range.Length;

    /// <summary>
    /// Is the symbol the text of the original formula, i.e. was nothing in it changed? A node whose
    /// parts are all original is written back as it was, character for character.
    /// </summary>
    internal bool IsOriginal => _transformedText is null;

    /// <summary>
    /// Create a symbol that is different from what was in the original formula.
    /// </summary>
    /// <param name="formula">Text of whole formula.</param>
    /// <param name="range">Range of the symbol in the formula.</param>
    /// <param name="transformedSymbol">The string of a transformed symbol.</param>
    public static TransformedSymbol ToText(string formula, SymbolRange range, string transformedSymbol)
    {
        return new TransformedSymbol(formula, transformedSymbol, range);
    }

    /// <summary>
    /// Create a new symbol represented by a substring of an original formula. Generally used when
    /// there is no modification of the symbol (i.e. just pass it as it is).
    /// </summary>
    /// <param name="formula">Text of whole formula.</param>
    /// <param name="range">Range of the symbol in the formula.</param>
    public static TransformedSymbol CopyOriginal(string formula, SymbolRange range)
    {
        return new TransformedSymbol(formula, null, range);
    }

    /// <summary>
    /// Get content of the symbol as a span. Doesn't allocate memory.
    /// </summary>
    public ReadOnlySpan<char> AsSpan()
    {
        if (_transformedText is not null)
            return _transformedText.AsSpan();

        return _formulaText.AsSpan(_range.Start, _range.Length);
    }

    /// <summary>
    /// Get symbol as a text with extra text at the end.
    /// </summary>
    /// <param name="append">Text to append at the end of the symbol text.</param>
    public string ToString(ReadOnlySpan<char> append)
    {
        // Nothing to append is the common case, and a symbol nothing changed is the common symbol,
        // so neither needs a builder: the text is either the replacement or a slice of the formula.
        if (append.Length == 0)
            return _transformedText ?? _formulaText.Substring(_range.Start, _range.Length);

        var text = AsSpan();
        return new StringBuilder(text.Length + append.Length)
            .Append(text)
            .Append(append)
            .ToString();
    }

    /// <summary>
    /// Get symbol text representation.
    /// </summary>
    public override string ToString()
    {
        return ToString(string.Empty.AsSpan());
    }
}