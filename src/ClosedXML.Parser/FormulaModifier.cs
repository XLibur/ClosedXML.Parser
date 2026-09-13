using System;

namespace ClosedXML.Parser;

/// <summary>
/// Says how a formula changes when the workbook around it changes, e.g. when a sheet is renamed or deleted,
/// or rows are inserted. Inherit it, override the <c>Modify*</c> methods for the parts of a formula that
/// change, and pass it to <see cref="FormulaConverter.ModifyA1"/> or <see cref="FormulaConverter.ModifyR1C1"/>.
/// The rest of the formula is written back as it was. This class itself changes nothing.
/// </summary>
public partial class FormulaModifier
{
    /// <summary>
    /// Asks the <see cref="ModContext.Modifier"/> of the context about each part of the formula that can
    /// change, and writes a part again only when the answer differs from what it passed in. It holds no
    /// state, so one serves every modifier.
    /// </summary>
    internal static readonly IAstFactory<TransformedSymbol, TransformedSymbol, ModContext> Factory = new Rewriter();

    /// <summary>
    /// Modify the name of a sheet, e.g. rename it. It gets a sheet of this workbook only: a sheet behind a book
    /// prefix belongs to another workbook and is left as it is.
    /// </summary>
    /// <param name="ctx">Where the formula is.</param>
    /// <param name="sheetName">Original sheet name.</param>
    /// <returns>New sheet name, or <c>null</c> if the sheet has been deleted and the part should be <c>#REF!</c>.</returns>
    /// <remarks>
    /// A new name must be one <see cref="NameUtils.IsSheetNameValid"/> accepts. A renamed sheet is
    /// written back into the formula, and a name no workbook could hold has no spelling to write, so
    /// returning one raises an <see cref="InvalidOperationException"/> rather than producing a
    /// formula the library can't read back.
    /// </remarks>
    protected virtual string? ModifySheet(ModContext ctx, string sheetName)
    {
        return sheetName;
    }

    /// <summary>
    /// Modify the sheets a 3D reference spans, e.g. narrow the reference when the sheet at one of
    /// its ends is deleted. It gets the two sheets of one reference together, and both are sheets
    /// of this workbook: a 3D reference behind a book prefix spans sheets of another workbook and
    /// is left as it is.
    /// </summary>
    /// <param name="ctx">Where the formula is.</param>
    /// <param name="firstSheet">The sheet the reference starts at.</param>
    /// <param name="lastSheet">The sheet the reference ends at.</param>
    /// <returns>The sheets the reference spans now, or <c>null</c> if the part should be <c>#REF!</c>.</returns>
    /// <remarks>
    /// Deleting the sheet at one end narrows the reference rather than breaking it: Excel turns
    /// <c>Sheet1:Sheet3!A1</c> into <c>Sheet2:Sheet3!A1</c> when <c>Sheet1</c> is deleted. Only a
    /// caller that knows tab order can name the sheet that takes over, which is why the two sheets
    /// arrive together. This default asks <see cref="ModifySheet"/> about each end on its own and
    /// gives up the whole reference when either sheet is gone. Each name it answers with is held
    /// to the same rule <see cref="ModifySheet"/> is.
    /// </remarks>
    protected virtual SheetRange? ModifySheetRange(ModContext ctx, string firstSheet, string lastSheet)
    {
        var modifiedFirstSheet = ModifySheet(ctx, firstSheet);
        var modifiedLastSheet = ModifySheet(ctx, lastSheet);
        if (modifiedFirstSheet is null || modifiedLastSheet is null)
            return null;

        return new SheetRange(modifiedFirstSheet, modifiedLastSheet);
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
}
