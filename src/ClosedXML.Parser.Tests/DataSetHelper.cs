using CsvHelper.Configuration.Attributes;
using CsvHelper.Configuration;
using CsvHelper;
using JetBrains.Annotations;
using System.Globalization;
using System.Text;

namespace ClosedXML.Parser.Tests;

internal static class DataSets
{
    public static IEnumerable<string> ReadCsv(string filename)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = false };
        using var reader = new StreamReader(filename);
        using var csv = new CsvReader(reader, config);
        var formulas = csv.GetRecords<Formula>();
        foreach (var formula in formulas)
            yield return formula.Text;
    }

    /// <summary>
    /// Read a CSV file whose records have several fields, e.g. an input formula and its output.
    /// </summary>
    public static List<string[]> ReadRecords(string filename)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = false };
        using var reader = new StreamReader(filename);
        using var csv = new CsvReader(reader, config);
        var records = new List<string[]>();
        while (csv.Read())
        {
            var fields = new string[csv.Parser.Count];
            for (var i = 0; i < fields.Length; i++)
                fields[i] = csv.GetField(i)!;

            records.Add(fields);
        }

        return records;
    }

    /// <summary>
    /// Write records the way the data set files are written: every field quoted, a line feed after each record.
    /// </summary>
    public static void WriteRecords(string filename, IEnumerable<string[]> records)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false,
            ShouldQuote = _ => true,
            NewLine = "\n",
        };
        using var writer = new StreamWriter(filename, false, new UTF8Encoding(false));
        using var csv = new CsvWriter(writer, config);
        foreach (var record in records)
        {
            foreach (var field in record)
                csv.WriteField(field);

            csv.NextRecord();
        }
    }

    [UsedImplicitly]
    private record Formula([Index(0)] string Text);
}
