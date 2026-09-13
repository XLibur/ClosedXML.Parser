using ParserToken = ClosedXML.Parser.Token;
using TokenParser = ClosedXML.Parser.TokenParser;
using SheetPrefix = ClosedXML.Parser.SheetPrefix;

/// <summary>
/// Semantic predicates of <c>FormulaParser.g4</c>. They read a token through the <c>TokenParser</c> of the
/// recursive descent parser, so both parsers decide the same way.
/// </summary>
/// <remarks>
/// A predicate gets the text of the current token, and ANTLR evaluates it on whatever token comes next when it
/// predicts an alternative. A token of another type can't start the alternative, so the predicate is false for it.
/// </remarks>
public partial class FormulaParser
{
    /// <summary>
    /// Is a <c>SINGLE_SHEET_PREFIX</c> token the application and the topic of a DDE link (e.g. <c>Sdemo123|tik!</c>)?
    /// It must have no workbook index and a non-empty part on each side of the first <c>|</c>.
    /// </summary>
    private bool IsDdeLinkPrefix(string token)
    {
        return CurrentToken.Type == SINGLE_SHEET_PREFIX &&
               SheetPrefix.ReadSingle(token.AsSpan(), WholeToken(ParserToken.SINGLE_SHEET_PREFIX, token)).TryGetDdeLink(out _, out _);
    }

    /// <summary>
    /// Is the name of a <c>BANG_NAME</c> token (e.g. <c>!SomeName</c>) a valid name? A name can't be <c>TRUE</c> or
    /// <c>FALSE</c>, but the lexer can't exclude them from the name after the bang.
    /// </summary>
    private bool IsBangName(string token)
    {
        return CurrentToken.Type == BANG_NAME &&
               TokenParser.TryParseBangName(token.AsSpan(), WholeToken(ParserToken.BANG_NAME, token), out _);
    }

    /// <summary>
    /// Is there a space before the <c>@</c> of an <c>INTERSECT</c> token (e.g. <c> @</c>)? The lexer puts the
    /// whitespace before <c>@</c> into the token, so after a reference, the space is the intersection operator. A line
    /// break alone is not, the same as for a <c>SPACE</c> token.
    /// </summary>
    private bool IsSpaceBeforeAt(string token)
    {
        return CurrentToken.Type == INTERSECT &&
               TokenParser.IsSpaceBeforeAt(token.AsSpan(), WholeToken(ParserToken.INTERSECT, token));
    }

    /// <summary>
    /// A predicate gets the text of one token, so the token spans all of it.
    /// </summary>
    private static ParserToken WholeToken(int symbolId, string text) => new(symbolId, 0, text.Length);
}
