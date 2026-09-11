namespace ClosedXML.Parser.Tests;

/// <summary>
/// Error values Excel added after [MS-XLSX] was written. Its grammar lists only the ones up to
/// <c>#GETTING_DATA</c>; these come with dynamic arrays, linked data types and Python in Excel.
/// </summary>
public static class ErrorValues
{
    public static TheoryData<string> AddedAfterMsXlsx => new()
    {
        "#SPILL!",
        "#CALC!",
        "#FIELD!",
        "#BLOCKED!",
        "#CONNECT!",
        "#BUSY!",
        "#UNKNOWN!",
        "#EXTERNAL!",
        "#PYTHON!",
        "#TIMEOUT!",
    };
}
