using System.Globalization;

namespace ClosedXML.Parser.Pratt.Parselets;

/// <summary>
/// Get a number node from a <see cref="TokenType.Number"/> token.
/// </summary>
/// <remarks>
/// <c>double.Parse</c> parses even <c>NaN</c> or <c>∞</c>, but we can never receive such text
/// from the lexer.
/// </remarks>
internal class NumberParselet<TScalar, T, TContext> : IPrefixParselet<T, TContext>
{
    private readonly IAstFactory<TScalar, T, TContext> _factory;
    private readonly Parser<T, TContext> _parser;

    public NumberParselet(IAstFactory<TScalar, T, TContext> factory, Parser<T, TContext> parser)
    {
        _factory = factory;
        _parser = parser;
    }

    public Node<T> Parse(TContext ctx, Token token)
    {
        var text = token.GetText(_parser.Input);
        var number = double.Parse(text, NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture);
        var node = _factory.NumberNode(ctx, token.Range, number);
        return new Node<T>(node, token.Range);
    }
}
