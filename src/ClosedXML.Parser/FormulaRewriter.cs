using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ClosedXML.Parser;

public partial class FormulaModifier
{
    /// <summary>
    /// Writes a modified formula. It asks the <see cref="ModContext.Modifier"/> of the context about every
    /// part of the formula that can change, and writes a part again only when the answer differs from what
    /// it passed in. Everything else is copied from the original formula, character for character, so a
    /// modification changes what it changes and leaves the rest of the text as it was written.
    /// </summary>
    /// <remarks>
    /// A part is compared as a whole: a reference whose sheet is renamed is written again in full, so the
    /// area of <c>'Old sheet'!D5:D5</c> is written from its value and comes out as <c>New!D5</c>. Only a
    /// part that nothing touched keeps its own text.
    /// <para>
    /// One deliberate exception: <c>#REF!A1</c> and <c>#REF!#REF!</c> are always written as <c>#REF!</c>.
    /// See <see cref="ErrorNode"/>.
    /// </para>
    /// </remarks>
    private sealed class Rewriter : IAstFactory<TransformedSymbol, TransformedSymbol, ModContext>
    {
        // 1 quote on left, 1 quote on right size and at most 4 quotes inside.
        private const int QUOTE_RESERVE = 6;
        private const int SHEET_SEPARATOR_LEN = 1;
        private const int BOOK_PREFIX_LEN = 3;
        private const int MAX_R1_C1_LEN = 20;
        private const string REF_ERROR = "#REF!";
        private const string BANG_REF_ERROR = "!#REF!";

        public TransformedSymbol LogicalValue(ModContext ctx, SymbolRange range, bool value)
        {
            return TransformedSymbol.CopyOriginal(ctx.Formula, range);
        }

        public TransformedSymbol NumberValue(ModContext ctx, SymbolRange range, double value)
        {
            return TransformedSymbol.CopyOriginal(ctx.Formula, range);
        }

        public TransformedSymbol TextValue(ModContext ctx, SymbolRange range, string text)
        {
            return TransformedSymbol.CopyOriginal(ctx.Formula, range);
        }

        public TransformedSymbol ErrorValue(ModContext ctx, SymbolRange range, ReadOnlySpan<char> error)
        {
            return TransformedSymbol.CopyOriginal(ctx.Formula, range);
        }

        public TransformedSymbol ArrayNode(ModContext ctx, SymbolRange range, int rows, int columns, IReadOnlyList<TransformedSymbol> elements)
        {
            if (AllOriginal(elements))
                return TransformedSymbol.CopyOriginal(ctx.Formula, range);

            var sb = new StringBuilder(2 + elements.Sum(x => x.Length) + elements.Count);
            sb.AppendStartFragment(ctx, range, elements[0]);
            var i = 0;
            sb.Append(elements[i++].AsSpan());
            for (var col = 1; col < columns; ++col)
            {
                sb.AppendMiddleFragment(ctx, elements[i - 1], elements[i]);
                sb.Append(elements[i++].AsSpan());
            }

            for (var row = 1; row < rows; ++row)
            {
                sb.AppendMiddleFragment(ctx, elements[i - 1], elements[i]);
                sb.Append(elements[i++].AsSpan());
                for (var col = 1; col < columns; ++col)
                {
                    sb.AppendMiddleFragment(ctx, elements[i - 1], elements[i]);
                    sb.Append(elements[i++].AsSpan());
                }
            }

            sb.AppendEndFragment(ctx, range, elements[elements.Count - 1]);
            return TransformedSymbol.ToText(ctx.Formula, range, sb.ToString());
        }

        public TransformedSymbol BlankNode(ModContext ctx, SymbolRange range)
        {
            return TransformedSymbol.CopyOriginal(ctx.Formula, range);
        }

        public TransformedSymbol LogicalNode(ModContext ctx, SymbolRange range, bool value)
        {
            return TransformedSymbol.CopyOriginal(ctx.Formula, range);
        }

