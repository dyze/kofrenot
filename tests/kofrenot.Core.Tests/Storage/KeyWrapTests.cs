namespace Kofrenot.Core.Tests.Storage;

public sealed class KeyWrapTests
{
    // Few iterations: the tests check the logic, not the strength.
    private const int Iterations = 10;
    private static readonly byte[] Vault = [7, 7, 7];

    [Fact]
    public void TheRightPassPhrase_UnlocksTheMasterKey()
    {
        var (wrapped, masterKey) = KeyWrap.Create("correct horse", Vault, Iterations);

        Assert.Equal(masterKey, KeyWrap.Unlock(wrapped, "correct horse", Vault));
        Assert.Equal(SecretBox.KeySize, masterKey.Length);
        Assert.Equal(Iterations, wrapped.Iterations);
    }

    [Fact]
    public void AWrongPassPhrase_UnlocksNothing()
    {
        var (wrapped, _) = KeyWrap.Create("correct horse", Vault, Iterations);

        Assert.Null(KeyWrap.Unlock(wrapped, "Correct horse", Vault));
    }

    [Fact]
    public void AnotherVault_UnlocksNothing()
    {
        var (wrapped, _) = KeyWrap.Create("correct horse", Vault, Iterations);

        Assert.Null(KeyWrap.Unlock(wrapped, "correct horse", [8]));
    }

    [Fact]
    public void ThePassPhrase_IsComparedInNormalizedForm()
    {
        // "é" as one character, then as "e" followed by a combining accent.
        var (wrapped, masterKey) = KeyWrap.Create("café", Vault, Iterations);

        Assert.Equal(masterKey, KeyWrap.Unlock(wrapped, "café", Vault));
    }

    [Fact]
    public void EachVault_GetsItsOwnKeyAndSalt()
    {
        var (first, firstKey) = KeyWrap.Create("same", Vault, Iterations);
        var (second, secondKey) = KeyWrap.Create("same", Vault, Iterations);

        Assert.NotEqual(firstKey, secondKey);
        Assert.NotEqual(first.Salt, second.Salt);
    }

    [Fact]
    public void AnEmptyPassPhrase_IsRefused() =>
        Assert.Throws<ArgumentException>(() => KeyWrap.Create("", Vault, Iterations));
}
