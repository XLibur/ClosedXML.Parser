using System.Globalization;
using System.Text;

namespace Antlr2Rolex;

/// <summary>
/// The reference style the generated lexer recognizes.
/// </summary>
public enum LexerStyle
{
    A1,
    R1C1,
}

/// <summary>
/// A Rolex grammar and the alternatives the conversion dropped to produce it.
/// </summary>
public sealed record ConversionResult(string RolexGrammar, IReadOnlyList<string> Warnings);

/// <summary>
/// The ANTLR grammar uses a construct the converter does not support, or is malformed.
/// </summary>
public sealed class AntlrGrammarException : Exception
{
    public AntlrGrammarException(int line, string message)
        : base($"line {line}: {message}")
    {
        Line = line;
    }

    public int Line { get; }
}

/// <summary>
/// Converts an ANTLR lexer grammar into a Rolex grammar: one line per token, each a single
/// regular expression with every fragment it uses inlined.
/// </summary>
/// <remarks>
/// <para>
/// Only the ANTLR syntax that <c>FormulaLexer.g4</c> uses is supported: token and fragment
/// rules, alternatives, groups, the <c>?</c>, <c>*</c> and <c>+</c> suffixes, string literals,
/// <c>'a' .. 'b'</c> ranges and <c>[...]</c> sets.
/// </para>
/// <para>
/// The brackets reproduce the output of the original Antlr2Rolex tool, which was never
/// published, so that the committed grammars regenerate byte for byte. An alternation is
/// <c>((alt1)|(alt2))</c>, a rule reference inlines the alternation of the rule, a group is its
/// alternation in one more pair of brackets, and an element with a suffix is
/// <c>((element)?)</c>. A <c>\uXXXX</c> escape is kept as written and a <c>\u{XXXXX}</c> escape
/// becomes the character itself, except a quote, a line break or another control character,
/// which becomes a <c>\uXXXX</c> escape so the rule stays one single-quoted line.
/// </para>
/// </remarks>
public static class RolexGrammarConverter
{
    // FormulaLexer.g4 keeps the R1C1 variants of the reference tokens in a commented-out section
    // after the A1 ones. The `/*` that comments them out is closed by the `*/` of the next header.
    private const string A1SectionMarker = "Local A1 References";
    private const string R1C1SectionMarker = "Local R1C1 References";

    // Characters that must be escaped with a backslash, outside a set and inside one.
    private const string Metacharacters = @"\^$.|?*+()[]{}-";
    private const string SetMetacharacters = @"\^[]-";

    /// <summary>
    /// Converts an ANTLR lexer grammar into a Rolex grammar for the given reference style.
    /// </summary>
    /// <exception cref="AntlrGrammarException">The grammar can't be converted.</exception>
    public static ConversionResult Convert(string antlrGrammar, LexerStyle style)
    {
        var a1OnlyRules = new HashSet<string>(StringComparer.Ordinal);
        if (style == LexerStyle.R1C1)
            antlrGrammar = UseR1C1Section(antlrGrammar, a1OnlyRules);

        var rules = new AntlrGrammarParser(antlrGrammar).ParseGrammar();
        a1OnlyRules.ExceptWith(rules.Select(rule => rule.Name));

        var warnings = new List<string>();
        foreach (var rule in rules)
            DropA1OnlyAlternatives(rule, rule.Body, a1OnlyRules, warnings);

        var emitter = new Emitter(rules);
        var output = new StringBuilder();
        foreach (var rule in rules)
        {
            // Fragments are emitted as well, to report their errors even when no token uses them.
            var expression = emitter.Emit(rule);
            if (!rule.IsFragment)
                output.Append(rule.Name).Append(" = '").Append(expression).Append("'\n");
        }

        return new ConversionResult(output.ToString(), warnings);
    }