        public TransformedSymbol ErrorNode(ModContext ctx, SymbolRange range, ReadOnlySpan<char> error)
        {
            if (range.Length == error.Length)
                return TransformedSymbol.CopyOriginal(ctx.Formula, range);

            // `#REF!A1` and `#REF!#REF!` are an invalid formula that Excel can't parse. It is displayed,
            // but likely only because it is a serialization of internal structures. When a sheet is
            // deleted, the result is `#REF!`, which is how it is actually saved in the file. This is the
            // one part that is written again although nothing changed it.
            var symbol = ctx.Formula.AsSpan().Slice(range.Start, range.Length);
            if (symbol.StartsWith(REF_ERROR.AsSpan(), StringComparison.OrdinalIgnoreCase))
                return TransformedSymbol.ToText(ctx.Formula, range, REF_ERROR);

            // A bang reference to a deleted cell, `!#REF!`, has no sheet to modify.
            return TransformedSymbol.CopyOriginal(ctx.Formula, range);
        }

        public TransformedSymbol SheetErrorNode(ModContext ctx, SymbolRange range, int? workbookIndex, string sheet, ReadOnlySpan<char> error)
        {
            // A sheet of another workbook isn't modified.
            if (workbookIndex is not null)
                return TransformedSymbol.CopyOriginal(ctx.Formula, range);

            var modifiedSheet = ctx.Modifier.ModifySheet(ctx, sheet);
            if (modifiedSheet == sheet)
                return TransformedSymbol.CopyOriginal(ctx.Formula, range);

            var prefix = modifiedSheet is null ? SheetPrefix.Deleted : SheetPrefix.Sheet(modifiedSheet);
            var sb = new StringBuilder(sheet.Length + QUOTE_RESERVE + SHEET_SEPARATOR_LEN + error.Length);
            var nodeText = sb.AppendPrefix(prefix).Append(error).ToString();
            return TransformedSymbol.ToText(ctx.Formula, range, nodeText);
        }

        public TransformedSymbol NumberNode(ModContext ctx, SymbolRange range, double value)
        {
            return TransformedSymbol.CopyOriginal(ctx.Formula, range);
        }

        public TransformedSymbol TextNode(ModContext ctx, SymbolRange range, string text)
        {
            return TransformedSymbol.CopyOriginal(ctx.Formula, range);
        }

        public TransformedSymbol Reference(ModContext ctx, SymbolRange range, ReferenceArea reference)
        {
            var modifiedReference = ctx.Modifier.ModifyRef(ctx, reference);
            if (modifiedReference is null)
                return TransformedSymbol.ToText(ctx.Formula, range, REF_ERROR);

            if (modifiedReference.Value == reference)
                return TransformedSymbol.CopyOriginal(ctx.Formula, range);

            var nodeText = new StringBuilder(MAX_R1_C1_LEN)
                .AppendRef(modifiedReference.Value)
                .ToString();
            return TransformedSymbol.ToText(ctx.Formula, range, nodeText);
        }

        public TransformedSymbol SheetReference(ModContext ctx, SymbolRange range, string sheet, ReferenceArea reference)
        {
            var modifiedSheet = ctx.Modifier.ModifySheet(ctx, sheet);
            var modifiedReference = ctx.Modifier.ModifyRef(ctx, reference);
            if (modifiedSheet is null || modifiedReference is null)
                return TransformedSymbol.ToText(ctx.Formula, range, REF_ERROR);

            if (modifiedSheet == sheet && modifiedReference.Value == reference)
                return TransformedSymbol.CopyOriginal(ctx.Formula, range);

            var nodeText = new StringBuilder(sheet.Length + QUOTE_RESERVE + SHEET_SEPARATOR_LEN + MAX_R1_C1_LEN)
                .AppendPrefix(SheetPrefix.Sheet(modifiedSheet))
                .AppendRef(modifiedReference.Value)
                .ToString();
            return TransformedSymbol.ToText(ctx.Formula, range, nodeText);
        }

        public TransformedSymbol BangReference(ModContext ctx, SymbolRange range, ReferenceArea reference)
        {
            var modifiedReference = ctx.Modifier.ModifyRef(ctx, reference);
            if (modifiedReference is null)
                return TransformedSymbol.ToText(ctx.Formula, range, BANG_REF_ERROR);

            if (modifiedReference.Value == reference)
                return TransformedSymbol.CopyOriginal(ctx.Formula, range);

            var nodeText = new StringBuilder(SHEET_SEPARATOR_LEN + MAX_R1_C1_LEN)
                .AppendPrefix(SheetPrefix.Bang)
                .AppendRef(modifiedReference.Value)
                .ToString();
            return TransformedSymbol.ToText(ctx.Formula, range, nodeText);
        }

