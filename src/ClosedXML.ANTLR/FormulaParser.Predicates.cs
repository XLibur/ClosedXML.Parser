using ParserToken = XLibur.Parser.Token;
using TokenParser = XLibur.Parser.TokenParser;
using SheetPrefix = XLibur.Parser.SheetPrefix;

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
    /// Does the token before the current one end with a space? The lexer puts the whitespace after an operator into
    /// its token, so the space intersection operator of e.g. <c>(A1) B2</c> is the end of the <c>) </c> token and
    /// there is no <c>SPACE</c> token for it.
    /// </summary>
    private bool IsSpaceAfterPreviousToken()
    {
        var previousToken = TokenStream.LT(-1);
        return previousToken is not null &&
               TokenParser.IsSpaceAtEnd(previousToken.Text.AsSpan(), WholeToken(previousToken.Type, previousToken.Text));
    }

    /// <summary>
    /// Does an argument list follow the function call or the expression in braces that starts at the current token,
    /// with no whitespace before its brace? ANTLR doesn't evaluate a predicate that comes after a consumed token
    /// while it predicts an alternative, so the check of <c>expression_call</c> has to be here, at its left edge:
    /// without it, the prediction takes <c>IF(A1,B1,C1) (D1)</c>, an intersection, for a call.
    /// </summary>
    private bool IsExpressionCall()
    {
        var first = CurrentToken.Type;
        var isPrefix = first == SINGLE_SHEET_PREFIX || first == BOOK_PREFIX;
        if (isPrefix)
        {
            // A prefix starts a call only when the name of a function follows it.
            if (TokenStream.LT(2).Type != USER_DEFINED_FUNCTION_NAME)
                return false;
        }
        else if (first != OPEN_BRACE && first != USER_DEFINED_FUNCTION_NAME && first != REF_FUNCTION_LIST && first != CELL_FUNCTION_LIST)
        {
            return false;
        }

        var depth = 0;
        for (var i = 1; ; i++)
        {
            var token = TokenStream.LT(i);
            switch (token.Type)
            {
                case Antlr4.Runtime.TokenConstants.EOF:
                    return false;
                case OPEN_BRACE:
                case USER_DEFINED_FUNCTION_NAME:
                case REF_FUNCTION_LIST:
                case CELL_FUNCTION_LIST:
                    depth++;
                    break;
                case CLOSE_BRACE:
                    depth--;
                    if (depth == 0)
                    {
                        return TokenStream.LT(i + 1).Type == OPEN_BRACE &&
                               !TokenParser.IsSpaceAtEnd(token.Text.AsSpan(), WholeToken(token.Type, token.Text));
                    }

                    break;
            }
        }
    }

    /// <summary>
    /// A predicate gets the text of one token, so the token spans all of it.
    /// </summary>
    private static ParserToken WholeToken(int symbolId, string text) => new(symbolId, 0, text.Length);
}
