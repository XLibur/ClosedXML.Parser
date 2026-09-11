namespace Antlr2Rolex;

// The syntax tree of an ANTLR lexer grammar, limited to the constructs the converter supports.

internal sealed record Rule(string Name, bool IsFragment, Alternation Body, int Line);

internal sealed record Alternation(List<Sequence> Alternatives, int Line);

internal sealed record Sequence(List<Element> Elements, int Line);

/// <param name="Suffix"><c>?</c>, <c>*</c> or <c>+</c>, or null for an element without one.</param>
internal sealed record Element(Atom Atom, char? Suffix);

internal abstract record Atom(int Line);

/// <param name="Raw">The literal between its quotes, with its escapes not yet decoded.</param>
internal sealed record Literal(string Raw, int Line) : Atom(Line);

/// <param name="From">The raw literal of the lower bound of <c>'a' .. 'z'</c>.</param>
/// <param name="To">The raw literal of the upper bound.</param>
internal sealed record CharRange(string From, string To, int Line) : Atom(Line);

/// <param name="Text">The set including its brackets, e.g. <c>[A-Za-z]</c>.</param>
internal sealed record CharSet(string Text, int Line) : Atom(Line);

internal sealed record RuleRef(string Name, int Line) : Atom(Line);

internal sealed record Block(Alternation Body, int Line) : Atom(Line);

/// <summary>
/// Parses the part of the ANTLR lexer grammar syntax that the converter supports. Anything else
/// is rejected with its line, rather than skipped or approximated.
/// </summary>
internal sealed class AntlrGrammarParser
{
    private enum Kind
    {
        Id,
        Literal,
        CharSet,
        Colon,
        Semi,
        Pipe,
        LParen,
        RParen,
        Question,
        Star,
        Plus,
        Range,
        Eof,
    }

    private readonly record struct Token(Kind Kind, string Text, int Line);

    private readonly string _text;
    private int _pos;
    private int _line;
    private Token _current;

    /// <param name="text">Grammar text.</param>
    /// <param name="firstLine">Line of the first character of <paramref name="text"/>, for errors.</param>
    public AntlrGrammarParser(string text, int firstLine = 1)
    {
        _text = text;
        _line = firstLine;
        _current = Next();
    }

    /// <summary>
    /// Parses a whole grammar: the <c>lexer grammar Name;</c> header and the rules after it.
    /// </summary>
    public List<Rule> ParseGrammar()
    {
        ExpectKeyword("lexer");
        ExpectKeyword("grammar");
        Expect(Kind.Id, "a grammar name");
        Expect(Kind.Semi, "';' after the grammar name");
        return ParseRules();
    }

    /// <summary>
    /// Parses rules up to the end of the text.
    /// </summary>
    public List<Rule> ParseRules()
    {
        var rules = new List<Rule>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        while (_current.Kind != Kind.Eof)
        {
            var rule = ParseRule();
            if (!names.Add(rule.Name))
                throw new AntlrGrammarException(rule.Line, $"{rule.Name} is defined twice");

            rules.Add(rule);
        }

        return rules;
    }

    private Rule ParseRule()
    {
        var isFragment = _current is { Kind: Kind.Id, Text: "fragment" };
        if (isFragment)
            Advance();

        var name = Expect(Kind.Id, "a rule name");
        if (!char.IsAsciiLetterUpper(name.Text[0]))
            throw new AntlrGrammarException(name.Line, $"{name.Text}: only lexer rules are supported, and their names start with a capital letter");

        Expect(Kind.Colon, $"':' after {name.Text}");
        var body = ParseAlternation();
        Expect(Kind.Semi, $"';' at the end of {name.Text}");
        return new Rule(name.Text, isFragment, body, name.Line);
    }

    private Alternation ParseAlternation()
    {
        var line = _current.Line;
        var alternatives = new List<Sequence> { ParseSequence() };
        while (_current.Kind == Kind.Pipe)
        {
            Advance();
            alternatives.Add(ParseSequence());
        }

        return new Alternation(alternatives, line);
    }

    private Sequence ParseSequence()
    {
        var line = _current.Line;
        var elements = new List<Element>();
        while (_current.Kind is not (Kind.Pipe or Kind.Semi or Kind.RParen or Kind.Eof))
            elements.Add(ParseElement());

        if (elements.Count == 0)
            throw new AntlrGrammarException(line, "an empty alternative is not supported");

        return new Sequence(elements, line);
    }

    private Element ParseElement()
    {
        var atom = ParseAtom();
        if (_current.Kind is not (Kind.Question or Kind.Star or Kind.Plus))
            return new Element(atom, null);

        var suffix = Advance().Text[0];
        if (_current.Kind == Kind.Question)
            throw new AntlrGrammarException(_current.Line, "non-greedy suffixes are not supported");

        return new Element(atom, suffix);
    }

