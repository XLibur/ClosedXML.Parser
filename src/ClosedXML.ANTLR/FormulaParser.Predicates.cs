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
}
