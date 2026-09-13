using System;
using System.Collections.Generic;
using System.Text;

namespace ClosedXML.Parser;

/// <summary>
/// Says how a formula changes when the workbook around it changes, e.g. when a sheet is renamed or deleted,
/// or rows are inserted. Inherit it, override the <c>Modify*</c> methods for the parts of a formula that
/// change, and pass it to <see cref="FormulaConverter.ModifyA1"/> or <see cref="FormulaConverter.ModifyR1C1"/>.
/// The rest of the formula is written back as it was. This class itself changes nothing.
/// </summary>
public class FormulaModifier
{
    /// <summary>
    /// Writes the formula again from what the parser reads, and asks the <see cref="ModContext.Modifier"/> of the
    /// context about each part that can change. It holds no state, so one serves every modifier.
    /// </summary>
    internal static readonly IAstFactory<TransformedSymbol, TransformedSymbol, ModContext> Factory = new Adapter();

    /// <summary>
    /// Modify the name of a sheet, e.g. rename it. It gets a sheet of this workbook only: a sheet behind a book
    /// prefix belongs to another workbook and is left as it is.
    /// </summary>
    /// <param name="ctx">Where the formula is.</param>
    /// <param name="sheetName">Original sheet name.</param>
    /// <returns>New sheet name, or <c>null</c> if the sheet has been deleted and the part should be <c>#REF!</c>.</returns>
    protected virtual string? ModifySheet(ModContext ctx, string sheetName)
    {
        return sheetName;
    }

    /// <summary>
    /// Modify the name of a table of this workbook.
    /// </summary>
    /// <param name="ctx">Where the formula is.</param>
    /// <param name="table">Original name of a table.</param>
    /// <returns>Modified name of a table, or <c>null</c> if the part should be <c>#REF!</c>.</returns>
    protected virtual string? ModifyTable(ModContext ctx, string table)
    {
        return table;
    }

    /// <summary>
    /// Modify the name of a function. Doesn't get a function of a sheet or of another workbook.
    /// </summary>
    /// <param name="ctx">Where the formula is.</param>
    /// <param name="functionName">Original name of function.</param>
    /// <returns>New name of a function.</returns>
    protected virtual ReadOnlySpan<char> ModifyFunction(ModContext ctx, ReadOnlySpan<char> functionName)
    {
        return functionName;
    }

    /// <summary>
    /// Modify a reference, e.g. shift it when rows are inserted. It gets every reference of the formula, in the
    /// reference style of the formula (see <see cref="ModContext.IsA1"/>).
    /// </summary>
    /// <param name="ctx">Where the formula is.</param>
    /// <param name="reference">Area reference.</param>
    /// <returns>Modified reference, or <c>null</c> if the part should be <c>#REF!</c>.</returns>
    protected virtual ReferenceArea? ModifyRef(ModContext ctx, ReferenceArea reference)
    {
        return reference;
    }

    /// <summary>
    /// Modify the called cell of a cell function (e.g. the <c>R7C3</c> of <c>R7C3(TRUE)</c>).
    /// </summary>
    /// <param name="ctx">Where the formula is.</param>
    /// <param name="cell">Original cell containing function.</param>
    /// <returns>Modified cell, or <c>null</c> if the part should be <c>#REF!</c>.</returns>
    protected virtual RowCol? ModifyCellFunction(ModContext ctx, RowCol cell)
    {
        return cell;
    }

    /// <summary>
    /// Writes each part of the formula again with a <see cref="CopyVisitor"/>, after the modifier of the context
    /// has changed what it changes.
    /// </summary>
    private sealed class Adapter : IAstFactory<TransformedSymbol, TransformedSymbol, ModContext>
    {
        private const string REF_ERROR = "#REF!";
        private const string BANG_REF_ERROR = "!#REF!";
        private static readonly CopyVisitor s_copyVisitor = new();

        public TransformedSymbol LogicalValue(ModContext ctx, SymbolRange range, bool value)
        {
            return s_copyVisitor.LogicalValue(ctx, range, value);
        }

        public TransformedSymbol NumberValue(ModContext ctx, SymbolRange range, double value)
        {
            return s_copyVisitor.NumberValue(ctx, range, value);
        }