        public TransformedSymbol Reference3D(ModContext ctx, SymbolRange range, string firstSheet, string lastSheet, ReferenceArea reference)
        {
            var modifiedFirstSheet = ctx.Modifier.ModifySheet(ctx, firstSheet);
            var modifiedLastSheet = ctx.Modifier.ModifySheet(ctx, lastSheet);
            var modifiedReference = ctx.Modifier.ModifyRef(ctx, reference);
            if (modifiedFirstSheet is null || modifiedLastSheet is null || modifiedReference is null)
                return TransformedSymbol.ToText(ctx.Formula, range, REF_ERROR);

            if (modifiedFirstSheet == firstSheet && modifiedLastSheet == lastSheet && modifiedReference.Value == reference)
                return TransformedSymbol.CopyOriginal(ctx.Formula, range);

            var nodeText = new StringBuilder(firstSheet.Length + QUOTE_RESERVE + lastSheet.Length + QUOTE_RESERVE + SHEET_SEPARATOR_LEN + MAX_R1_C1_LEN)
                .AppendPrefix(SheetPrefix.Range(modifiedFirstSheet, modifiedLastSheet))
                .AppendRef(modifiedReference.Value)
                .ToString();
            return TransformedSymbol.ToText(ctx.Formula, range, nodeText);
        }

        public TransformedSymbol ExternalSheetReference(ModContext ctx, SymbolRange range, int workbookIndex, string sheet, ReferenceArea reference)
        {
            // The sheet is a sheet of another workbook, so it isn't modified.
            var modifiedReference = ctx.Modifier.ModifyRef(ctx, reference);
            if (modifiedReference is null)
                return TransformedSymbol.ToText(ctx.Formula, range, REF_ERROR);

            if (modifiedReference.Value == reference)
                return TransformedSymbol.CopyOriginal(ctx.Formula, range);

            var nodeText = new StringBuilder(BOOK_PREFIX_LEN + sheet.Length + QUOTE_RESERVE + SHEET_SEPARATOR_LEN + MAX_R1_C1_LEN)
                .AppendPrefix(SheetPrefix.Sheet(sheet, workbookIndex))
                .AppendRef(modifiedReference.Value)
                .ToString();
            return TransformedSymbol.ToText(ctx.Formula, range, nodeText);
        }

        public TransformedSymbol ExternalReference3D(ModContext ctx, SymbolRange range, int workbookIndex, string firstSheet, string lastSheet, ReferenceArea reference)
        {
            // The sheets are sheets of another workbook, so they aren't modified.
            var modifiedReference = ctx.Modifier.ModifyRef(ctx, reference);
            if (modifiedReference is null)
                return TransformedSymbol.ToText(ctx.Formula, range, REF_ERROR);

            if (modifiedReference.Value == reference)
                return TransformedSymbol.CopyOriginal(ctx.Formula, range);

            var nodeText = new StringBuilder(BOOK_PREFIX_LEN + firstSheet.Length + QUOTE_RESERVE + lastSheet.Length + QUOTE_RESERVE + SHEET_SEPARATOR_LEN + MAX_R1_C1_LEN)
                .AppendPrefix(SheetPrefix.Range(firstSheet, lastSheet, workbookIndex))
                .AppendRef(modifiedReference.Value)
                .ToString();
            return TransformedSymbol.ToText(ctx.Formula, range, nodeText);
        }

        public TransformedSymbol Function(ModContext ctx, SymbolRange range, ReadOnlySpan<char> functionName, IReadOnlyList<TransformedSymbol> arguments)
        {
            var modifiedFunction = ctx.Modifier.ModifyFunction(ctx, functionName);
            var nameChanged = !modifiedFunction.SequenceEqual(functionName);
            if (!nameChanged && AllOriginal(arguments))
                return TransformedSymbol.CopyOriginal(ctx.Formula, range);

            var sb = new StringBuilder(modifiedFunction.Length + 2 + arguments.Sum(static x => x.Length) + arguments.Count);
            if (nameChanged)
                sb.Append(modifiedFunction);
            else
                sb.AppendOriginalCallee(ctx, range, arguments);

            var nodeText = sb.AppendArguments(ctx, range, arguments).ToString();
            return TransformedSymbol.ToText(ctx.Formula, range, nodeText);
        }

