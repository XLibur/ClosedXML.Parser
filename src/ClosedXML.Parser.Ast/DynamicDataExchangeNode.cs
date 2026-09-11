namespace ClosedXML.Parser;

public record DynamicDataExchangeNode(string Application, string Topic, string Item) : AstNode
{
    public override string GetDisplayString(ReferenceStyle style)
    {
        // NameUtils would quote any link, because of the `|`. Excel writes it bare unless a part needs quotes.
        var link = Application + '|' + Topic;
        var prefix = NameUtils.ShouldQuote(Application) || NameUtils.ShouldQuote(Topic) ? '\'' + link.Replace("'", "''") + '\'' : link;
        return $"{prefix}!'{Item.Replace("'", "''")}'";
    }
}
