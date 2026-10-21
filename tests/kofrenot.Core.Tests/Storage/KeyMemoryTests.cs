using System.Security.Cryptography;
using System.Text;

namespace Kofrenot.Core.Tests.Storage;

public sealed class KeyMemoryTests : IDisposable
{
    private static readonly byte[] MasterKey = RandomNumberGenerator.GetBytes(SecretBox.KeySize);
    private static readonly UnlockedIdentity Identity = new(Guid.NewGuid(), [5, 6, 7, 8]);

    private readonly TempFolder _temp = new();
    private readonly RingLog _log = new();
    private readonly FakeProtector _protector = new();
    private readonly Guid _vault = Guid.NewGuid();

    private string FilePath => _temp.Sub("keys.json");

    [Fact]
    public void NothingIsRemembered_AtFirst()
    {
        var memory = Memory();

        Assert.False(memory.Has(_vault));
        Assert.Null(memory.RecallIdentity(_vault));
        Assert.Null(memory.RecallMasterKey(_vault));
    }

    [Fact]
    public void ARememberedIdentity_IsRecalled_OnlyForItsVault()
    {
        Memory().Remember(Upgraded(_vault));

        var memory = Memory();
        Assert.True(memory.Has(_vault));
        var recalled = memory.RecallIdentity(_vault);
        Assert.Equal(Identity.Id, recalled?.Id);
        Assert.Equal(Identity.PrivateKey, recalled?.PrivateKey);
        Assert.Null(memory.RecallMasterKey(_vault));
        Assert.Null(memory.RecallIdentity(Guid.NewGuid()));
        Assert.Contains(_log.Entries, entry => entry.Level == LogLevel.Info && entry.Message.Contains("remembered"));
    }

    [Fact]
    public void AFormat5Vault_RemembersItsMasterKey()
    {
        Memory().Remember(NotUpgraded(_vault));

        Assert.True(Memory().Has(_vault));
        Assert.Equal(MasterKey, Memory().RecallMasterKey(_vault));
        Assert.Null(Memory().RecallIdentity(_vault));
    }

    [Fact]
    public void TheIdentity_ReplacesTheMasterKey_OnceTheVaultIsUpgraded()
    {
        Memory().Remember(NotUpgraded(_vault));

        Memory().Remember(Upgraded(_vault));

        Assert.Null(Memory().RecallMasterKey(_vault));
        Assert.NotNull(Memory().RecallIdentity(_vault));
    }

    [Fact]
    public void TheKey_IsStoredProtected()
    {
        Memory().Remember(NotUpgraded(_vault));

        Assert.Contains(Convert.ToBase64String(_protector.Protect(MasterKey)), File.ReadAllText(FilePath));
        Assert.DoesNotContain(Convert.ToBase64String(MasterKey), File.ReadAllText(FilePath));
    }

    [Fact]
    public void AForgottenVault_IsGone_AndTheOthersStay()
    {
        var other = Guid.NewGuid();
        Memory().Remember(NotUpgraded(_vault));
        Memory().Remember(Upgraded(_vault));
        Memory().Remember(Upgraded(other));

        Memory().Forget(_vault);

        Assert.False(Memory().Has(_vault));
        Assert.True(Memory().Has(other));
        Assert.Contains(_log.Entries, entry => entry.Message.Contains("forgotten"));
    }

    [Fact]
    public void ForgettingAnUnknownVault_DoesNothing()
    {
        Memory().Forget(_vault);

        Assert.False(File.Exists(FilePath));
    }

    [Fact]
    public void AKeyThatCannotBeUnprotected_IsNotRecalled()
    {
        Memory().Remember(Upgraded(_vault));
        _protector.Fails = true;

        Assert.Null(Memory().RecallIdentity(_vault));
        Assert.Contains(_log.Entries, entry => entry.Level == LogLevel.Warning);
    }

    [Fact]
    public void ADamagedFile_MeansNothingRemembered()
    {
        Directory.CreateDirectory(_temp.Path);
        File.WriteAllText(FilePath, "{ not json");

        Assert.False(Memory().Has(_vault));
        Assert.Contains(_log.Entries, entry => entry.Level == LogLevel.Warning);
    }

    [Fact]
    public void ANullFile_MeansNothingRemembered()
    {
        Directory.CreateDirectory(_temp.Path);
        File.WriteAllText(FilePath, "null");

        Assert.False(Memory().Has(_vault));
    }

    public void Dispose() => _temp.Dispose();

    private static VaultAccess Upgraded(Guid vault) => new(new VaultHeader(VaultFormat.Current, vault), KeyRing.Of(MasterKey), Identity);

    private static VaultAccess NotUpgraded(Guid vault) => new(new VaultHeader(5, vault), KeyRing.Of(MasterKey), Identity: null);

    private KeyMemory Memory() => new(FilePath, _protector, _log);

    // Reversible and recognizable: enough to check the key goes through the protector.
    private sealed class FakeProtector : ISecretProtector
    {
        private static readonly byte[] Marker = Encoding.ASCII.GetBytes("protected:");

        public bool Fails { get; set; }

        public byte[] Protect(byte[] secret) => [.. Marker, .. secret];

        public byte[] Unprotect(byte[] protectedSecret) =>
            Fails ? throw new CryptographicException("Another user") : protectedSecret[Marker.Length..];
    }
}
