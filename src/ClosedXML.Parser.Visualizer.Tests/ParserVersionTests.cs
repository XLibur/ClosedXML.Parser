namespace ClosedXML.Parser.Visualizer.Tests;

public class ParserVersionTests
{
    [Fact]
    public void Reads_the_version_and_the_commit()
    {
        var version = ParserVersion.FromInformationalVersion("2.1.0-alpha.0.4+de8da5c0123456789abcdef0123456789abcdef0");

        Assert.Equal("2.1.0-alpha.0.4", version.Version);
        Assert.Equal("de8da5c", version.ShortCommit);
        Assert.Equal("https://github.com/XLibur/ClosedXML.Parser/commit/de8da5c0123456789abcdef0123456789abcdef0", version.CommitUrl);
    }

    [Theory]
    [InlineData("2.1.0", "2.1.0")]
    [InlineData("2.1.0+build", "2.1.0")]
    [InlineData(null, "unknown")]
    public void Has_no_commit_without_a_sha(string? informationalVersion, string expected)
    {
        var version = ParserVersion.FromInformationalVersion(informationalVersion);

        Assert.Equal(expected, version.Version);
        Assert.Null(version.Commit);
        Assert.Null(version.CommitUrl);
    }
}
