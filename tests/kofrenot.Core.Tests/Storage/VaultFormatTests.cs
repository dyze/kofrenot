namespace Kofrenot.Core.Tests.Storage;

public class VaultFormatTests
{
    [Fact]
    public void EachFormat_UpToTheCurrentOne_NamesTheFirstKofrenotWritingIt()
    {
        Assert.Equal("0.1", VaultFormat.Since(1));
        Assert.Equal("0.3", VaultFormat.Since(3));
        Assert.NotEmpty(VaultFormat.Since(VaultFormat.Current));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(VaultFormat.Current + 1)]
    public void AnUnknownFormat_HasNoFirstKofrenot(int format) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => VaultFormat.Since(format));

    [Fact]
    public void TheUpgradeQuestion_SaysWhichKofrenotNoLongerOpensTheVault()
    {
        var question = VaultFormat.UpgradeQuestion(3);

        Assert.Contains("format 3, written by kofrenot 0.3", question);
        Assert.Contains($"writes format {VaultFormat.Current}", question);
        Assert.Contains($"kofrenot older than {VaultFormat.Since(VaultFormat.Current)} no longer opens it", question);
    }
}