        public TransformedSymbol Function(ModContext ctx, SymbolRange range, string sheetName, ReadOnlySpan<char> functionName, IReadOnlyList<TransformedSymbol> arguments)
        {
            var modifiedSheet = ctx.Modifier.ModifySheet(ctx, sheetName);
            if (modifiedSheet is null)
                return TransformedSymbol.ToText(ctx.Formula, range, REF_ERROR);

            var sheetChanged = modifiedSheet != sheetName;
            if (!sheetChanged && AllOriginal(arguments))
                return TransformedSymbol.CopyOriginal(ctx.Formula, range);

            var sb = new StringBuilder(sheetName.Length + QUOTE_RESERVE + SHEET_SEPARATOR_LEN + functionName.Length + 2 + arguments.Sum(static x => x.Length) + arguments.Count);
            if (sheetChanged)
                sb.AppendPrefix(SheetPrefix.Sheet(modifiedSheet)).Append(functionName);
            else
                sb.AppendOriginalCallee(ctx, range, arguments);

            var nodeText = sb.AppendArguments(ctx, range, arguments).ToString();
            return TransformedSymbol.ToText(ctx.Formula, range, nodeText);
        }

        public TransformedSymbol ExternalFunction(ModContext ctx, SymbolRange range, int workbookIndex, string sheetName, ReadOnlySpan<char> functionName, IReadOnlyList<TransformedSymbol> arguments)
        {
            // The sheet is a sheet of another workbook, so nothing before the arguments can change.
            if (AllOriginal(arguments))
                return TransformedSymbol.CopyOriginal(ctx.Formula, range);

            var nodeText = new StringBuilder(BOOK_PREFIX_LEN + sheetName.Length + QUOTE_RESERVE + SHEET_SEPARATOR_LEN + functionName.Length + 2 + arguments.Sum(static x => x.Length) + arguments.Count)
                .AppendOriginalCallee(ctx, range, arguments)
                .AppendArguments(ctx, range, arguments)
                .ToString();
            return TransformedSymbol.ToText(ctx.Formula, range, nodeText);
        }

        public TransformedSymbol ExternalFunction(ModContext ctx, SymbolRange range, int workbookIndex, ReadOnlySpan<char> functionName, IReadOnlyList<TransformedSymbol> arguments)
        {
            // The function is a function of another workbook, so nothing before the arguments can change.
            if (AllOriginal(arguments))
                return TransformedSymbol.CopyOriginal(ctx.Formula, range);

            var nodeText = new StringBuilder(BOOK_PREFIX_LEN + functionName.Length + 2 + arguments.Sum(static x => x.Length) + arguments.Count)
                .AppendOriginalCallee(ctx, range, arguments)
                .AppendArguments(ctx, range, arguments)
                .ToString();
            return TransformedSymbol.ToText(ctx.Formula, range, nodeText);
        }

        public TransformedSymbol CellFunction(ModContext ctx, SymbolRange range, RowCol cell, IReadOnlyList<TransformedSymbol> arguments)
        {
            var modifiedCell = ctx.Modifier.ModifyCellFunction(ctx, cell);
            if (modifiedCell is null)
                return TransformedSymbol.ToText(ctx.Formula, range, REF_ERROR);

            var cellChanged = modifiedCell.Value != cell;
            if (!cellChanged && AllOriginal(arguments))
                return TransformedSymbol.CopyOriginal(ctx.Formula, range);

            var sb = new StringBuilder(MAX_R1_C1_LEN + SHEET_SEPARATOR_LEN + arguments.Sum(static x => x.Length));
            if (cellChanged)
                sb.AppendRef(modifiedCell.Value);
            else
                sb.AppendOriginalCallee(ctx, range, arguments);

            var nodeText = sb.AppendArguments(ctx, range, arguments).ToString();
            return TransformedSymbol.ToText(ctx.Formula, range, nodeText);
        }

