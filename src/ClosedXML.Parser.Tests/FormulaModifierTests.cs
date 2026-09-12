namespace ClosedXML.Parser.Tests;

/// <summary>
/// Formula modification through its public interface, a <see cref="FormulaModifier"/> passed to
/// <see cref="FormulaConverter.ModifyA1"/> or <see cref="FormulaConverter.ModifyR1C1"/>.
/// </summary>
public class FormulaModifierTests
{
    [Theory]
    [InlineData("SUM(Sheet!A1:B2, 'Jane''s'!C3) + [1]Other!Name")]
    [InlineData("IF( A1 > 5 , \"Yes\" ,  {1,2;3,4} )  ")]
    [InlineData("(A1),B2")]
    [InlineData("SUM((A1):B2)")]
    [InlineData("SUM((Total_Cost Jan):(Total_Cost Apr.))")]
    public void Modifier_that_changes_nothing_writes_the_formula_as_it_was(string formula)
    {
        Assert.Equal(formula, FormulaConverter.ModifyA1(formula, "Sheet", 1, 1, new FormulaModifier()));
    }

    [Fact]
    public void Hooks_see_where_the_formula_is()
    {
        var modifier = new ContextRecordingModifier();

        FormulaConverter.ModifyA1("Other!A1", "Home", 7, 3, modifier);

        Assert.NotNull(modifier.Context);
        Assert.Equal("Home", modifier.Context!.Sheet);
        Assert.Equal(7, modifier.Context.Row);
        Assert.Equal(3, modifier.Context.Col);
        Assert.True(modifier.Context.IsA1);
    }

    #region ModifySheet

    [Theory]
    [InlineData("Old!B7:$D$10", "Old", "New", "New!B7:$D$10")]
    [InlineData("Old!B7:$D$10", "Old", "New sheet", "'New sheet'!B7:$D$10")]
    [InlineData("'Old Mike''s sheet'!B7:$D$10", "Old Mike's sheet", "New Mike's sheet", "'New Mike''s sheet'!B7:$D$10")]
    public void ModifySheet_can_rename_sheet_name(string formula, string oldSheetName, string newSheetName, string modifiedFormula)
    {
        var modifier = new SheetModifier { SheetMap = { { oldSheetName, newSheetName } } };
        AssertModifiedA1(formula, modifier, modifiedFormula);
    }

    [Theory]
    [InlineData("Old!#REF!", "Old", null, "#REF!#REF!")]
    [InlineData("Old!#REF!", "Old", "New", "New!#REF!")]
    [InlineData("'Old sheet'!#REF!", "Old sheet", "New", "New!#REF!")]
    [InlineData("'Old sheet'!#REF!", "Old sheet", "New sheet", "'New sheet'!#REF!")]
    [InlineData("Old! #REF!", "Old", "New", "New!#REF!")]
    public void ErrorNode_can_modify_sheet(string formula, string oldSheetName, string? newSheetName, string modifiedFormula)
    {
        var modifier = new SheetModifier { SheetMap = { { oldSheetName, newSheetName } } };
        AssertModifiedA1(formula, modifier, modifiedFormula);
    }

    [Theory]
    [InlineData("'[1]Old sheet'!#REF!", "Old sheet", "New", "'[1]Old sheet'!#REF!")]
    [InlineData("[1]Old!#REF!", "Old", null, "[1]Old!#REF!")]
    public void ErrorNode_leaves_sheet_of_another_workbook(string formula, string oldSheetName, string? newSheetName, string modifiedFormula)
    {
        var modifier = new SheetModifier { SheetMap = { { oldSheetName, newSheetName } } };
        AssertModifiedA1(formula, modifier, modifiedFormula);
    }

    [Theory]
    [InlineData("Old!B$5", "Old", null, "#REF!")]
    [InlineData("Old!B:D", "Old", "Shiny", "Shiny!B:D")]
    public void SheetReference_can_modify_sheet(string formula, string oldSheetName, string? newSheetName, string modifiedFormula)
    {
        var modifier = new SheetModifier { SheetMap = { { oldSheetName, newSheetName } } };
        AssertModifiedA1(formula, modifier, modifiedFormula);
    }

