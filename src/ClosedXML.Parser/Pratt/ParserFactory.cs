using ClosedXML.Parser.Pratt.Parselets;

namespace ClosedXML.Parser.Pratt;

/// <summary>
/// Builds the Pratt parser, an unfinished second parser that nothing in the library uses.
/// </summary>
/// <remarks>
/// The parser that reads a formula is <see cref="FormulaParser{TScalarValue,TNode,TContext}"/>, a
/// recursive descent over the tokens of <see cref="Rolex.RolexLexer"/>:
/// <see cref="FormulaConverter"/> reaches it and nothing reaches this. Every call to
/// <see cref="Create{TScalar,TNode,TContext}"/> comes from a test.
/// <para>
/// It is also far from being an alternative. The registrations below cover nine token types out of
/// the lexer's twenty-eight, so there is no parselet for a text, an error, a comparison, a square
/// identifier, a comma or a brace, and <see cref="TokenType.Minus"/> is registered as an operation
/// only — a formula as ordinary as <c>-1</c> or <c>SUM(1)</c> has nothing to parse it. It carries
/// its own lexer and its own reader of A1 references, both of which duplicate what the live path
/// does from the generated DFA tables.
/// </para>
/// <para>
/// Treat it as a prototype kept for reference, not as code on the way to anything: a fix made here
/// does not reach a formula any caller of this library parses, and a fix made to the live reader
/// does not reach here.
/// </para>
/// </remarks>
internal static class ParserFactory
{
    public static Parser<TNode, TContext> Create<TScalar, TNode, TContext>(
        IAstFactory<TScalar, TNode, TContext> factory)
    {
        var parser = new Parser<TNode, TContext>();

        // Register prefix parselets
        parser.Register(TokenType.Number, new NumberParselet<TScalar, TNode, TContext>(factory, parser));
        parser.Register(TokenType.LeftParen, new GroupParselet<TNode, TContext>(parser));
        parser.Register(TokenType.Ident, new IdentParselet<TScalar, TNode, TContext>(factory, parser));
        parser.Register(TokenType.QIdent, new QIdentParselet<TScalar, TNode, TContext>(factory, parser));

        // Register operation parselets
        parser.Register(TokenType.Plus, new BinaryOpParselet<TScalar, TNode, TContext>(factory, parser, BinaryOperation.Addition, BindingPower.Addition));
        parser.Register(TokenType.Minus, new BinaryOpParselet<TScalar, TNode, TContext>(factory, parser, BinaryOperation.Subtraction, BindingPower.Subtraction));
        parser.Register(TokenType.Mul, new BinaryOpParselet<TScalar, TNode, TContext>(factory, parser, BinaryOperation.Multiplication, BindingPower.Multiplication));
        parser.Register(TokenType.Div, new BinaryOpParselet<TScalar, TNode, TContext>(factory, parser, BinaryOperation.Division, BindingPower.Division));
        parser.Register(TokenType.Pow, new BinaryOpParselet<TScalar, TNode, TContext>(factory, parser, BinaryOperation.Power, BindingPower.Exponentiation));

        return parser;
    }
}
