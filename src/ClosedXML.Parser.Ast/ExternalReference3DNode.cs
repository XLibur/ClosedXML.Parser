namespace ClosedXML.Parser;

public record ExternalReference3DNode(int WorkbookIndex, string FirstSheet, string LastSheet, ReferenceArea Reference) : AstNode
{
    public override string GetDisplayString(ReferenceStyle style)
    {
        return $"{SheetPrefixWriter.Range(FirstSheet, LastSheet, WorkbookIndex)}{Reference.GetDisplayString(style)}";
    }
}