    /// <summary>
    /// Comments out the A1 section and uncomments the R1C1 section, the same edit the README
    /// describes doing by hand. The removed text is blanked rather than deleted, so the lines
    /// in errors still match the file.
    /// </summary>
    /// <param name="grammar">The grammar as written, with the A1 section in use.</param>
    /// <param name="a1Rules">Receives the names of the rules defined in the A1 section.</param>
    private static string UseR1C1Section(string grammar, HashSet<string> a1Rules)
    {
        var (_, a1HeaderEnd) = FindSectionHeader(grammar, A1SectionMarker);
        var (r1c1HeaderStart, r1c1HeaderEnd) = FindSectionHeader(grammar, R1C1SectionMarker);
        var commentStart = r1c1HeaderEnd;
        while (commentStart < grammar.Length && char.IsWhiteSpace(grammar[commentStart]))
            commentStart++;

        if (a1HeaderEnd > r1c1HeaderStart || string.CompareOrdinal(grammar, commentStart, "/*", 0, 2) != 0)
        {
            throw new AntlrGrammarException(LineOf(grammar, r1c1HeaderEnd),
                $"the {R1C1SectionMarker} section must follow the A1 section and be commented out with /*");
        }

        var a1Section = grammar[a1HeaderEnd..r1c1HeaderStart];
        foreach (var rule in new AntlrGrammarParser(a1Section, LineOf(grammar, a1HeaderEnd)).ParseRules())
            a1Rules.Add(rule.Name);

        var chars = grammar.ToCharArray();
        for (var i = a1HeaderEnd; i < r1c1HeaderStart; ++i)
        {
            if (chars[i] is not ('\n' or '\r'))
                chars[i] = ' ';
        }

        chars[commentStart] = ' ';
        chars[commentStart + 1] = ' ';
        return new string(chars);
    }

    /// <summary>
    /// Finds the <c>/* ... marker ... */</c> comment that heads a section.
    /// </summary>
    private static (int Start, int End) FindSectionHeader(string grammar, string marker)
    {
        var index = grammar.IndexOf(marker, StringComparison.Ordinal);
        var start = index < 0 ? -1 : grammar.LastIndexOf("/*", index, StringComparison.Ordinal);
        var end = index < 0 ? -1 : grammar.IndexOf("*/", index, StringComparison.Ordinal);
        if (start < 0 || end < 0)
            throw new AntlrGrammarException(1, $"the R1C1 style needs a /* {marker} */ section header");

        return (start, end + 2);
    }

    private static int LineOf(string text, int index) => text.AsSpan(0, index).Count('\n') + 1;

    /// <summary>
    /// Drops every alternative that refers to a rule defined only in the A1 section. Such a rule
    /// doesn't exist in the R1C1 lexer, e.g. the column of a sheet range like <c>JAN:Sheet</c>.
    /// </summary>
    private static void DropA1OnlyAlternatives(Rule rule, Alternation alternation, IReadOnlySet<string> a1OnlyRules, List<string> warnings)
    {
        if (a1OnlyRules.Count == 0)
            return;

        for (var i = 0; i < alternation.Alternatives.Count;)
        {
            var sequence = alternation.Alternatives[i];
            foreach (var block in sequence.Elements.Select(element => element.Atom).OfType<Block>())
                DropA1OnlyAlternatives(rule, block.Body, a1OnlyRules, warnings);

            var reference = sequence.Elements
                .Select(element => element.Atom)
                .OfType<RuleRef>()
                .FirstOrDefault(atom => a1OnlyRules.Contains(atom.Name));
            if (reference is null)
            {
                ++i;
                continue;
            }

            warnings.Add($"{rule.Name}: dropped the alternative on line {sequence.Line}, because {reference.Name} is defined only in the A1 section");
            alternation.Alternatives.RemoveAt(i);
        }

        if (alternation.Alternatives.Count == 0)
            throw new AntlrGrammarException(alternation.Line, $"{rule.Name}: every alternative refers to a rule defined only in the A1 section");
    }

    private sealed class Emitter
    {
        private readonly Dictionary<string, Rule> _rules;
        private readonly Dictionary<string, string> _expressions = new(StringComparer.Ordinal);
        private readonly HashSet<string> _inProgress = new(StringComparer.Ordinal);

        public Emitter(IEnumerable<Rule> rules)
        {
            _rules = rules.ToDictionary(rule => rule.Name, StringComparer.Ordinal);
        }

        public string Emit(Rule rule)
        {
            if (_expressions.TryGetValue(rule.Name, out var expression))
                return expression;

            if (!_inProgress.Add(rule.Name))
                throw new AntlrGrammarException(rule.Line, $"{rule.Name} refers to itself, which a regular expression can't express");

            expression = Emit(rule.Body);
            _inProgress.Remove(rule.Name);
            _expressions.Add(rule.Name, expression);
            return expression;
        }

        private string Emit(Alternation alternation) =>
            "(" + string.Join("|", alternation.Alternatives.Select(sequence => "(" + Emit(sequence) + ")")) + ")";

        private string Emit(Sequence sequence) => string.Concat(sequence.Elements.Select(element => Emit(element)));

