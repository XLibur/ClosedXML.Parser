namespace ClosedXML.Parser;

internal static class StructuredReferenceAreaExtensions
{
    /// <summary>
    /// The item specifiers of an area, each with its own brackets.
    /// </summary>
    /// <remarks>
    /// A list rather than one string, because an area spanning two regions is written as two
    /// specifiers — <c>[[#Headers],[#Data]]</c> — and the caller has to know that it is looking at
    /// two, not one: a specifier list needs the brackets around it that a lone specifier does not.
    /// </remarks>
    public static IReadOnlyList<string> GetSpecifiers(this StructuredReferenceArea area)
    {
        return area switch
        {
            StructuredReferenceArea.None => [],
            StructuredReferenceArea.Data => ["[#Data]"],
            StructuredReferenceArea.Headers => ["[#Headers]"],
            StructuredReferenceArea.Totals => ["[#Totals]"],
            StructuredReferenceArea.Data | StructuredReferenceArea.Headers => ["[#Headers]", "[#Data]"],
            StructuredReferenceArea.Data | StructuredReferenceArea.Totals => ["[#Data]", "[#Totals]"],
            StructuredReferenceArea.All => ["[#All]"],
            StructuredReferenceArea.ThisRow => ["[#This Row]"],
            _ => throw new NotSupportedException($"Unexpected area {area}.")
        };
    }
}
