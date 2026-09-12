using System.Reflection;

namespace ClosedXML.Parser.Visualizer;

/// <summary>
/// The version of the parser assembly, for the footer. The site is deployed from each push to
/// develop, so it can be ahead of the last NuGet release.
/// </summary>
/// <param name="Version">The MinVer version, for example <c>2.1.0-alpha.0.4</c>.</param>
/// <param name="Commit">The SHA of the commit the parser was built from, when it is known.</param>
public sealed record ParserVersion(string Version, string? Commit)
{
    private const string RepositoryUrl = "https://github.com/XLibur/ClosedXML.Parser";

    public static ParserVersion Current { get; } = FromInformationalVersion(
        typeof(ParsingException).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    public string? ShortCommit => Commit?[..Math.Min(7, Commit.Length)];

    public string? CommitUrl => Commit is null ? null : $"{RepositoryUrl}/commit/{Commit}";

    /// <summary>
    /// Read an informational version such as <c>2.1.0-alpha.0.4+3f2a...</c>. The SDK appends the
    /// commit SHA as the last part of the build metadata.
    /// </summary>
    public static ParserVersion FromInformationalVersion(string? informationalVersion)
    {
        if (string.IsNullOrEmpty(informationalVersion))
            return new ParserVersion("unknown", null);

        var plus = informationalVersion.IndexOf('+');
        if (plus < 0)
            return new ParserVersion(informationalVersion, null);

        var lastMetadata = informationalVersion[(plus + 1)..].Split('.')[^1];
        var commit = lastMetadata.Length >= 7 && lastMetadata.All(Uri.IsHexDigit) ? lastMetadata : null;
        return new ParserVersion(informationalVersion[..plus], commit);
    }
}
