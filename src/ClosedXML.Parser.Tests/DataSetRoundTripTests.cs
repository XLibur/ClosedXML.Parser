using System.Security.Cryptography;
using System.Text;

namespace ClosedXML.Parser.Tests;

/// <summary>
/// Run every formula of the data sets through <see cref="FormulaConverter"/> and compare the output with the
/// output recorded in the data set. A formula that doesn't come back as written must be listed in
/// <c>known-diffs.csv</c> with its exact output, and the R1C1 form of every formula must hash to the digest in
/// <c>r1c1.sha256</c>. Any change to formula modification or to the conversion between reference styles fails
/// here, formula by formula, so a refactoring can't change the output unnoticed.
/// </summary>
public class DataSetRoundTripTests
{
    private const string Identity = "identity";
    private const string RoundTrip = "round-trip";

    // An anchor away from A1, so relative references get offsets of both signs.
    private const int Row = 1000;
    private const int Col = 100;

    private static readonly FormulaModifier IdentityModifier = new();

    [Theory]
    [InlineData("enron")]
    [InlineData("euses")]
    [InlineData("contributions")]
    public void Formulas_convert_as_recorded(string dataSet)
    {
        var directory = Path.Combine("data", dataSet);
        var failsPath = Path.Combine(directory, "known-fails.csv");
        var fails = File.Exists(failsPath) ? DataSets.ReadCsv(failsPath).ToHashSet() : new HashSet<string>();
        var formulas = DataSets.ReadCsv(Path.Combine(directory, "formulas.csv"))
            .Where(formula => !fails.Contains(formula))
            .Distinct()
            .OrderBy(formula => formula, StringComparer.Ordinal)
            .ToList();

        var diffsPath = Path.Combine(directory, "known-diffs.csv");
        var expectedDiffs = File.Exists(diffsPath)
            ? DataSets.ReadRecords(diffsPath).ToDictionary(record => (record[0], record[1]), record => record[2])
            : new Dictionary<(string, string), string>();
        var digestPath = Path.Combine(directory, "r1c1.sha256");
        var expectedDigest = File.Exists(digestPath) ? File.ReadAllText(digestPath).Trim() : "(none)";

        var actualDiffs = new List<string[]>();
        var r1c1Forms = new List<string[]>(formulas.Count);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var formula in formulas)
        {
            var identity = Convert(() => FormulaConverter.ModifyA1(formula, "Sheet1", Row, Col, IdentityModifier));
            if (identity != formula)
                actualDiffs.Add(new[] { Identity, formula, identity });

            var r1c1 = Convert(() => FormulaConverter.ToR1C1(formula, Row, Col));
            r1c1Forms.Add(new[] { formula, r1c1 });
            hash.AppendData(Encoding.UTF8.GetBytes(r1c1 + "\n"));

            var roundTrip = IsThrow(r1c1) ? r1c1 : Convert(() => FormulaConverter.ToA1(r1c1, Row, Col));
            if (roundTrip != formula)
                actualDiffs.Add(new[] { RoundTrip, formula, roundTrip });
        }

        var actualDigest = System.Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();

        var mismatches = new List<string>();
        var actualKeys = new HashSet<(string, string)>();
        foreach (var diff in actualDiffs)
        {
            var key = (diff[0], diff[1]);
            actualKeys.Add(key);
            if (!expectedDiffs.TryGetValue(key, out var expected))
                mismatches.Add($"{diff[0]} of '{diff[1]}' gives '{diff[2]}', which isn't in known-diffs.csv.");
            else if (expected != diff[2])
                mismatches.Add($"{diff[0]} of '{diff[1]}' gives '{diff[2]}', known-diffs.csv records '{expected}'.");
        }

        foreach (var key in expectedDiffs.Keys.Where(key => !actualKeys.Contains(key)))
            mismatches.Add($"{key.Item1} of '{key.Item2}' gives the formula back unchanged, but known-diffs.csv lists it.");

        if (actualDigest != expectedDigest)
            mismatches.Add($"The R1C1 forms hash to {actualDigest}, r1c1.sha256 records {expectedDigest}.");

        if (mismatches.Count == 0)
            return;

        // Write what this run produced, so an intended change can be reviewed and copied over the recorded files.
        // The R1C1 forms aren't recorded, only their digest; run this test before and after a change and diff the
        // two r1c1.actual.csv files to see which forms changed.
        var actualDiffsPath = Path.GetFullPath(Path.Combine(directory, "known-diffs.actual.csv"));
        var actualDigestPath = Path.GetFullPath(Path.Combine(directory, "r1c1.actual.sha256"));
        var r1c1FormsPath = Path.GetFullPath(Path.Combine(directory, "r1c1.actual.csv"));
        DataSets.WriteRecords(actualDiffsPath, actualDiffs.OrderBy(d => d[0], StringComparer.Ordinal).ThenBy(d => d[1], StringComparer.Ordinal));
        File.WriteAllText(actualDigestPath, actualDigest + "\n");
        DataSets.WriteRecords(r1c1FormsPath, r1c1Forms);

        Assert.True(false,
            $"{mismatches.Count} outputs of the {dataSet} data set differ from the recorded ones. The first ones:{Environment.NewLine}" +
            string.Join(Environment.NewLine, mismatches.Take(20)) + Environment.NewLine +
            $"This run's outputs are in {actualDiffsPath} and {actualDigestPath} (R1C1 forms in {r1c1FormsPath}).");
    }

    private static string Convert(Func<string> convert)
    {
        try
        {
            return convert();
        }
        catch (Exception e)
        {
            // Recorded like an output, so a formula that starts or stops throwing is a changed output too.
            return $"<throws {e.GetType().Name}>";
        }
    }

    private static bool IsThrow(string output) => output.StartsWith("<throws ", StringComparison.Ordinal);
}
