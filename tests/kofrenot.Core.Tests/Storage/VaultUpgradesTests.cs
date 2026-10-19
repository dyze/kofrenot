namespace Kofrenot.Core.Tests.Storage;

public sealed class VaultUpgradesTests : IDisposable
{
    private readonly TempFolder _temp = new();
    private readonly RingLog _log = new();
    private readonly Guid _vault = Guid.NewGuid();

    private string FilePath => Path.Combine(_temp.Sub("data"), "vault-upgrades.json");

    [Fact]
    public void NoFile_MeansNoUpgradeAgreed() =>
        Assert.False(Upgrades().IsAgreed(_vault, 4));

    [Fact]
    public void AnAgreedUpgrade_IsKeptPerVault_AndInTheFile()
    {
        Upgrades().Agree(_vault, 4);

        var upgrades = Upgrades();
        Assert.True(upgrades.IsAgreed(_vault, 4));
        Assert.True(upgrades.IsAgreed(_vault, 3));
        Assert.False(upgrades.IsAgreed(Guid.NewGuid(), 4));
    }

    [Fact]
    public void AnUpgradeToANewerFormat_IsAskedAgain()
    {
        var upgrades = Upgrades();
        upgrades.Agree(_vault, 4);

        Assert.False(upgrades.IsAgreed(_vault, 5));
        upgrades.Agree(_vault, 5);
        upgrades.Agree(_vault, 4);
        Assert.True(Upgrades().IsAgreed(_vault, 5));
    }

    [Fact]
    public void ADamagedFile_MeansNoUpgradeAgreed()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, "{ not json");

        Assert.False(Upgrades().IsAgreed(_vault, 4));
        Assert.Contains(_log.Entries, entry => entry.Level == LogLevel.Warning);
    }

    public void Dispose() => _temp.Dispose();

    private VaultUpgrades Upgrades() => new(FilePath, _log);
}
