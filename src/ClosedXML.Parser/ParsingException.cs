using XLibur.Parser.Pratt;

namespace XLibur.Parser;

/// <summary>
/// Indicates an error during parsing. In most cases, unexpected token.
/// </summary>
public class ParsingException : Exception
{
    internal ParsingException(string message, int position) : base(message)
    {
        Position = position;
    }

    /// <summary>
    /// Zero-based char offset into the input text where parsing stopped. It is the offset the
    /// message names. For a failure without a more precise place (<see cref="ReferenceParser.ParseA1"/>
    /// of a whole text) it is 0.
    /// </summary>
    public int Position { get; }

    /// <summary>
    /// There are problems with underlying stream.
    /// </summary>
    internal static ParsingException UnpairedSurrogate(int codepoint, int position)
    {
        throw new ParsingException($"Found an unpaired surrogate 0x{codepoint:X4} at {position}.", position);
    }

    /// <summary>
    /// Token has a start and end indicator, but no end indicator was found.
    /// </summary>
    internal static Exception UnterminatedLiteral(int start, char delimiter)
    {
        throw new ParsingException($"An unterminated literal (delimiter {delimiter}) found at position {start}.", start);
    }

    /// <summary>
    /// A token was started to be parsed, but is not complete or there is a problem with it.
    /// </summary>
    internal static Exception TokenPartialMatch(int start, TokenType type)
    {
        throw new ParsingException($"Token {type} was parsed from position {start}, but was only partially matched.", start);
    }

    /// <summary>
    /// Lexer has no idea which token it should start to parse.
    /// </summary>
    internal static Exception UnableToSelectToken(int start)
    {
        throw new ParsingException($"Unable to determine a token at position {start}.", start);
    }
}
