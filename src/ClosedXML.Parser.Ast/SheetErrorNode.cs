namespace ClosedXML.Parser;

/// <summary>
/// A ref error qualified with a sheet, e.g. <c>Sheet1!#REF!</c>. The area is gone, the sheet is not.
/// </summary>
public record SheetErrorNode(int? WorkbookIndex, string Sheet, string Error) : AstNode
{
    public override string GetDisplayString(ReferenceStyle style)
    {
        var sheet = NameUtils.ShouldQuote(Sheet) ? '\'' + Sheet.Replace("'", "''") + '\'' : Sheet;
        return WorkbookIndex is null ? $"{sheet}!{Error}" : $"[{WorkbookIndex}]{sheet}!{Error}";
    }
}