        public TransformedSymbol TextValue(ModContext ctx, SymbolRange range, string text)
        {
            return s_copyVisitor.TextValue(ctx, range, text);
        }

        public TransformedSymbol ErrorValue(ModContext ctx, SymbolRange range, ReadOnlySpan<char> error)
        {
            return s_copyVisitor.ErrorValue(ctx, range, error);
        }

        public TransformedSymbol ArrayNode(ModContext ctx, SymbolRange range, int rows, int columns, IReadOnlyList<TransformedSymbol> elements)
        {
            return s_copyVisitor.ArrayNode(ctx, range, rows, columns, elements);
        }

        public TransformedSymbol BlankNode(ModContext ctx, SymbolRange range)
        {
            return s_copyVisitor.BlankNode(ctx, range);
        }

        public TransformedSymbol LogicalNode(ModContext ctx, SymbolRange range, bool value)
        {
            return s_copyVisitor.LogicalNode(ctx, range, value);
        }

        public TransformedSymbol ErrorNode(ModContext ctx, SymbolRange range, ReadOnlySpan<char> error)
        {
            if (range.Length == error.Length)
                return TransformedSymbol.CopyOriginal(ctx.Formula, range);

            // Deal with `Sheet!#REF!`, `!#REF!`, `#REF!A1` and `#REF!#REF!`
            var symbol = ctx.Formula.AsSpan().Slice(range.Start, range.Length);
            var sheetIsRefError = symbol.StartsWith(REF_ERROR.AsSpan(), StringComparison.OrdinalIgnoreCase);

            if (sheetIsRefError)
            {
                // #REF!A1 is invalid formula that can't be parsed by Excel. It is displayed, but
                // likely only because it is a serialization of internal structures. When sheet is
                // deleted, the result is #REF!, which is how it is actually saved in the file.
                return TransformedSymbol.ToText(ctx.Formula, range, REF_ERROR);
            }

            // A bang reference to a deleted cell has no sheet to modify.
            if (symbol[0] == '!')
                return TransformedSymbol.CopyOriginal(ctx.Formula, range);

            // Sheet!#REF! is a valid formula per grammar and Excel, though it displeases me. The symbol
            // is a sheet prefix token followed by the error token, so read the sheet the way the parser does.
            var errorText = symbol.Slice(symbol.Length - error.Length);
            var sheetPrefixToken = new Token(Token.SINGLE_SHEET_PREFIX, range.Start, range.Length - error.Length);
            var sheetPrefix = SheetPrefix.ReadSingle(ctx.Formula.AsSpan(), sheetPrefixToken);
            var nodeText = new StringBuilder();
            if (sheetPrefix.BookIndex is null)
            {
                var modifiedSheet = ctx.Modifier.ModifySheet(ctx, sheetPrefix.FirstSheet!);
                nodeText.AppendPrefix(modifiedSheet is null ? SheetPrefix.Deleted : SheetPrefix.Sheet(modifiedSheet));
            }
            else
            {
                nodeText.AppendPrefix(sheetPrefix); // A sheet of another workbook isn't modified.
            }

            return TransformedSymbol.ToText(ctx.Formula, range, nodeText.Append(errorText).ToString());
        }

        public TransformedSymbol NumberNode(ModContext ctx, SymbolRange range, double value)
        {
            return s_copyVisitor.NumberNode(ctx, range, value);
        }

        public TransformedSymbol TextNode(ModContext ctx, SymbolRange range, string text)
        {
            return s_copyVisitor.TextNode(ctx, range, text);
        }

        public TransformedSymbol Reference(ModContext ctx, SymbolRange range, ReferenceArea reference)
        {
            var modifiedReference = ctx.Modifier.ModifyRef(ctx, reference);
            if (modifiedReference is null)
                return TransformedSymbol.ToText(ctx.Formula, range, REF_ERROR);

            return s_copyVisitor.Reference(ctx, range, modifiedReference.Value);
        }

