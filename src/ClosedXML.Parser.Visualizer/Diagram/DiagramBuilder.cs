using System.Globalization;
using System.Text;

namespace ClosedXML.Parser.Visualizer.Diagram;

/// <summary>
/// Turns a formula tree into a Mermaid flowchart.
/// </summary>
public static class DiagramBuilder
{
    public static DiagramModel Build(AstNode root, IReadOnlyDictionary<AstNode, SymbolRange> ranges, ReferenceStyle style)
    {
        var nodes = new List<DiagramNode>();
        var edges = new List<(string Parent, string Child)>();
        Visit(root);

        var mermaid = new StringBuilder();
        WriteFlowchart(mermaid, nodes, edges);
        var withClassDefs = new StringBuilder(mermaid.ToString());
        WriteClassDefs(withClassDefs);
        return new DiagramModel(nodes, mermaid.ToString(), withClassDefs.ToString());

        void Visit(AstNode node)
        {
            var id = NodeId(nodes.Count);
            var type = node.GetTypeString();
            nodes.Add(new DiagramNode(id, node.GetDisplayString(style), type, NodeFamilies.Of(type), ranges.GetValueOrDefault(node)));
            foreach (var child in node.Children)
            {
                // The child gets the next id. Adding the edge first keeps the edges in the order
                // of the arguments, and Mermaid places the children in the order of the edges.
                edges.Add((id, NodeId(nodes.Count)));
                Visit(child);
            }
        }
    }

    /// <summary>
    /// Escape a text for a quoted Mermaid label. Mermaid reads <c>#name;</c> as an entity, so
    /// <c>#</c> is written as an entity too. A backtick would start a Markdown label.
    /// </summary>
    public static string EscapeLabel(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            switch (c)
            {
                case '#': sb.Append("#35;"); break;
                case '"': sb.Append("#quot;"); break;
                case '<': sb.Append("#lt;"); break;
                case '>': sb.Append("#gt;"); break;
                case '&': sb.Append("#amp;"); break;
                case '`': sb.Append("#96;"); break;
                case '\r' or '\n': sb.Append(' '); break;
                default: sb.Append(c); break;
            }
        }

        return sb.ToString();
    }

    private static string NodeId(int index) => "n" + index.ToString(CultureInfo.InvariantCulture);

    private static void WriteFlowchart(StringBuilder sb, List<DiagramNode> nodes, List<(string Parent, string Child)> edges)
    {
        sb.Append("flowchart TD\n");
        foreach (var node in nodes)
        {
            sb.Append("    ").Append(node.Id)
                .Append("[\"").Append(EscapeLabel(node.Label))
                .Append("<br/>[").Append(EscapeLabel(node.Type)).Append("]\"]:::")
                .Append(NodeFamilies.CssClass(node.Family)).Append('\n');
        }

        foreach (var (parent, child) in edges)
            sb.Append("    ").Append(parent).Append(" --> ").Append(child).Append('\n');
    }

    private static void WriteClassDefs(StringBuilder sb)
    {
        foreach (var family in NodeFamilies.All)
        {
            var (fill, stroke, text) = NodeFamilies.LightColours(family);
            sb.Append("    classDef ").Append(NodeFamilies.CssClass(family))
                .Append(" fill:").Append(fill)
                .Append(",stroke:").Append(stroke)
                .Append(",color:").Append(text).Append('\n');
        }
    }
}
