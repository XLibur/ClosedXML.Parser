using System;
using JetBrains.Annotations;

namespace ClosedXML.Parser;

/// <summary>
/// Convert formulas between <em>A1</em> and <em>R1C1</em> style, and modify formulas.
/// </summary>
[PublicAPI]
public static class FormulaConverter
{
    private static readonly ToR1C1Modifier s_toR1C1 = new();
    private static readonly ToA1Modifier s_toA1 = new();

    /// <summary>
    /// Convert a formula in <em>A1</em> form to the <em>R1C1</em> form.
    /// </summary>
    /// <param name="formulaA1">Formula text.</param>
    /// <param name="row">The row origin of R1C1, from 1 to 1048576.</param>
    /// <param name="col">The column origin of R1C1, from 1 to 16384.</param>
    /// <returns>Formula converted to R1C1.</returns>
    /// <exception cref="ParsingException">The formula is not parseable.</exception>
    public static string ToR1C1(string formulaA1, int row, int col)
    {
        return Modify(formulaA1, string.Empty, row, col, isA1: true, s_toR1C1);
    }

    /// <summary>
    /// Convert a formula in <em>R1C1</em> form to the <em>A1</em> form.
    /// </summary>
    /// <param name="formulaR1C1">Formula text in R1C1.</param>
    /// <param name="row">The row origin of R1C1, from 1 to 1048576.</param>
    /// <param name="col">The column origin of R1C1, from 1 to 16384.</param>
    /// <returns>Formula converted to A1.</returns>
    /// <exception cref="ParsingException">The formula is not parseable.</exception>
    public static string ToA1(string formulaR1C1, int row, int col)
    {
        return Modify(formulaR1C1, string.Empty, row, col, isA1: false, s_toA1);
    }

    /// <summary>
    /// Modify a formula written in <em>A1</em> style with the passed <paramref name="modifier"/>.
    /// </summary>
    /// <param name="formulaA1">Original formula in A1 style.</param>
    /// <param name="sheet">Name of the sheet where is the formula.</param>
    /// <param name="row">Row number of formula.</param>
    /// <param name="col">Column number of formula.</param>
    /// <param name="modifier">Says what changes in the formula.</param>
    /// <returns>The modified formula, in A1 style.</returns>
    /// <exception cref="ParsingException">The formula is not parseable.</exception>
    public static string ModifyA1(string formulaA1, string sheet, int row, int col, FormulaModifier modifier)
    {
        return Modify(formulaA1, sheet, row, col, isA1: true, modifier ?? throw new ArgumentNullException(nameof(modifier)));
    }

    /// <summary>
    /// Modify a formula written in <em>R1C1</em> style with the passed <paramref name="modifier"/>.
    /// </summary>
    /// <param name="formulaR1C1">Original formula in R1C1 style.</param>
    /// <param name="sheet">Name of the sheet where is the formula.</param>
    /// <param name="row">Row number of formula.</param>
    /// <param name="col">Column number of formula.</param>
    /// <param name="modifier">Says what changes in the formula.</param>
    /// <returns>The modified formula, in R1C1 style.</returns>
    /// <exception cref="ParsingException">The formula is not parseable.</exception>
    public static string ModifyR1C1(string formulaR1C1, string sheet, int row, int col, FormulaModifier modifier)
    {
        return Modify(formulaR1C1, sheet, row, col, isA1: false, modifier ?? throw new ArgumentNullException(nameof(modifier)));
    }

    private static string Modify(string formula, string sheet, int row, int col, bool isA1, FormulaModifier modifier)
    {
        var ctx = new ModContext(formula, sheet, row, col, isA1, modifier);
        var modifiedFormula = isA1
            ? FormulaParser<TransformedSymbol, TransformedSymbol, ModContext>.CellFormulaA1(formula, ctx, FormulaModifier.Factory)
            : FormulaParser<TransformedSymbol, TransformedSymbol, ModContext>.CellFormulaR1C1(formula, ctx, FormulaModifier.Factory);
        return Normalize(modifiedFormula, formula);
    }

    private static string Normalize(TransformedSymbol transformedFormula, string originalFormula)
    {
        // Because of intersection operator, we trim the whitespaces at the end before sending
        // formula to the parser. Add them back, if necessary.
        var trimmed = originalFormula.TrimEnd();
        var endLength = originalFormula.Length - trimmed.Length;
        var trimmedEnd = originalFormula.AsSpan().Slice(trimmed.Length, endLength);
        return transformedFormula.ToString(trimmedEnd);
    }

    private sealed class ToR1C1Modifier : FormulaModifier
    {
        protected override ReferenceArea? ModifyRef(ModContext ctx, ReferenceArea reference)
        {
            return reference.ToR1C1(ctx.Row, ctx.Col);
        }

        protected override RowCol? ModifyCellFunction(ModContext ctx, RowCol cell)
        {
            return cell.ToR1C1(ctx.Row, ctx.Col);
        }
    }

    private sealed class ToA1Modifier : FormulaModifier
    {
        protected override ReferenceArea? ModifyRef(ModContext ctx, ReferenceArea reference)
        {
            return reference.ToA1OrError(ctx.Row, ctx.Col);
        }

        protected override RowCol? ModifyCellFunction(ModContext ctx, RowCol cell)
        {
            return cell.ToA1OrError(ctx.Row, ctx.Col);
        }
    }
}
