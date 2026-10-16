namespace Kofrenot.Core.Tests.Storage;

public sealed class KeyVaultTests
{
    // Few iterations: the tests check the logic, not the strength.
    private const int Iterations = 10;
    private static readonly byte[] Database = [7, 7, 7];

    [Fact]
    public void TheRightPassPhrase_UnlocksTheMasterKey()
    {
        var (wrapped, masterKey) = KeyVault.Create("correct horse", Database, Iterations);

        Assert.Equal(masterKey, KeyVault.Unlock(wrapped, "correct horse", Database));
        Assert.Equal(SecretBox.KeySize, masterKey.Length);
        Assert.Equal(Iterations, wrapped.Iterations);
    }

    [Fact]
    public void AWrongPassPhrase_UnlocksNothing()
    {
        var (wrapped, _) = KeyVault.Create("correct horse", Database, Iterations);

        Assert.Null(KeyVault.Unlock(wrapped, "Correct horse", Database));
    }

    [Fact]
    public void AnotherDatabase_UnlocksNothing()
    {
        var (wrapped, _) = KeyVault.Create("correct horse", Database, Iterations);

        Assert.Null(KeyVault.Unlock(wrapped, "correct horse", [8]));
    }

    [Fact]
    public void ThePassPhrase_IsComparedInNormalizedForm()
    {
        // "é" as one character, then as "e" followed by a combining accent.
        var (wrapped, masterKey) = KeyVault.Create("café", Database, Iterations);

        Assert.Equal(masterKey, KeyVault.Unlock(wrapped, "café", Database));
    }

    [Fact]
    public void EachDatabase_GetsItsOwnKeyAndSalt()
    {
        var (first, firstKey) = KeyVault.Create("same", Database, Iterations);
        var (second, secondKey) = KeyVault.Create("same", Database, Iterations);

        Assert.NotEqual(firstKey, secondKey);
        Assert.NotEqual(first.Salt, second.Salt);
    }

    [Fact]
    public void AnEmptyPassPhrase_IsRefused() =>
        Assert.Throws<ArgumentException>(() => KeyVault.Create("", Database, Iterations));
}