        public TransformedSymbol StructureReference(ModContext ctx, SymbolRange range, StructuredReferenceArea area, string? firstColumn, string? lastColumn)
        {
            // Nothing in a structured reference without a table can be modified.
            return TransformedSymbol.CopyOriginal(ctx.Formula, range);
        }

        public TransformedSymbol StructureReference(ModContext ctx, SymbolRange range, string table, StructuredReferenceArea area, string? firstColumn, string? lastColumn)
        {
            var modifiedTable = ctx.Modifier.ModifyTable(ctx, table);
            if (modifiedTable is null)
                return TransformedSymbol.ToText(ctx.Formula, range, REF_ERROR);

            if (modifiedTable == table)
                return TransformedSymbol.CopyOriginal(ctx.Formula, range);

            var nodeText = modifiedTable + GetIntraTableReference(area, firstColumn, lastColumn);
            return TransformedSymbol.ToText(ctx.Formula, range, nodeText);
        }

        public TransformedSymbol ExternalStructureReference(ModContext ctx, SymbolRange range, int workbookIndex, string table, StructuredReferenceArea area, string? firstColumn, string? lastColumn)
        {
            // The table is a table of another workbook, so it isn't modified.
            return TransformedSymbol.CopyOriginal(ctx.Formula, range);
        }

        public TransformedSymbol Name(ModContext ctx, SymbolRange range, string name)
        {
            return TransformedSymbol.CopyOriginal(ctx.Formula, range);
        }

        public TransformedSymbol SheetName(ModContext ctx, SymbolRange range, string sheet, string name)
        {
            var modifiedSheet = ctx.Modifier.ModifySheet(ctx, sheet);
            if (modifiedSheet is null)
                return TransformedSymbol.ToText(ctx.Formula, range, REF_ERROR);

            if (modifiedSheet == sheet)
                return TransformedSymbol.CopyOriginal(ctx.Formula, range);

            var nodeText = new StringBuilder(sheet.Length + QUOTE_RESERVE + SHEET_SEPARATOR_LEN + name.Length)
                .AppendPrefix(SheetPrefix.Sheet(modifiedSheet))
                .Append(name)
                .ToString();
            return TransformedSymbol.ToText(ctx.Formula, range, nodeText);
        }

        public TransformedSymbol BangName(ModContext ctx, SymbolRange range, string name)
        {
            return TransformedSymbol.CopyOriginal(ctx.Formula, range);
        }

        public TransformedSymbol ExternalName(ModContext ctx, SymbolRange range, int workbookIndex, string name)
        {
            return TransformedSymbol.CopyOriginal(ctx.Formula, range);
        }

        public TransformedSymbol ExternalSheetName(ModContext ctx, SymbolRange range, int workbookIndex, string sheet, string name)
        {
            // The sheet is a sheet of another workbook, so it isn't modified.
            return TransformedSymbol.CopyOriginal(ctx.Formula, range);
        }

        public TransformedSymbol ExternalDynamicDataExchange(ModContext ctx, SymbolRange range, int workbookIndex, string item)
        {
            return TransformedSymbol.CopyOriginal(ctx.Formula, range);
        }

        public TransformedSymbol DynamicDataExchange(ModContext ctx, SymbolRange range, string application, string topic, string item)
        {
            // The prefix is the application and the topic of a DDE link, not a sheet, so a sheet rename doesn't apply.
            return TransformedSymbol.CopyOriginal(ctx.Formula, range);
        }

        public TransformedSymbol BinaryNode(ModContext ctx, SymbolRange range, BinaryOperation operation, TransformedSymbol leftNode, TransformedSymbol rightNode)
        {
            if (leftNode.IsOriginal && rightNode.IsOriginal)
                return TransformedSymbol.CopyOriginal(ctx.Formula, range);

            var nodeText = new StringBuilder(leftNode.Length + rightNode.OriginalRange.Start - leftNode.OriginalRange.End + rightNode.Length)
                .AppendStartFragment(ctx, range, leftNode)
                .Append(leftNode.AsSpan())
                .AppendMiddleFragment(ctx, leftNode, rightNode)
                .Append(rightNode.AsSpan())
                .AppendEndFragment(ctx, range, rightNode)
                .ToString();
            return TransformedSymbol.ToText(ctx.Formula, range, nodeText);
        }

