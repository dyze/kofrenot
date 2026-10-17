using System.Security.Cryptography;
using System.Text;

namespace Kofrenot.Core.Tests.Storage;

public sealed class KeyMemoryTests : IDisposable
{
    private static readonly byte[] Key = [1, 2, 3, 4];

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
        Assert.Null(memory.Recall(_vault));
    }

    [Fact]
    public void ARememberedKey_IsRecalled_OnlyForItsVault()
    {
        Memory().Remember(_vault, Key);

        var memory = Memory();
        Assert.True(memory.Has(_vault));
        Assert.Equal(Key, memory.Recall(_vault));
        Assert.Null(memory.Recall(Guid.NewGuid()));
        Assert.Contains(_log.Entries, entry => entry.Level == LogLevel.Info && entry.Message.Contains("remembered"));
    }

    [Fact]
    public void TheKey_IsStoredProtected()
    {
        Memory().Remember(_vault, Key);

        Assert.Contains(Convert.ToBase64String(_protector.Protect(Key)), File.ReadAllText(FilePath));
        Assert.DoesNotContain(Convert.ToBase64String(Key), File.ReadAllText(FilePath));
    }

    [Fact]
    public void AForgottenKey_IsGone_AndTheOthersStay()
    {
        var other = Guid.NewGuid();
        Memory().Remember(_vault, Key);
        Memory().Remember(other, Key);

        Memory().Forget(_vault);

        Assert.False(Memory().Has(_vault));
        Assert.True(Memory().Has(other));
        Assert.Contains(_log.Entries, entry => entry.Message.Contains("forgotten"));
    }

    [Fact]
    public void ForgettingAnUnknownKey_DoesNothing()
    {
        Memory().Forget(_vault);

        Assert.False(File.Exists(FilePath));
    }

    [Fact]
    public void AKeyThatCannotBeUnprotected_IsNotRecalled()
    {
        Memory().Remember(_vault, Key);
        _protector.Fails = true;

        Assert.Null(Memory().Recall(_vault));
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