    [Theory]
    [InlineData("Old!F(5)", "Old", null, "#REF!")]
    [InlineData("Old!F(7)", "Old", "Shiny", "Shiny!F(7)")]
    public void SheetFunction_can_modify_sheet(string formula, string oldSheetName, string? newSheetName, string modifiedFormula)
    {
        var modifier = new SheetModifier { SheetMap = { { oldSheetName, newSheetName } } };
        AssertModifiedA1(formula, modifier, modifiedFormula);
    }

    [Theory]
    [InlineData("Sheet1:Sheet5!A1", "Sheet1", null, "#REF!")]
    [InlineData("Sheet1:Sheet5!A1", "Sheet1", "New sheet", "'New sheet:Sheet5'!A1")]
    [InlineData("Sheet1:Sheet5!A1", "Sheet5", "Sheet9", "Sheet1:Sheet9!A1")]
    public void Reference3D_can_modify_sheet(string formula, string oldSheetName, string? newSheetName, string modifiedFormula)
    {
        var modifier = new SheetModifier { SheetMap = { { oldSheetName, newSheetName } } };
        AssertModifiedA1(formula, modifier, modifiedFormula);
    }

    /// <summary>
    /// A sheet behind a book prefix is a sheet of another workbook, so a sheet of this workbook with the same
    /// name being renamed or deleted doesn't change it.
    /// </summary>
    [Theory]
    [InlineData("'[1]Old sheet:Other'!A1", "Old sheet", "New", "'[1]Old sheet:Other'!A1")]
    [InlineData("[1]Old:Other!A1", "Old", null, "[1]Old:Other!A1")]
    [InlineData("[1]First:Old!A1", "Old", "New", "[1]First:Old!A1")]
    [InlineData("'[1]Old sheet'!A1", "Old sheet", "New", "'[1]Old sheet'!A1")]
    [InlineData("[1]Old!Name", "Old", null, "[1]Old!Name")]
    [InlineData("[1]Old!F(1)", "Old", "New", "[1]Old!F(1)")]
    public void Sheet_of_another_workbook_is_not_modified(string formula, string oldSheetName, string? newSheetName, string modifiedFormula)
    {
        var modifier = new SheetModifier { SheetMap = { { oldSheetName, newSheetName } } };
        AssertModifiedA1(formula, modifier, modifiedFormula);
    }

    [Theory]
    [InlineData("Sheet!Name", "Sheet", null, "#REF!")]
    [InlineData("Sheet!Name", "Sheet", "New Sheet", "'New Sheet'!Name")]
    public void SheetName_can_modify_sheet(string formula, string oldSheetName, string? newSheetName, string modifiedFormula)
    {
        var modifier = new SheetModifier { SheetMap = { { oldSheetName, newSheetName } } };
        AssertModifiedA1(formula, modifier, modifiedFormula);
    }

    [Theory]
    [InlineData("Sdemo123|tik!'item'", "Sdemo123|tik", "New", "Sdemo123|tik!'item'")]
    [InlineData("Sdemo123|tik!'item'", "Sdemo123|tik", null, "Sdemo123|tik!'item'")]
    [InlineData("'My App|Topic 1'!'item'", "My App|Topic 1", "New", "'My App|Topic 1'!'item'")]
    public void DynamicDataExchange_prefix_is_not_a_sheet(string formula, string oldSheetName, string? newSheetName, string modifiedFormula)
    {
        var modifier = new SheetModifier { SheetMap = { { oldSheetName, newSheetName } } };
        AssertModifiedA1(formula, modifier, modifiedFormula);
    }

    [Theory]
    [InlineData("Old!R1C1", "Old", "New", "New!R1C1")]
    [InlineData("SUM(Old!R1C1,R[2]C)", "Old", "New sheet", "SUM('New sheet'!R1C1,R[2]C)")]
    [InlineData("'Old sheet'!R[1]C", "Old sheet", null, "#REF!")]
    public void ModifyR1C1_can_modify_sheet(string formula, string oldSheetName, string? newSheetName, string modifiedFormula)
    {
        var modifier = new SheetModifier { SheetMap = { { oldSheetName, newSheetName } } };
        Assert.Equal(modifiedFormula, FormulaConverter.ModifyR1C1(formula, "Sheet", 1, 1, modifier));
    }

    #endregion

