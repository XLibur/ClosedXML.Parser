namespace ClosedXML.Parser;

/// <summary>
/// A ref error qualified with a sheet, e.g. <c>Sheet1!#REF!</c>. The area is gone, the sheet is not.
/// </summary>
public record SheetErrorNode(int? WorkbookIndex, string Sheet, string Error) : AstNode
{
    public override string GetDisplayString(ReferenceStyle style)
    {
        // A quote wraps the whole prefix, the book index included: '[2]Jane''s'!#REF!
        var book = WorkbookIndex is null ? string.Empty : $"[{WorkbookIndex}]";
        return NameUtils.ShouldQuote(Sheet)
            ? $"'{book}{Sheet.Replace("'", "''")}'!{Error}"
            : $"{book}{Sheet}!{Error}";
    }
}
