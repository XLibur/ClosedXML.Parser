using System.Globalization;
using System.Text.RegularExpressions;

namespace ClosedXML.Parser.Visualizer.Parsing;

/// <summary>
/// Reads the position of an error from the message of a <see cref="ParsingException"/>. The
/// exception has no position property, but most messages contain one.
/// </summary>
public static partial class ParseErrorPosition
{
    private const string RestPrefix = "but the rest `";
    private const string RestSuffix = "` wasn't.";

    /// <summary>
    /// Get the position of the error in <paramref name="formula"/>, or null when the message has
    /// no position.
    /// </summary>
    public static int? Find(string formula, string message)
    {
        return FindUnparsedRest(formula, message) ?? FindPositionText(formula, message);
    }

    /// <summary>
    /// "The formula `f` wasn't parsed correctly. The expression `p` was parsed, but the rest `r` wasn't."
    /// The error is at the start of the rest.
    /// </summary>
    private static int? FindUnparsedRest(string formula, string message)
    {
        if (!message.EndsWith(RestSuffix, StringComparison.Ordinal))
            return null;

        var body = message.AsSpan(0, message.Length - RestSuffix.Length);
        for (var start = 0; start <= formula.Length; ++start)
        {
            var rest = formula.AsSpan(start);
            if (body.EndsWith(rest, StringComparison.Ordinal) &&
                body[..^rest.Length].EndsWith(RestPrefix, StringComparison.Ordinal))
                return start;
        }

        return null;
    }

    /// <summary>
    /// "... at position 5", "from position 5", "at index 5", "Error at char 5 of ..." or "... at 5."
    /// </summary>
    private static int? FindPositionText(string formula, string message)
    {
        // Some messages quote the formula. Remove it, so a number in the formula is not read as the position.
        var text = formula.Length > 0 ? message.Replace(formula, string.Empty, StringComparison.Ordinal) : message;
        var match = PositionPattern().Match(text);
        if (!match.Success)
            return null;

        var position = int.Parse(match.Groups[1].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture);
        return Math.Min(position, formula.Length);
    }

    [GeneratedRegex(@"\b(?:position|index|char|at) (\d{1,9})\b", RegexOptions.CultureInvariant)]
    private static partial Regex PositionPattern();
}
