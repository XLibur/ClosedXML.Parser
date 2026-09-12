namespace ClosedXML.Parser.Visualizer.Diagram;

/// <summary>
/// A node of the diagram.
/// </summary>
/// <param name="Id">The Mermaid id of the node: <c>n0</c>, <c>n1</c>, ... in pre-order.</param>
/// <param name="Label">The display string of the node.</param>
/// <param name="Type">The node type, as <see cref="AstNode.GetTypeString"/> returns it.</param>
/// <param name="Family">The colour group of the node.</param>
/// <param name="Range">The range of the formula text that the node was parsed from.</param>
public sealed record DiagramNode(string Id, string Label, string Type, NodeFamily Family, SymbolRange Range);

/// <summary>
/// The diagram of a formula tree.
/// </summary>
/// <param name="Nodes">The nodes, in pre-order. The first node is the root.</param>
/// <param name="Mermaid">The Mermaid text that the page draws. The page CSS colours the nodes.</param>
/// <param name="MermaidWithClassDefs">The Mermaid text with <c>classDef</c> colours, for Copy Mermaid.</param>
public sealed record DiagramModel(IReadOnlyList<DiagramNode> Nodes, string Mermaid, string MermaidWithClassDefs)
{
    public DiagramNode? FindNode(string? id) => id is null ? null : Nodes.FirstOrDefault(node => node.Id == id);
}