        public TransformedSymbol SheetReference(ModContext ctx, SymbolRange range, string sheet, ReferenceArea reference)
        {
            var modifiedSheet = ctx.Modifier.ModifySheet(ctx, sheet);
            var modifiedReference = ctx.Modifier.ModifyRef(ctx, reference);
            if (modifiedSheet is null || modifiedReference is null)
                return TransformedSymbol.ToText(ctx.Formula, range, REF_ERROR);

            return s_copyVisitor.SheetReference(ctx, range, modifiedSheet, modifiedReference.Value);
        }

        public TransformedSymbol BangReference(ModContext ctx, SymbolRange range, ReferenceArea reference)
        {
            var modifiedReference = ctx.Modifier.ModifyRef(ctx, reference);
            if (modifiedReference is null)
                return TransformedSymbol.ToText(ctx.Formula, range, BANG_REF_ERROR);

            return s_copyVisitor.BangReference(ctx, range, modifiedReference.Value);
        }

        public TransformedSymbol Reference3D(ModContext ctx, SymbolRange range, string firstSheet, string lastSheet, ReferenceArea reference)
        {
            var modifiedFirstSheet = ctx.Modifier.ModifySheet(ctx, firstSheet);
            var modifiedLastSheet = ctx.Modifier.ModifySheet(ctx, lastSheet);
            var modifiedReference = ctx.Modifier.ModifyRef(ctx, reference);
            if (modifiedFirstSheet is null || modifiedLastSheet is null || modifiedReference is null)
                return TransformedSymbol.ToText(ctx.Formula, range, REF_ERROR);

            return s_copyVisitor.Reference3D(ctx, range, modifiedFirstSheet, modifiedLastSheet, modifiedReference.Value);
        }

        public TransformedSymbol ExternalSheetReference(ModContext ctx, SymbolRange range, int workbookIndex, string sheet, ReferenceArea reference)
        {
            // The sheet is a sheet of another workbook, so it isn't modified.
            var modifiedReference = ctx.Modifier.ModifyRef(ctx, reference);
            if (modifiedReference is null)
                return TransformedSymbol.ToText(ctx.Formula, range, REF_ERROR);

            return s_copyVisitor.ExternalSheetReference(ctx, range, workbookIndex, sheet, modifiedReference.Value);
        }

        public TransformedSymbol ExternalReference3D(ModContext ctx, SymbolRange range, int workbookIndex, string firstSheet, string lastSheet, ReferenceArea reference)
        {
            // The sheets are sheets of another workbook, so they aren't modified.
            var modifiedReference = ctx.Modifier.ModifyRef(ctx, reference);
            if (modifiedReference is null)
                return TransformedSymbol.ToText(ctx.Formula, range, REF_ERROR);

            return s_copyVisitor.ExternalReference3D(ctx, range, workbookIndex, firstSheet, lastSheet, modifiedReference.Value);
        }

        public TransformedSymbol Function(ModContext ctx, SymbolRange range, ReadOnlySpan<char> functionName, IReadOnlyList<TransformedSymbol> arguments)
        {
            var modifiedFunction = ctx.Modifier.ModifyFunction(ctx, functionName);
            return s_copyVisitor.Function(ctx, range, modifiedFunction, arguments);
        }

        public TransformedSymbol Function(ModContext ctx, SymbolRange range, string sheetName, ReadOnlySpan<char> functionName, IReadOnlyList<TransformedSymbol> arguments)
        {
            var modifiedSheet = ctx.Modifier.ModifySheet(ctx, sheetName);
            if (modifiedSheet is null)
                return TransformedSymbol.ToText(ctx.Formula, range, REF_ERROR);

            return s_copyVisitor.Function(ctx, range, modifiedSheet, functionName, arguments);
        }

        public TransformedSymbol ExternalFunction(ModContext ctx, SymbolRange range, int workbookIndex, string sheetName, ReadOnlySpan<char> functionName, IReadOnlyList<TransformedSymbol> arguments)
        {
            return s_copyVisitor.ExternalFunction(ctx, range, workbookIndex, sheetName, functionName, arguments);
        }

        public TransformedSymbol ExternalFunction(ModContext ctx, SymbolRange range, int workbookIndex, ReadOnlySpan<char> functionName, IReadOnlyList<TransformedSymbol> arguments)
        {
            return s_copyVisitor.ExternalFunction(ctx, range, workbookIndex, functionName, arguments);
        }

