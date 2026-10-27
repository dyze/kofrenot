namespace Kofrenot.Core.Tests;

public class AppVersionTests
{
    [Fact]
    public void Format_KeepsPlainVersion()
    {
        Assert.Equal("0.1", AppVersion.Format("0.1"));
    }

    [Fact]
    public void Format_DropsBuildMetadata()
    {
        Assert.Equal("0.2", AppVersion.Format("0.2+3f9a1c7d"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Format_MissingVersion_IsUnknown(string? raw)
    {
        Assert.Equal("unknown", AppVersion.Format(raw));
    }

    [Fact]
    public void Of_BuiltAssembly_HasAVersionWithoutMetadata()
    {
        var version = AppVersion.Of(typeof(AppVersion).Assembly);

        Assert.NotEqual("unknown", version);
        Assert.DoesNotContain("+", version);
    }

    [Fact]
    public void FormatWithCommit_ShortensTheCommit()
    {
        Assert.Equal("0.4 (4e9c313)", AppVersion.FormatWithCommit("0.4+4e9c3137c078155d"));
    }

    [Fact]
    public void AVersionUnderDevelopment_KeepsItsSuffix()
    {
        Assert.Equal("0.10-dev", AppVersion.Format("0.10-dev+4e9c3137c078155d"));
        Assert.Equal("0.10-dev (4e9c313)", AppVersion.FormatWithCommit("0.10-dev+4e9c3137c078155d"));
    }

    [Fact]
    public void FormatWithCommit_MarksADirtyBuild()
    {
        Assert.Equal("0.4 (4e9c313-dirty)", AppVersion.FormatWithCommit("0.4+4e9c3137c078155d.dirty"));
    }

    [Fact]
    public void FormatWithCommit_KeepsAShortCommitWhole()
    {
        Assert.Equal("0.4 (abc)", AppVersion.FormatWithCommit("0.4+abc"));
    }

    [Theory]
    [InlineData("0.4")]
    [InlineData("0.4+")]
    [InlineData("0.4+.dirty")]
    public void FormatWithCommit_WithoutCommit_IsTheVersion(string raw)
    {
        Assert.Equal("0.4", AppVersion.FormatWithCommit(raw));
    }

    [Fact]
    public void FormatWithCommit_ShowsTheCommitDate()
    {
        Assert.Equal("0.11-dev (4e9c313, 2023-11-14 22:13)", AppVersion.FormatWithCommit("0.11-dev+4e9c3137c078155d.1700000000", TimeZoneInfo.Utc));
    }

    [Fact]
    public void FormatWithCommit_ShowsTheCommitDateInTheZone()
    {
        var plusTwo = TimeZoneInfo.CreateCustomTimeZone("plus two", TimeSpan.FromHours(2), "plus two", "plus two");

        Assert.Equal("0.4 (4e9c313, 2023-11-15 00:13)", AppVersion.FormatWithCommit("0.4+4e9c3137c078155d.1700000000", plusTwo));
    }

    [Fact]
    public void FormatWithCommit_ShowsTheDateOfADirtyBuild()
    {
        Assert.Equal("0.4 (4e9c313-dirty, 2023-11-14 22:13)", AppVersion.FormatWithCommit("0.4+4e9c3137c078155d.1700000000.dirty", TimeZoneInfo.Utc));
    }

    [Theory]
    [InlineData("0.4+4e9c3137c078155d.soon")]
    [InlineData("0.4+4e9c3137c078155d.-5")]
    [InlineData("0.4+4e9c3137c078155d.99999999999999999")]
    public void FormatWithCommit_NotADate_IsLeftOut(string raw)
    {
        Assert.Equal("0.4 (4e9c313)", AppVersion.FormatWithCommit(raw, TimeZoneInfo.Utc));
    }

    [Fact]
    public void FormatWithCommit_MissingVersion_IsUnknown()
    {
        Assert.Equal("unknown", AppVersion.FormatWithCommit(null));
    }

    [Fact]
    public void BuildOf_BuiltAssembly_StartsWithItsVersion()
    {
        var assembly = typeof(AppVersion).Assembly;

        Assert.StartsWith(AppVersion.Of(assembly), AppVersion.BuildOf(assembly));
    }

    [Fact]
    public void WithCommitOf_BuiltAssembly_StartsWithItsVersion()
    {
        var assembly = typeof(AppVersion).Assembly;

        Assert.StartsWith(AppVersion.Of(assembly), AppVersion.WithCommitOf(assembly));
    }
}
