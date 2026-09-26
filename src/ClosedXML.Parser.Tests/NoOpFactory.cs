namespace XLibur.Parser.Tests;

/// <summary>
/// A factory that builds no tree: each method returns the same node. A parse with this factory
/// measures the lexer and the parser without the nodes that a real factory allocates.
/// </summary>
/// <remarks>
/// The node is a reference type, like the nodes of a real factory, so the parser runs the same
/// shared generic code as it does with the AST factory. A test that needs to see what the parser
/// passes a function overrides the members that are virtual.
/// </remarks>
internal class NoOpFactory : IAstFactory<object, object, object?>
{
    public static readonly NoOpFactory Instance = new();

    public static readonly object Node = new();

    public object LogicalValue(object? context, SymbolRange range, bool value) => Node;

    public object NumberValue(object? context, SymbolRange range, double value) => Node;

    public object TextValue(object? context, SymbolRange range, string text) => Node;

    public object ErrorValue(object? context, SymbolRange range, ReadOnlySpan<char> error) => Node;

    public object ArrayNode(object? context, SymbolRange range, int rows, int columns, IReadOnlyList<object> elements) => Node;

    public object BlankNode(object? context, SymbolRange range) => Node;

    public object LogicalNode(object? context, SymbolRange range, bool value) => Node;

    public object ErrorNode(object? context, SymbolRange range, ReadOnlySpan<char> error) => Node;

    public object SheetErrorNode(object? context, SymbolRange range, int? workbookIndex, string sheet, ReadOnlySpan<char> error) => Node;

    public virtual object NumberNode(object? context, SymbolRange range, double value) => Node;

    public object TextNode(object? context, SymbolRange range, string text) => Node;

    public object Reference(object? context, SymbolRange range, ReferenceArea reference) => Node;

    public object SheetReference(object? context, SymbolRange range, string sheet, ReferenceArea reference) => Node;

    public object BangReference(object? context, SymbolRange range, ReferenceArea reference) => Node;

    public object Reference3D(object? context, SymbolRange range, string firstSheet, string lastSheet, ReferenceArea reference) => Node;

    public object ExternalSheetReference(object? context, SymbolRange range, int workbookIndex, string sheet, ReferenceArea reference) => Node;

    public object ExternalReference3D(object? context, SymbolRange range, int workbookIndex, string firstSheet, string lastSheet, ReferenceArea reference) => Node;

    public virtual object Function(object? context, SymbolRange range, ReadOnlySpan<char> functionName, IReadOnlyList<object> arguments) => Node;

    public object Function(object? context, SymbolRange range, string sheetName, ReadOnlySpan<char> functionName, IReadOnlyList<object> arguments) => Node;

    public object ExternalFunction(object? context, SymbolRange range, int workbookIndex, string sheetName, ReadOnlySpan<char> functionName, IReadOnlyList<object> arguments) => Node;

    public object ExternalFunction(object? context, SymbolRange range, int workbookIndex, ReadOnlySpan<char> functionName, IReadOnlyList<object> arguments) => Node;

    public object CellFunction(object? context, SymbolRange range, RowCol cell, IReadOnlyList<object> arguments) => Node;

    public object StructureReference(object? context, SymbolRange range, StructuredReferenceArea area, string? firstColumn, string? lastColumn) => Node;

    public object StructureReference(object? context, SymbolRange range, string table, StructuredReferenceArea area, string? firstColumn, string? lastColumn) => Node;

    public object ExternalStructureReference(object? context, SymbolRange range, int workbookIndex, string table, StructuredReferenceArea area, string? firstColumn, string? lastColumn) => Node;

    public object Name(object? context, SymbolRange range, string name) => Node;

    public object SheetName(object? context, SymbolRange range, string sheet, string name) => Node;

    public object BangName(object? context, SymbolRange range, string name) => Node;

    public object ExternalName(object? context, SymbolRange range, int workbookIndex, string name) => Node;

    public object ExternalSheetName(object? context, SymbolRange range, int workbookIndex, string sheet, string name) => Node;

    public object ExternalDynamicDataExchange(object? context, SymbolRange range, int workbookIndex, string item) => Node;

    public object DynamicDataExchange(object? context, SymbolRange range, string application, string topic, string item) => Node;

    public object BinaryNode(object? context, SymbolRange range, BinaryOperation operation, object leftNode, object rightNode) => Node;

    public object Unary(object? context, SymbolRange range, UnaryOperation operation, object node) => Node;

    public object Nested(object? context, SymbolRange range, object node) => Node;
}