        public TransformedSymbol CellFunction(ModContext ctx, SymbolRange range, RowCol cell, IReadOnlyList<TransformedSymbol> arguments)
        {
            var modifiedCell = ctx.Modifier.ModifyCellFunction(ctx, cell);
            if (modifiedCell is null)
                return TransformedSymbol.ToText(ctx.Formula, range, REF_ERROR);

            return s_copyVisitor.CellFunction(ctx, range, modifiedCell.Value, arguments);
        }

        public TransformedSymbol StructureReference(ModContext ctx, SymbolRange range, StructuredReferenceArea area, string? firstColumn, string? lastColumn)
        {
            return s_copyVisitor.StructureReference(ctx, range, area, firstColumn, lastColumn);
        }

        public TransformedSymbol StructureReference(ModContext ctx, SymbolRange range, string table, StructuredReferenceArea area, string? firstColumn, string? lastColumn)
        {
            var modifiedTableName = ctx.Modifier.ModifyTable(ctx, table);
            if (modifiedTableName is null)
                return TransformedSymbol.ToText(ctx.Formula, range, REF_ERROR);

            return s_copyVisitor.StructureReference(ctx, range, modifiedTableName, area, firstColumn, lastColumn);
        }

        public TransformedSymbol ExternalStructureReference(ModContext ctx, SymbolRange range, int workbookIndex, string table, StructuredReferenceArea area, string? firstColumn, string? lastColumn)
        {
            return s_copyVisitor.ExternalStructureReference(ctx, range, workbookIndex, table, area, firstColumn, lastColumn);
        }

        public TransformedSymbol Name(ModContext ctx, SymbolRange range, string name)
        {
            return s_copyVisitor.Name(ctx, range, name);
        }

        public TransformedSymbol SheetName(ModContext ctx, SymbolRange range, string sheet, string name)
        {
            var modifiedSheet = ctx.Modifier.ModifySheet(ctx, sheet);
            if (modifiedSheet is null)
                return TransformedSymbol.ToText(ctx.Formula, range, REF_ERROR);

            return s_copyVisitor.SheetName(ctx, range, modifiedSheet, name);
        }

        public TransformedSymbol BangName(ModContext ctx, SymbolRange range, string name)
        {
            return s_copyVisitor.BangName(ctx, range, name);
        }

        public TransformedSymbol ExternalName(ModContext ctx, SymbolRange range, int workbookIndex, string name)
        {
            return s_copyVisitor.ExternalName(ctx, range, workbookIndex, name);
        }

        public TransformedSymbol ExternalSheetName(ModContext ctx, SymbolRange range, int workbookIndex, string sheet, string name)
        {
            return s_copyVisitor.ExternalSheetName(ctx, range, workbookIndex, sheet, name);
        }

        public TransformedSymbol ExternalDynamicDataExchange(ModContext ctx, SymbolRange range, int workbookIndex, string item)
        {
            return s_copyVisitor.ExternalDynamicDataExchange(ctx, range, workbookIndex, item);
        }

        public TransformedSymbol DynamicDataExchange(ModContext ctx, SymbolRange range, string application, string topic, string item)
        {
            // The prefix is the application and the topic of a DDE link, not a sheet, so a sheet rename doesn't apply.
            return s_copyVisitor.DynamicDataExchange(ctx, range, application, topic, item);
        }

        public TransformedSymbol BinaryNode(ModContext ctx, SymbolRange range, BinaryOperation operation, TransformedSymbol leftNode, TransformedSymbol rightNode)
        {
            return s_copyVisitor.BinaryNode(ctx, range, operation, leftNode, rightNode);
        }

        public TransformedSymbol Unary(ModContext ctx, SymbolRange range, UnaryOperation operation, TransformedSymbol node)
        {
            return s_copyVisitor.Unary(ctx, range, operation, node);
        }

        public TransformedSymbol Nested(ModContext ctx, SymbolRange range, TransformedSymbol node)
        {
            return s_copyVisitor.Nested(ctx, range, node);
        }
    }
}
