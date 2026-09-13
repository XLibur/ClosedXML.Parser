namespace ClosedXML.Parser;

public record ExternalStructureReferenceNode(
    int WorkbookIndex,
    string Table,
    StructuredReferenceArea Area,
    string? FirstColumn,
    string? LastColumn) : AstNode
{
    public override string GetDisplayString(ReferenceStyle style)
    {
        // The bang is the book prefix of a reference that names no sheet, the same form
        // ExternalNameNode writes. Without it the string reads as a book index followed by a table
        // name, which the parser refuses.
        return $"[{WorkbookIndex}]!{Table}{StructuredReferenceWriter.Specifier(Area, FirstColumn, LastColumn)}";
    }
};
