namespace Kofrenot.Core.Tests.Settings;

public sealed class VaultListTests
{
    private static readonly VaultLocation A = new(@"D:\a");
    private static readonly VaultLocation B = new(@"D:\b");
    private static readonly VaultLocation C = new(@"D:\c");

    private static VaultList Of(params VaultLocation[] locations) => new(locations.Select(location => new RegisteredVault(location)));

    private static VaultLocation[] Locations(VaultList list) => [.. list.Vaults.Select(vault => vault.Location)];

    [Theory]
    [InlineData(@"D:\Sync\Vault", @"d:\sync\vault")]
    [InlineData(@"D:\Sync\Vault", @"D:\Sync\Vault\")]
    [InlineData(@"\\nas\share", @"\\NAS\share/")]
    public void TheSameFolder_IsTheSameLocation_WhateverTheCaseOrATrailingSeparator(string folder, string other)
    {
        Assert.Equal(new VaultLocation(folder), new VaultLocation(other));
        Assert.Equal(new VaultLocation(folder).GetHashCode(), new VaultLocation(other).GetHashCode());
    }

    [Fact]
    public void AnotherFolder_IsAnotherLocation()
    {
        Assert.NotEqual(A, B);
        Assert.False(A.Equals(null));
    }

    [Fact]
    public void Add_PutsTheVaultLast()
    {
        Assert.Equal([A, B, C], Locations(Of(A, B).Add(new RegisteredVault(C))));
    }

    [Fact]
    public void AVaultListedAlready_ByItsLocationOrItsId_IsRefused()
    {
        var id = Guid.NewGuid();
        var list = new VaultList([new RegisteredVault(A, id)]);

        Assert.True(list.IsListed(new VaultLocation(@"d:\A\"), null));
        Assert.True(list.IsListed(B, id));
        Assert.False(list.IsListed(B, Guid.NewGuid()));
        Assert.False(list.IsListed(B, null));
        Assert.Throws<ArgumentException>(() => list.Add(new RegisteredVault(B, id)));
    }

    [Fact]
    public void Duplicates_AreListedOnce_TheFirstKept()
    {
        var id = Guid.NewGuid();

        var list = new VaultList([new RegisteredVault(A, id), new RegisteredVault(B, id), new RegisteredVault(new VaultLocation(@"d:\a"))]);

        Assert.Equal([A], Locations(list));
    }

    [Fact]
    public void Remove_TakesTheVaultOut()
    {
        Assert.Equal([A, C], Locations(Of(A, B, C).Remove(B)));
        Assert.Equal([A], Locations(Of(A).Remove(B)));
    }

    [Theory]
    [InlineData(-1, new[] { "B", "A", "C" })]
    [InlineData(1, new[] { "A", "C", "B" })]
    [InlineData(-5, new[] { "B", "A", "C" })]
    [InlineData(5, new[] { "A", "C", "B" })]
    [InlineData(0, new[] { "A", "B", "C" })]
    public void Move_StopsAtEitherEnd(int offset, string[] expected)
    {
        var moved = Of(A, B, C).Move(B, offset);

        Assert.Equal(expected.Select(name => new VaultLocation($@"D:\{name}")), Locations(moved));
    }

    [Fact]
    public void Move_OfAVaultNotListed_ChangesNothing()
    {
        var list = Of(A, B);

        Assert.Same(list, list.Move(C, 1));
    }

    [Fact]
    public void With_ChangesOneVault()
    {
        var id = Guid.NewGuid();

        var list = Of(A, B).With(B, vault => vault with { Id = id });

        Assert.Equal([new RegisteredVault(A), new RegisteredVault(B, id)], list.Vaults);
        Assert.Equal(new RegisteredVault(B, id), list.Find(B));
        Assert.Null(list.Find(C));
    }
}