        public TransformedSymbol Unary(ModContext ctx, SymbolRange range, UnaryOperation operation, TransformedSymbol node)
        {
            if (node.IsOriginal)
                return TransformedSymbol.CopyOriginal(ctx.Formula, range);

            var nodeText = new StringBuilder(node.Length + 1)
                .AppendStartFragment(ctx, range, node)
                .Append(node.AsSpan())
                .AppendEndFragment(ctx, range, node)
                .ToString();
            return TransformedSymbol.ToText(ctx.Formula, range, nodeText);
        }

        public TransformedSymbol Nested(ModContext ctx, SymbolRange range, TransformedSymbol node)
        {
            if (node.IsOriginal)
                return TransformedSymbol.CopyOriginal(ctx.Formula, range);

            var nodeText = new StringBuilder(node.Length + 2)
                .AppendStartFragment(ctx, range, node)
                .Append(node.AsSpan())
                .AppendEndFragment(ctx, range, node)
                .ToString();
            return TransformedSymbol.ToText(ctx.Formula, range, nodeText);
        }

        /// <summary>
        /// Was every part of a node left as it was written? If so, the node keeps its own text.
        /// </summary>
        private static bool AllOriginal(IReadOnlyList<TransformedSymbol> nodes)
        {
            for (var i = 0; i < nodes.Count; ++i)
            {
                if (!nodes[i].IsOriginal)
                    return false;
            }

            return true;
        }

        private static string GetIntraTableReference(StructuredReferenceArea area, string? firstColumn, string? lastColumn)
        {
            if (firstColumn is null || lastColumn is null)
            {
                // No column

                // Shorthand for full table inside the table.
                if (area == StructuredReferenceArea.None)
                    return "[]";

                if (area == (StructuredReferenceArea.Headers | StructuredReferenceArea.Data))
                    return "[[#Headers],[#Data]]";

                if (area == (StructuredReferenceArea.Data | StructuredReferenceArea.Totals))
                    return "[[#Data],[#Totals]]";

                return Keyword(area);
            }

            if (firstColumn == lastColumn)
            {
                // One column
                if (area == StructuredReferenceArea.None)
                {
                    // One column, no keyword
                    return new StringBuilder(firstColumn.Length + 2)
                        .Append('[').Append(firstColumn).Append(']')
                        .ToString();
                }

                // One column, keyword
                var keywordList = KeywordList(area);
                return new StringBuilder(keywordList.Length + firstColumn.Length + 5)
                    .Append('[')
                    .Append(keywordList).Append(',')
                    .Append('[').Append(firstColumn).Append(']')
                    .Append(']')
                    .ToString();
            }
            else
            {
                // Two columns
                var keywordList = KeywordList(area);
                var sb = new StringBuilder(firstColumn.Length + lastColumn.Length + keywordList.Length + 8);
                sb.Append('[');
                if (keywordList.Length > 0)
                    sb.Append(keywordList).Append(',');

                return sb
                    .Append('[').Append(firstColumn).Append(']')
                    .Append(':')
                    .Append('[').Append(lastColumn).Append(']')
                    .Append(']')
                    .ToString();
            }

            static string KeywordList(StructuredReferenceArea area)
            {
                return area switch
                {
                    StructuredReferenceArea.Headers | StructuredReferenceArea.Data => "[#Headers],[#Data]",
                    StructuredReferenceArea.Data | StructuredReferenceArea.Totals => "[#Data],[#Totals]",
                    _ => Keyword(area),
                };
            }

            static string Keyword(StructuredReferenceArea area)
            {
                return area switch
                {
                    StructuredReferenceArea.None => string.Empty,
                    StructuredReferenceArea.Headers => "[#Headers]",
                    StructuredReferenceArea.Data => "[#Data]",
                    StructuredReferenceArea.Totals => "[#Totals]",
                    StructuredReferenceArea.All => "[#All]",
                    StructuredReferenceArea.ThisRow => "[#This Row]",
                    _ => throw new NotSupportedException(),
                };
            }
        }
    }
}
