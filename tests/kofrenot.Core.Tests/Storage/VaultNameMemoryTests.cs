using System.Security.Cryptography;

namespace Kofrenot.Core.Tests.Storage;

public sealed class VaultNameMemoryTests : IDisposable
{
    private readonly TempFolder _temp = new();
    private readonly RingLog _log = new();
    private readonly XorProtector _protector = new();
    private readonly Guid _vault = Guid.NewGuid();

    private string FilePath => _temp.Sub("vault-names.json");

    [Fact]
    public void NoName_AtFirst()
    {
        Assert.Null(Memory().Recall(_vault));
    }

    [Fact]
    public void ARememberedName_IsRecalled_OnlyForItsVault()
    {
        Memory().Remember(_vault, "Privé");

        Assert.Equal("Privé", Memory().Recall(_vault));
        Assert.Null(Memory().Recall(Guid.NewGuid()));
    }

    [Fact]
    public void TheName_IsStoredProtected_AndNeverLogged()
    {
        Memory().Remember(_vault, "Secret project");

        Assert.DoesNotContain("Secret", File.ReadAllText(FilePath));
        Assert.DoesNotContain(Convert.ToBase64String("Secret project"u8.ToArray()), File.ReadAllText(FilePath));
        Assert.DoesNotContain(_log.Entries, entry => entry.Message.Contains("Secret"));
    }

    [Fact]
    public void TheSameName_IsNotWrittenAgain()
    {
        Memory().Remember(_vault, "Work");
        var written = File.GetLastWriteTimeUtc(FilePath);
        File.SetLastWriteTimeUtc(FilePath, written.AddDays(-1));

        Memory().Remember(_vault, "Work");

        Assert.Equal(written.AddDays(-1), File.GetLastWriteTimeUtc(FilePath));
    }

    [Fact]
    public void ANewName_ReplacesTheOldOne()
    {
        Memory().Remember(_vault, "Work");

        Memory().Remember(_vault, "Office");

        Assert.Equal("Office", Memory().Recall(_vault));
    }

    [Fact]
    public void AForgottenName_IsGone_AndTheOthersStay()
    {
        var other = Guid.NewGuid();
        Memory().Remember(_vault, "Work");
        Memory().Remember(other, "Home");

        Memory().Forget(_vault);
        Memory().Forget(_vault);

        Assert.Null(Memory().Recall(_vault));
        Assert.Equal("Home", Memory().Recall(other));
    }

    [Fact]
    public void ANameThatCannotBeUnprotected_IsNone()
    {
        Memory().Remember(_vault, "Work");
        _protector.Fails = true;

        Assert.Null(Memory().Recall(_vault));
        Assert.Contains(_log.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("vault name"));
    }

    public void Dispose() => _temp.Dispose();

    private VaultNameMemory Memory() => new(FilePath, _protector, _log);

    // Reversible, and hides the text: enough to check the name goes through the protector.
    private sealed class XorProtector : ISecretProtector
    {
        public bool Fails { get; set; }

        public byte[] Protect(byte[] secret) => [.. secret.Select(value => (byte)(value ^ 0x5A))];

        public byte[] Unprotect(byte[] protectedSecret) =>
            Fails ? throw new CryptographicException("Another user") : Protect(protectedSecret);
    }
}
