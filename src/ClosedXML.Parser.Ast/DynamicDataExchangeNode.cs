namespace XLibur.Parser;

public record DynamicDataExchangeNode(string Application, string Topic, string Item) : AstNode
{
    public override string GetDisplayString(ReferenceStyle style)
    {
        // NameUtils would quote any link, because of the `|`. Excel writes it bare unless a part needs quotes.
        // Neither part is a sheet name, so only its characters count: R5|tik!'item' reads back as the
        // same link, and the formula rewriter writes it bare too.
        var link = Application + '|' + Topic;
        var prefix = NameUtils.ShouldQuoteForCharacters(Application) || NameUtils.ShouldQuoteForCharacters(Topic) ? '\'' + link.Replace("'", "''") + '\'' : link;
        return $"{prefix}!'{Item.Replace("'", "''")}'";
    }
}
