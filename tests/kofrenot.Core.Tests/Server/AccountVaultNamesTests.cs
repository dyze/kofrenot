namespace Kofrenot.Core.Tests.Server;

public sealed class AccountVaultNamesTests
{
    private const int Iterations = 10;

    private static readonly (Identity Identity, UnlockedIdentity Unlocked, RecoveryKey Recovery) Account = Identity.Create("account phrase", Iterations);
    private static readonly Guid Store = Guid.NewGuid();

    [Theory]
    [InlineData("Personal")]
    [InlineData("")]
    [InlineData("Écrits · 日本")]
    public void AName_OpensWithTheIdentity(string name)
    {
        var sealedName = AccountVaultNames.Seal(name, Account.Identity, Store);

        Assert.Equal(name, AccountVaultNames.Open(sealedName, Account.Unlocked, Store));
    }

    [Fact]
    public void Names_AllHaveTheSameSize_UpToABlock()
    {
        var shortName = AccountVaultNames.Seal("A", Account.Identity, Store);
        var longer = AccountVaultNames.Seal(new string('x', 200), Account.Identity, Store);
        var longest = AccountVaultNames.Seal(new string('x', 300), Account.Identity, Store);

        Assert.Equal(shortName.Length, longer.Length);
        Assert.Equal(shortName.Length + AccountVaultNames.Block, longest.Length);
        Assert.Equal(new string('x', 300), AccountVaultNames.Open(longest, Account.Unlocked, Store));
    }

    [Fact]
    public void AName_DoesNotOpen_ForAnotherStore_OrIdentity_OrDamaged()
    {
        var sealedName = AccountVaultNames.Seal("Personal", Account.Identity, Store);
        var other = Identity.Create("other phrase", Iterations).Unlocked;
        var damaged = sealedName.ToArray();
        damaged[^1] ^= 1;

        Assert.Null(AccountVaultNames.Open(sealedName, Account.Unlocked, Guid.NewGuid()));
        Assert.Null(AccountVaultNames.Open(sealedName, other, Store));
        Assert.Null(AccountVaultNames.Open(damaged, Account.Unlocked, Store));
        Assert.Null(AccountVaultNames.Open([1, 2, 3], Account.Unlocked, Store));
    }

    [Fact]
    public void AnOffCurvePoint_IsDamaged()
    {
        var sealedName = AccountVaultNames.Seal("Personal", Account.Identity, Store);
        sealedName.AsSpan(5, 64).Fill(1);

        Assert.Null(AccountVaultNames.Open(sealedName, Account.Unlocked, Store));
    }

    [Fact]
    public void ANameTooLong_IsRefused() =>
        Assert.Throws<ArgumentException>(() => AccountVaultNames.Seal(new string('x', ushort.MaxValue + 1), Account.Identity, Store));
}
