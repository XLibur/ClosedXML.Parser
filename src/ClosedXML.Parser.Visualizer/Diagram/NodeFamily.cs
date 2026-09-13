using System.Collections.Frozen;

namespace ClosedXML.Parser.Visualizer.Diagram;

/// <summary>
/// A group of node types that the diagram shows in one colour.
/// </summary>
public enum NodeFamily
{
    Function,
    Reference,
    StructuredReference,
    Name,
    Value,
    Error,
    Operator,
}

public static class NodeFamilies
{
    private static readonly FrozenDictionary<string, NodeFamily> FamilyOfType = new Dictionary<string, NodeFamily>
    {
        ["Function"] = NodeFamily.Function,
        ["ExternalFunction"] = NodeFamily.Function,
        ["CellFunction"] = NodeFamily.Function,

        ["Reference"] = NodeFamily.Reference,
        ["SheetReference"] = NodeFamily.Reference,
        ["BangReference"] = NodeFamily.Reference,
        ["Reference3D"] = NodeFamily.Reference,
        ["ExternalSheetReference"] = NodeFamily.Reference,
        ["ExternalReference3D"] = NodeFamily.Reference,

        ["StructureReference"] = NodeFamily.StructuredReference,
        ["ExternalStructureReference"] = NodeFamily.StructuredReference,

        ["Name"] = NodeFamily.Name,
        ["SheetName"] = NodeFamily.Name,
        ["BangName"] = NodeFamily.Name,
        ["ExternalName"] = NodeFamily.Name,
        ["ExternalSheetName"] = NodeFamily.Name,
        ["DynamicDataExchange"] = NodeFamily.Name,
        ["ExternalDynamicDataExchange"] = NodeFamily.Name,

        ["Number"] = NodeFamily.Value,
        ["Text"] = NodeFamily.Value,
        ["Logical"] = NodeFamily.Value,
        ["Blank"] = NodeFamily.Value,
        ["Array"] = NodeFamily.Value,

        ["Error"] = NodeFamily.Error,
        ["SheetError"] = NodeFamily.Error,

        ["Binary"] = NodeFamily.Operator,
        ["Unary"] = NodeFamily.Operator,
    }.ToFrozenDictionary();

    public static IReadOnlyList<NodeFamily> All { get; } = Enum.GetValues<NodeFamily>();

    /// <summary>
    /// The node types that have a family, as <see cref="AstNode.GetTypeString"/> returns them.
    /// </summary>
    public static IEnumerable<string> KnownTypes => FamilyOfType.Keys;

    /// <summary>
    /// Get the family of a node type. A type that has no family yet shows as a value.
    /// </summary>
    public static NodeFamily Of(string nodeType) => FamilyOfType.GetValueOrDefault(nodeType, NodeFamily.Value);

    /// <summary>
    /// The class of a family in the diagram and in the legend. The prefix keeps it apart from the
    /// classes of Mermaid, such as <c>label</c> and <c>error</c>.
    /// </summary>
    public static string CssClass(NodeFamily family) => "fam" + family;

    public static string DisplayName(NodeFamily family) => family switch
    {
        NodeFamily.StructuredReference => "Structured reference",
        _ => family.ToString(),
    };

    /// <summary>
    /// The light colours of a family, for the <c>classDef</c> lines of a copied diagram. The page
    /// sets its own colours in CSS, for light and dark mode. Keep the two the same.
    /// </summary>
    public static (string Fill, string Stroke, string Text) LightColours(NodeFamily family) => family switch
    {
        NodeFamily.Function => ("#dbeafe", "#2563eb", "#1e3a8a"),
        NodeFamily.Reference => ("#dcfce7", "#16a34a", "#14532d"),
        NodeFamily.StructuredReference => ("#ccfbf1", "#0d9488", "#134e4a"),
        NodeFamily.Name => ("#ede9fe", "#7c3aed", "#4c1d95"),
        NodeFamily.Value => ("#fef3c7", "#d97706", "#78350f"),
        NodeFamily.Error => ("#fee2e2", "#dc2626", "#7f1d1d"),
        NodeFamily.Operator => ("#f1f5f9", "#475569", "#0f172a"),
        _ => throw new ArgumentOutOfRangeException(nameof(family), family, null),
    };
}
