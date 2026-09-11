namespace ClosedXML.Parser;

public record ExternalDynamicDataExchangeNode(int WorkbookIndex, string Item) : AstNode
{
    public override string GetDisplayString(ReferenceStyle style)
    {
        return $"[{WorkbookIndex}]!'{Item.Replace("'", "''")}'";
    }
}