        private string Emit(Element element) =>
            element.Suffix is { } suffix ? "((" + Emit(element.Atom) + ")" + suffix + ")" : Emit(element.Atom);

        private string Emit(Atom atom) => atom switch
        {
            Literal literal => string.Concat(Decode(literal.Raw, literal.Line).Select(c => c.Verbatim ?? Escape(c.CodePoint, Metacharacters))),
            CharRange range => $"[{RangeBound(range.From, range.Line)}-{RangeBound(range.To, range.Line)}]",
            CharSet set => set.Text,
            RuleRef reference => Emit(Resolve(reference)),
            Block block => "(" + Emit(block.Body) + ")",
            _ => throw new InvalidOperationException($"Unknown atom {atom.GetType().Name}."),
        };

        private Rule Resolve(RuleRef reference)
        {
            if (!_rules.TryGetValue(reference.Name, out var rule))
                throw new AntlrGrammarException(reference.Line, $"{reference.Name} is not defined");

            return rule;
        }
    }

    private static string RangeBound(string raw, int line)
    {
        var chars = Decode(raw, line);
        if (chars.Count != 1)
            throw new AntlrGrammarException(line, $"a range bound must be one character, not '{raw}'");

        return chars[0].Verbatim ?? Escape(chars[0].CodePoint, SetMetacharacters);
    }

    private static string Escape(int codePoint, string metacharacters)
    {
        // A quote would end the single-quoted expression and a line break would end the rule,
        // so write those, and the other control characters, as a four digit Unicode escape.
        if (codePoint is '\'' or 0x2028 or 0x2029 || (codePoint <= 0xFFFF && char.IsControl((char)codePoint)))
            return "\\u" + codePoint.ToString("X4", CultureInfo.InvariantCulture);

        var text = char.ConvertFromUtf32(codePoint);
        return text.Length == 1 && metacharacters.Contains(text[0]) ? "\\" + text : text;
    }

    /// <summary>
    /// One code point of a literal, with the text to write it as when that isn't the character.
    /// </summary>
    private readonly record struct LiteralChar(int CodePoint, string? Verbatim);

    /// <summary>
    /// Decodes the escapes of a literal. Only the escapes the grammar uses are supported: a
    /// backslash, a four digit Unicode escape and a code point escape.
    /// </summary>
    private static List<LiteralChar> Decode(string raw, int line)
    {
        if (raw.Length == 0)
            throw new AntlrGrammarException(line, "an empty literal is not supported");

        var chars = new List<LiteralChar>();
        for (var i = 0; i < raw.Length;)
        {
            if (raw[i] != '\\')
            {
                chars.Add(new LiteralChar(char.ConvertToUtf32(raw, i), null));
                i += char.IsSurrogatePair(raw, i) ? 2 : 1;
                continue;
            }

            var escape = i + 1 < raw.Length ? raw[i + 1] : '\0';
            if (escape == '\\')
            {
                chars.Add(new LiteralChar('\\', null));
                i += 2;
            }
            else if (escape == 'u' && i + 2 < raw.Length && raw[i + 2] == '{')
            {
                var close = raw.IndexOf('}', i + 3);
                if (close < 0 || !TryParseHex(raw.AsSpan(i + 3, close - i - 3), out var codePoint) ||
                    codePoint > 0x10FFFF || codePoint is >= 0xD800 and <= 0xDFFF)
                {
                    throw new AntlrGrammarException(line, $"'{raw}' has an invalid code point escape");
                }

                chars.Add(new LiteralChar(codePoint, null));
                i = close + 1;
            }
            else if (escape == 'u')
            {
                if (i + 6 > raw.Length || !TryParseHex(raw.AsSpan(i + 2, 4), out var codePoint))
                    throw new AntlrGrammarException(line, $"'{raw}' has an invalid Unicode escape");

                chars.Add(new LiteralChar(codePoint, raw.Substring(i, 6)));
                i += 6;
            }
            else if (escape == '\'')
            {
                throw new AntlrGrammarException(line, "a quote in a literal is not supported, because it ends the expression in the Rolex grammar; write it as a Unicode escape");
            }
            else
            {
                throw new AntlrGrammarException(line, $"'{raw}' has an unsupported escape; only a backslash and Unicode escapes are supported");
            }
        }

        return chars;
    }

    private static bool TryParseHex(ReadOnlySpan<char> digits, out int value) =>
        int.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value);
}
