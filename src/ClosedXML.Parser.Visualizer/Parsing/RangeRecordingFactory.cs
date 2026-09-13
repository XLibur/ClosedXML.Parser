namespace ClosedXML.Parser.Visualizer.Parsing;

/// <summary>
/// Creates the nodes with <see cref="F"/> and records the range of the formula text that each
/// node was parsed from. <see cref="F"/> ignores the ranges.
/// </summary>
public sealed class RangeRecordingFactory : IAstFactory<ScalarValue, AstNode, Ctx>
{
    private readonly F _factory = new();

    /// <summary>
    /// The range of each node. The node records compare by value, so the lookup compares by
    /// reference: two equal nodes, such as two <c>B5</c> references, keep their own ranges.
    /// </summary>
    public Dictionary<AstNode, SymbolRange> Ranges { get; } = new(ReferenceEqualityComparer.Instance);

    public ScalarValue LogicalValue(Ctx context, SymbolRange range, bool value) => _factory.LogicalValue(context, range, value);

    public ScalarValue NumberValue(Ctx context, SymbolRange range, double value) => _factory.NumberValue(context, range, value);

    public ScalarValue TextValue(Ctx context, SymbolRange range, string input) => _factory.TextValue(context, range, input);

    public ScalarValue ErrorValue(Ctx context, SymbolRange range, ReadOnlySpan<char> error) => _factory.ErrorValue(context, range, error);

    public AstNode BlankNode(Ctx context, SymbolRange range) => Record(_factory.BlankNode(context, range), range);

    public AstNode LogicalNode(Ctx context, SymbolRange range, bool value) => Record(_factory.LogicalNode(context, range, value), range);

    public AstNode ErrorNode(Ctx context, SymbolRange range, ReadOnlySpan<char> error) => Record(_factory.ErrorNode(context, range, error), range);

    public AstNode SheetErrorNode(Ctx context, SymbolRange range, int? workbookIndex, string sheet, ReadOnlySpan<char> error) =>
        Record(_factory.SheetErrorNode(context, range, workbookIndex, sheet, error), range);

    public AstNode NumberNode(Ctx context, SymbolRange range, double value) => Record(_factory.NumberNode(context, range, value), range);

    public AstNode TextNode(Ctx context, SymbolRange range, string text) => Record(_factory.TextNode(context, range, text), range);

    public AstNode ArrayNode(Ctx context, SymbolRange range, int rows, int columns, IReadOnlyList<ScalarValue> array) =>
        Record(_factory.ArrayNode(context, range, rows, columns, array), range);

    public AstNode Reference(Ctx context, SymbolRange range, ReferenceArea reference) =>
        Record(_factory.Reference(context, range, reference), range);

    public AstNode SheetReference(Ctx context, SymbolRange range, string sheet, ReferenceArea reference) =>
        Record(_factory.SheetReference(context, range, sheet, reference), range);

    public AstNode BangReference(Ctx context, SymbolRange range, ReferenceArea reference) =>
        Record(_factory.BangReference(context, range, reference), range);

    public AstNode Reference3D(Ctx context, SymbolRange range, string firstSheet, string lastSheet, ReferenceArea reference) =>
        Record(_factory.Reference3D(context, range, firstSheet, lastSheet, reference), range);

    public AstNode ExternalSheetReference(Ctx context, SymbolRange range, int workbookIndex, string sheet, ReferenceArea reference) =>
        Record(_factory.ExternalSheetReference(context, range, workbookIndex, sheet, reference), range);

    public AstNode ExternalReference3D(Ctx context, SymbolRange range, int workbookIndex, string firstSheet, string lastSheet, ReferenceArea reference) =>
        Record(_factory.ExternalReference3D(context, range, workbookIndex, firstSheet, lastSheet, reference), range);

    public AstNode Function(Ctx context, SymbolRange range, ReadOnlySpan<char> name, IReadOnlyList<AstNode> args) =>
        Record(_factory.Function(context, range, name, args), range);

    public AstNode Function(Ctx context, SymbolRange range, string sheet, ReadOnlySpan<char> name, IReadOnlyList<AstNode> args) =>
        Record(_factory.Function(context, range, sheet, name, args), range);

    public AstNode ExternalFunction(Ctx context, SymbolRange range, int workbookIndex, string sheetName, ReadOnlySpan<char> name, IReadOnlyList<AstNode> args) =>
        Record(_factory.ExternalFunction(context, range, workbookIndex, sheetName, name, args), range);

    public AstNode ExternalFunction(Ctx context, SymbolRange range, int workbookIndex, ReadOnlySpan<char> name, IReadOnlyList<AstNode> args) =>
        Record(_factory.ExternalFunction(context, range, workbookIndex, name, args), range);

    public AstNode CellFunction(Ctx context, SymbolRange range, RowCol cell, IReadOnlyList<AstNode> args) =>
        Record(_factory.CellFunction(context, range, cell, args), range);

    public AstNode StructureReference(Ctx context, SymbolRange range, StructuredReferenceArea area, string? firstColumn, string? lastColumn) =>
        Record(_factory.StructureReference(context, range, area, firstColumn, lastColumn), range);

    public AstNode StructureReference(Ctx context, SymbolRange range, string table, StructuredReferenceArea area, string? firstColumn, string? lastColumn) =>
        Record(_factory.StructureReference(context, range, table, area, firstColumn, lastColumn), range);

    public AstNode ExternalStructureReference(Ctx context, SymbolRange range, int workbookIndex, string table, StructuredReferenceArea area, string? firstColumn, string? lastColumn) =>
        Record(_factory.ExternalStructureReference(context, range, workbookIndex, table, area, firstColumn, lastColumn), range);

    public AstNode Name(Ctx context, SymbolRange range, string name) => Record(_factory.Name(context, range, name), range);

    public AstNode SheetName(Ctx context, SymbolRange range, string sheet, string name) =>
        Record(_factory.SheetName(context, range, sheet, name), range);

    public AstNode BangName(Ctx context, SymbolRange range, string name) => Record(_factory.BangName(context, range, name), range);

    public AstNode ExternalName(Ctx context, SymbolRange range, int workbookIndex, string name) =>
        Record(_factory.ExternalName(context, range, workbookIndex, name), range);

    public AstNode ExternalSheetName(Ctx context, SymbolRange range, int workbookIndex, string sheet, string name) =>
        Record(_factory.ExternalSheetName(context, range, workbookIndex, sheet, name), range);

    public AstNode ExternalDynamicDataExchange(Ctx context, SymbolRange range, int workbookIndex, string item) =>
        Record(_factory.ExternalDynamicDataExchange(context, range, workbookIndex, item), range);

    public AstNode DynamicDataExchange(Ctx context, SymbolRange range, string application, string topic, string item) =>
        Record(_factory.DynamicDataExchange(context, range, application, topic, item), range);

    public AstNode BinaryNode(Ctx context, SymbolRange range, BinaryOperation operation, AstNode leftNode, AstNode rightNode) =>
        Record(_factory.BinaryNode(context, range, operation, leftNode, rightNode), range);

    public AstNode Unary(Ctx context, SymbolRange range, UnaryOperation operation, AstNode node) =>
        Record(_factory.Unary(context, range, operation, node), range);

    // The tree has no node for the parentheses, so the inner node keeps its own range.
    public AstNode Nested(Ctx context, SymbolRange range, AstNode node) => _factory.Nested(context, range, node);

    private AstNode Record(AstNode node, SymbolRange range)
    {
        Ranges[node] = range;
        return node;
    }
}
