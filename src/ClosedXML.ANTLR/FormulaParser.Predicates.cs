/// <summary>
/// Semantic predicates of <c>FormulaParser.g4</c>.
/// </summary>
public partial class FormulaParser
{
    /// <summary>
    /// Is a <c>SINGLE_SHEET_PREFIX</c> token the application and the topic of a DDE link (e.g. <c>Sdemo123|tik!</c>)?
    /// It must have no workbook index and a non-empty part on each side of the first <c>|</c>. The recursive
    /// descent parser checks the same in <c>TokenParser.TrySplitDdeLink</c>, which this project can't reference.
    /// </summary>
    private static bool IsDdeLinkPrefix(string token)
    {
        // The token ends with '!' and optional whitespace. A quoted name can contain '!', but only whitespace follows the last one.
        var name = token.Substring(0, token.LastIndexOf('!'));
        if (name.StartsWith("'"))
            name = name.Substring(1, name.Length - 2).Replace("''", "'");

        // A name can't start with '[', so it's a workbook index.
        if (name.StartsWith("["))
            return false;

        var separatorIndex = name.IndexOf('|');
        return separatorIndex > 0 && separatorIndex < name.Length - 1;
    }

    /// <summary>
    /// Is the name of a <c>BANG_NAME</c> token (e.g. <c>!SomeName</c>) a valid name? A name can't be <c>TRUE</c> or
    /// <c>FALSE</c>, but the lexer can't exclude them from the name after the bang. The recursive descent parser
    /// checks the same when it reads the token.
    /// </summary>
    private static bool IsBangName(string token)
    {
        var name = token.Substring(1);
        return !name.Equals("TRUE", System.StringComparison.OrdinalIgnoreCase) &&
               !name.Equals("FALSE", System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Is there a space before the <c>@</c> of an <c>INTERSECT</c> token (e.g. <c> @</c>)? The lexer puts the
    /// whitespace before <c>@</c> into the token, so after a reference, the space is the intersection operator. A line
    /// break alone is not, the same as for a <c>SPACE</c> token. The recursive descent parser checks the same in
    /// <c>IsSpaceBeforeAt</c>.
    /// </summary>
    private static bool IsSpaceBeforeAt(string token)
    {
        var atIndex = token.IndexOf('@');
        return atIndex >= 0 && token.Substring(0, atIndex).Contains(" ");
    }
}
