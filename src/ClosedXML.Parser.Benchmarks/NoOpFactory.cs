namespace ClosedXML.Parser.Benchmarks;

/// <summary>
/// A factory that builds no tree: each method returns the same node. A parse with this factory
/// measures the lexer and the parser without the nodes that a real factory allocates.
/// </summary>
/// <remarks>
/// The node is a reference type, like the nodes of a real factory, so the parser runs the same
/// shared generic code as it does with the AST factory.
/// </remarks>
internal sealed class NoOpFactory : IAstFactory<object, object, object?>
{
    public static readonly NoOpFactory Instance = new();

    private static readonly object s_node = new();

    public object LogicalValue(object? context, SymbolRange range, bool value) => s_node;

    public object NumberValue(object? context, SymbolRange range, double value) => s_node;

    public object TextValue(object? context, SymbolRange range, string text) => s_node;

    public object ErrorValue(object? context, SymbolRange range, ReadOnlySpan<char> error) => s_node;

    public object ArrayNode(object? context, SymbolRange range, int rows, int columns, IReadOnlyList<object> elements) => s_node;

    public object BlankNode(object? context, SymbolRange range) => s_node;

    public object LogicalNode(object? context, SymbolRange range, bool value) => s_node;

    public object ErrorNode(object? context, SymbolRange range, ReadOnlySpan<char> error) => s_node;

    public object SheetErrorNode(object? context, SymbolRange range, int? workbookIndex, string sheet, ReadOnlySpan<char> error) => s_node;

    public object NumberNode(object? context, SymbolRange range, double value) => s_node;

    public object TextNode(object? context, SymbolRange range, string text) => s_node;

    public object Reference(object? context, SymbolRange range, ReferenceArea reference) => s_node;

    public object SheetReference(object? context, SymbolRange range, string sheet, ReferenceArea reference) => s_node;

    public object BangReference(object? context, SymbolRange range, ReferenceArea reference) => s_node;

    public object Reference3D(object? context, SymbolRange range, string firstSheet, string lastSheet, ReferenceArea reference) => s_node;

    public object ExternalSheetReference(object? context, SymbolRange range, int workbookIndex, string sheet, ReferenceArea reference) => s_node;

    public object ExternalReference3D(object? context, SymbolRange range, int workbookIndex, string firstSheet, string lastSheet, ReferenceArea reference) => s_node;

    public object Function(object? context, SymbolRange range, ReadOnlySpan<char> functionName, IReadOnlyList<object> arguments) => s_node;

    public object Function(object? context, SymbolRange range, string sheetName, ReadOnlySpan<char> functionName, IReadOnlyList<object> arguments) => s_node;

    public object ExternalFunction(object? context, SymbolRange range, int workbookIndex, string sheetName, ReadOnlySpan<char> functionName, IReadOnlyList<object> arguments) => s_node;

    public object ExternalFunction(object? context, SymbolRange range, int workbookIndex, ReadOnlySpan<char> functionName, IReadOnlyList<object> arguments) => s_node;

    public object CellFunction(object? context, SymbolRange range, RowCol cell, IReadOnlyList<object> arguments) => s_node;

    public object StructureReference(object? context, SymbolRange range, StructuredReferenceArea area, string? firstColumn, string? lastColumn) => s_node;

    public object StructureReference(object? context, SymbolRange range, string table, StructuredReferenceArea area, string? firstColumn, string? lastColumn) => s_node;

    public object ExternalStructureReference(object? context, SymbolRange range, int workbookIndex, string table, StructuredReferenceArea area, string? firstColumn, string? lastColumn) => s_node;

    public object Name(object? context, SymbolRange range, string name) => s_node;

    public object SheetName(object? context, SymbolRange range, string sheet, string name) => s_node;

    public object BangName(object? context, SymbolRange range, string name) => s_node;

    public object ExternalName(object? context, SymbolRange range, int workbookIndex, string name) => s_node;

    public object ExternalSheetName(object? context, SymbolRange range, int workbookIndex, string sheet, string name) => s_node;

    public object ExternalDynamicDataExchange(object? context, SymbolRange range, int workbookIndex, string item) => s_node;

    public object DynamicDataExchange(object? context, SymbolRange range, string application, string topic, string item) => s_node;

    public object BinaryNode(object? context, SymbolRange range, BinaryOperation operation, object leftNode, object rightNode) => s_node;

    public object Unary(object? context, SymbolRange range, UnaryOperation operation, object node) => s_node;

    public object Nested(object? context, SymbolRange range, object node) => s_node;
}
