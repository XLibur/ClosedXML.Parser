namespace ClosedXML.Parser;

public record StructureReferenceNode(
    string? Table,
    StructuredReferenceArea Area,
    string? FirstColumn,
    string? LastColumn) : AstNode
{
    public override string GetDisplayString(ReferenceStyle style)
    {
        return $"{Table}{StructuredReferenceWriter.Specifier(Area, FirstColumn, LastColumn)}";
    }
};
