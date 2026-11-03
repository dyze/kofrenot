namespace Kofrenot.Core.Tests.Storage;

public class VaultFormatTests
{
    [Fact]
    public void EachFormat_UpToTheCurrentOne_NamesTheFirstKofrenotWritingIt()
    {
        Assert.Equal("0.1", VaultFormat.Since(1));
        Assert.Equal("0.3", VaultFormat.Since(3));
        Assert.Equal("0.5", VaultFormat.Since(5));
        Assert.Equal("0.7", VaultFormat.Since(6));
        Assert.Equal("0.8", VaultFormat.Since(7));
        Assert.Equal("0.11", VaultFormat.Since(8));
        Assert.Equal("0.13", VaultFormat.Since(9));
        Assert.Equal("0.14", VaultFormat.Since(10));
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

    [Fact]
    public void TheNewerMessage_NamesBothFormats()
    {
        var message = VaultFormat.NewerMessage(VaultFormat.Current + 1);

        Assert.Contains($"vault format {VaultFormat.Current + 1}; this one reads up to format {VaultFormat.Current}", message);
        Assert.Contains("Install the latest kofrenot", message);
    }

    [Fact]
    public void TheOlderMessage_SaysWhichKofrenotNoLongerOpensTheUpgradedVault()
    {
        var message = VaultFormat.OlderMessage(9);

        Assert.Contains($"format 9: this kofrenot uses it once upgraded to format {VaultFormat.Current}", message);
        Assert.Contains($"kofrenot older than {VaultFormat.Since(VaultFormat.Current)} no longer opens", message);
    }
}
