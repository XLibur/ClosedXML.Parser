namespace XLibur.Parser;

public record SheetReferenceNode(string Sheet, ReferenceArea Reference) : AstNode
{
    public override string GetDisplayString(ReferenceStyle style)
    {
        return $"{SheetPrefixWriter.Sheet(Sheet)}{Reference.GetDisplayString(style)}";
    }
}