    #region ModifyRef

    [Theory]
    [InlineData("A1+B2", "B2", "C3", "A1+C3")]
    [InlineData("SUM(Sheet!B2:C3)", "B2:C3", "$B$2:$D$9", "SUM(Sheet!$B$2:$D$9)")]
    [InlineData("A1+B2", "B2", null, "A1+#REF!")]
    public void ModifyRef_can_shift_reference(string formula, string reference, string? replacement, string modifiedFormula)
    {
        var modifier = new ShiftReferenceModifier { ReferenceMap = { { reference, replacement } } };
        AssertModifiedA1(formula, modifier, modifiedFormula);
    }

    [Theory]
    [InlineData("5 + !$B1", "$B1", "$7:$9", "5 + !$7:$9")]
    [InlineData("5 + !$B1", "$B1", null, "5 + !#REF!")]
    public void Bang_references_is_modified(string formula, string reference, string? replacement, string modifiedFormula)
    {
        var modifier = new ShiftReferenceModifier { ReferenceMap = { { reference, replacement } } };
        AssertModifiedA1(formula, modifier, modifiedFormula);
    }

    [Fact]
    public void ModifyCellFunction_can_shift_called_cell()
    {
        var modifier = new ShiftReferenceModifier { ReferenceMap = { { "B$3", "C$4" } } };
        AssertModifiedA1("B$3(5)", modifier, "C$4(5)");
    }

    [Fact]
    public void Log10_is_not_interpreted_as_cell_function()
    {
        var modifier = new ShiftReferenceModifier { ReferenceMap = { { "LOG10", "A1" } } };
        AssertModifiedA1("LOG10(LOG10)", modifier, "LOG10(A1)");
    }

    [Theory]
    [InlineData("R1C1+R[1]C", "R[1]C", "R[5]C", "R1C1+R[5]C")]
    [InlineData("SUM(R[1]C:R[2]C)", "R[1]C:R[2]C", null, "SUM(#REF!)")]
    public void ModifyR1C1_can_shift_reference(string formula, string reference, string? replacement, string modifiedFormula)
    {
        var modifier = new ShiftReferenceModifier { ReferenceMap = { { reference, replacement } } };
        Assert.Equal(modifiedFormula, FormulaConverter.ModifyR1C1(formula, "Sheet", 1, 1, modifier));
    }

    #endregion

    private static void AssertModifiedA1(string formula, FormulaModifier modifier, string expected)
    {
        Assert.Equal(expected, FormulaConverter.ModifyA1(formula, "Sheet", 1, 1, modifier));
    }

    private sealed class ContextRecordingModifier : FormulaModifier
    {
        public ModContext? Context { get; private set; }

        protected override string? ModifySheet(ModContext ctx, string sheetName)
        {
            Context = ctx;
            return sheetName;
        }
    }

    private sealed class SheetModifier : FormulaModifier
    {
        public Dictionary<string, string?> SheetMap { get; } = new();

        protected override string? ModifySheet(ModContext ctx, string sheetName)
        {
            return SheetMap.GetValueOrDefault(sheetName, sheetName);
        }
    }

    /// <summary>
    /// Replaces a reference written as a key of <see cref="ReferenceMap"/>, in the reference style of the formula.
    /// </summary>
    private sealed class ShiftReferenceModifier : FormulaModifier
    {
        public Dictionary<string, string?> ReferenceMap { get; } = new();

        protected override ReferenceArea? ModifyRef(ModContext ctx, ReferenceArea reference)
        {
            var written = ctx.IsA1 ? reference.GetDisplayStringA1() : reference.GetDisplayStringR1C1();
            if (!ReferenceMap.TryGetValue(written, out var replacement))
                return reference;

            if (replacement is null)
                return null;

            if (ctx.IsA1)
                return ReferenceParser.ParseA1(replacement);

            Assert.True(ReferenceParser.TryParseR1C1(replacement, out var area));
            return area;
        }

        protected override RowCol? ModifyCellFunction(ModContext ctx, RowCol cell)
        {
            if (ReferenceMap.TryGetValue(cell.GetDisplayStringA1(), out var replacement))
                return replacement is not null ? ReferenceParser.ParseA1(replacement).First : null;

            return cell;
        }
    }
}
