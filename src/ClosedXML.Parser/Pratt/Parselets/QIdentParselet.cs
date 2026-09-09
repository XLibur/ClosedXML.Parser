using System;

namespace ClosedXML.Parser.Pratt.Parselets;

/// <summary>
/// Parses a reference whose sheet prefix is quoted, e.g. <c>'New York'!A1</c>. The lexer
/// hands the whole quoted run over as one <see cref="TokenType.QIdent"/> token, so the
/// surrounding apostrophes are stripped and the doubled ones collapsed here.
/// </summary>
/// <remarks>
/// Serializers quote a sheet name whenever <see cref="NameUtils.ShouldQuote"/> says so, so
/// without this parselet the Pratt parser cannot read back what they write - not only the
/// obvious <c>'New York'!A1</c>, but any name holding an apostrophe or reading as a
/// logical literal.
/// </remarks>
internal class QIdentParselet<TScalar, T, TContext> : IPrefixParselet<T, TContext>
{
    private readonly IAstFactory<TScalar, T, TContext> _factory;
    private readonly Parser<T, TContext> _parser;

    public QIdentParselet(IAstFactory<TScalar, T, TContext> factory, Parser<T, TContext> parser)
    {
        _factory = factory;
        _parser = parser;
    }

    public Node<T> Parse(TContext ctx, Token token)
    {
        // A quoted ident is only ever a sheet prefix, so it must be followed by a bang:
        // * 'sheet'!A1, 'sheet'!A1:B2, 'sheet'!A:B, 'sheet'!1:2
        // * 'sheet'!name
        // * 'first:last'!A1 and the same reference shapes
        if (_parser.LookAhead(1).Type != TokenType.Bang)
            throw Unparseable(token);

        // The token text still carries the apostrophes the lexer matched on.
        var quoted = token.GetText(_parser.Input);
        var content = quoted.Slice(1, quoted.Length - 2);

        var bangToken = _parser.Consume(TokenType.Bang);
        var prefixRange = token.Range.ExtendRight(bangToken.Range);

        // No need to check the token type, at EoF nothing matches.
        var refToken = _parser.Consume();

        // A sheet name cannot contain a colon, so a colon here always separates the two
        // sheets of a 3D reference.
        var separator = content.IndexOf(':');
        if (separator >= 0)
        {
            var firstSheet = Unescape(content.Slice(0, separator));
            var lastSheet = Unescape(content.Slice(separator + 1));
            if (!IsSheetName(firstSheet) || !IsSheetName(lastSheet))
                throw Unparseable(token);

            // `sheet1:sheet2!name` is not a thing, a 3D prefix only takes a reference.
            if (!_parser.TryReferenceA1(refToken, out var area3D, out var area3DRange))
                throw Unparseable(token);

            var range3D = prefixRange.ExtendRight(area3DRange);
            var reference3D = _factory.Reference3D(ctx, range3D, firstSheet, lastSheet, area3D);
            return new Node<T>(reference3D, range3D);
        }

        var sheet = Unescape(content);
        if (!IsSheetName(sheet))
            throw Unparseable(token);

        // Check for area `'sheet'!A1:B2`, cell `'sheet'!A1`, colspan `'sheet'!A:B` or
        // rowspan `'sheet'!1:2`.
        if (_parser.TryReferenceA1(refToken, out var area, out var areaRange))
        {
            var range = prefixRange.ExtendRight(areaRange);
            var reference = _factory.SheetReference(ctx, range, sheet, area);
            return new Node<T>(reference, range);
        }

        // Check for `'sheet'!name`
        if (_parser.TryGetName(refToken, out var name))
        {
            var range = prefixRange.ExtendRight(refToken.Range);
            var sheetName = _factory.SheetName(ctx, range, sheet, name.ToString()); // String allocation, needed for the IAstFactory
            return new Node<T>(sheetName, range);
        }

        throw Unparseable(token);
    }

    /// <summary>
    /// A name is only quoted because it holds something a bare name cannot, so the
    /// characters Excel rejects outright are still rejected, as is the workbook index of
    /// an external reference - the unquoted path does not support those either.
    /// </summary>
    private static bool IsSheetName(string sheet)
    {
        return NameUtils.IsSheetNameValid(sheet.AsSpan());
    }

    /// <summary>
    /// Collapse the doubled apostrophes. The lexer ends the token at the first apostrophe
    /// that is not doubled, so every one left inside the content is half of a pair.
    /// </summary>
    private static string Unescape(ReadOnlySpan<char> name)
    {
        return name.ToString().Replace("''", "'");
    }

    private static ParsingException Unparseable(Token token)
    {
        return new ParsingException($"Unable to parse value starting from position {token.Range.Start}.");
    }
}