    private Atom ParseAtom()
    {
        var token = Advance();
        switch (token.Kind)
        {
            case Kind.Literal when _current.Kind == Kind.Range:
                Advance();
                var to = Expect(Kind.Literal, "a literal after '..'");
                return new CharRange(token.Text, to.Text, token.Line);
            case Kind.Literal:
                return new Literal(token.Text, token.Line);
            case Kind.CharSet:
                return new CharSet(token.Text, token.Line);
            case Kind.Id:
                return new RuleRef(token.Text, token.Line);
            case Kind.LParen:
                var body = ParseAlternation();
                Expect(Kind.RParen, "')'");
                return new Block(body, token.Line);
            default:
                throw new AntlrGrammarException(token.Line, $"unexpected {Describe(token)}");
        }
    }

    private Token Expect(Kind kind, string what)
    {
        if (_current.Kind != kind)
            throw new AntlrGrammarException(_current.Line, $"expected {what}, found {Describe(_current)}");

        return Advance();
    }

    private void ExpectKeyword(string keyword)
    {
        if (_current.Kind != Kind.Id || _current.Text != keyword)
            throw new AntlrGrammarException(_current.Line, $"expected '{keyword}', found {Describe(_current)}; only lexer grammars are supported");

        Advance();
    }

    private static string Describe(Token token) => token.Kind == Kind.Eof ? "the end of the file" : $"'{token.Text}'";

    private Token Advance()
    {
        var token = _current;
        _current = Next();
        return token;
    }

    private Token Next()
    {
        SkipTrivia();
        var line = _line;
        if (_pos >= _text.Length)
            return new Token(Kind.Eof, string.Empty, line);

        var c = _text[_pos];
        if (char.IsAsciiLetter(c) || c == '_')
        {
            var start = _pos;
            while (_pos < _text.Length && (char.IsAsciiLetterOrDigit(_text[_pos]) || _text[_pos] == '_'))
                _pos++;

            return new Token(Kind.Id, _text[start.._pos], line);
        }

        switch (c)
        {
            case '\'':
                return new Token(Kind.Literal, ReadDelimited('\'', "literal"), line);
            case '[':
                var set = ReadDelimited(']', "set");
                if (set.Contains('\''))
                    throw new AntlrGrammarException(line, "a quote in a set is not supported, because it ends the expression in the Rolex grammar");

                return new Token(Kind.CharSet, $"[{set}]", line);
            case '.' when StartsWith(".."):
                _pos += 2;
                return new Token(Kind.Range, "..", line);
            case '.':
                throw new AntlrGrammarException(line, "the wildcard . is not supported");
            case '~':
                throw new AntlrGrammarException(line, "the set complement ~ is not supported");
            case '-' when StartsWith("->"):
                throw new AntlrGrammarException(line, "lexer commands (->) are not supported");
            case '{':
                throw new AntlrGrammarException(line, "actions are not supported");
        }

        Kind? kind = c switch
        {
            ':' => Kind.Colon,
            ';' => Kind.Semi,
            '|' => Kind.Pipe,
            '(' => Kind.LParen,
            ')' => Kind.RParen,
            '?' => Kind.Question,
            '*' => Kind.Star,
            '+' => Kind.Plus,
            _ => null,
        };
        if (kind is null)
            throw new AntlrGrammarException(line, $"unexpected character '{c}'");

        _pos++;
        return new Token(kind.Value, c.ToString(), line);
    }

    private void SkipTrivia()
    {
        while (_pos < _text.Length)
        {
            if (_text[_pos] == '\n')
            {
                _line++;
                _pos++;
            }
            else if (char.IsWhiteSpace(_text[_pos]))
            {
                _pos++;
            }
            else if (StartsWith("//"))
            {
                while (_pos < _text.Length && _text[_pos] != '\n')
                    _pos++;
            }
            else if (StartsWith("/*"))
            {
                var end = _text.IndexOf("*/", _pos + 2, StringComparison.Ordinal);
                if (end < 0)
                    throw new AntlrGrammarException(_line, "the comment is not closed");

                for (; _pos < end + 2; _pos++)
                {
                    if (_text[_pos] == '\n')
                        _line++;
                }
            }
            else
            {
                return;
            }
        }
    }

    /// <summary>
    /// Reads from the opening delimiter at the current position to <paramref name="close"/>,
    /// skipping escaped characters, and returns the text between the two.
    /// </summary>
    private string ReadDelimited(char close, string what)
    {
        var start = ++_pos;
        while (_pos < _text.Length && _text[_pos] != close && _text[_pos] != '\n')
            _pos += _text[_pos] == '\\' ? 2 : 1;

        if (_pos >= _text.Length || _text[_pos] != close)
            throw new AntlrGrammarException(_line, $"the {what} is not closed on its line");

        return _text[start.._pos++];
    }

    private bool StartsWith(string value) => string.CompareOrdinal(_text, _pos, value, 0, value.Length) == 0;
}
