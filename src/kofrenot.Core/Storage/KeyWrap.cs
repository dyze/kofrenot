using System.Security.Cryptography;
using System.Text;

namespace Kofrenot.Core.Storage;

/// <summary>
/// A secret (a format 5 vault's master key, an identity's private key) encrypted with a key
/// derived from the pass phrase (PBKDF2-SHA256). Nothing secret.
/// </summary>
public sealed record WrappedKey(byte[] Salt, int Iterations, byte[] Sealed);

/// <summary>
/// Seals a secret with a pass phrase, and opens it again. Changing the pass phrase only seals the
/// same secret again, so what it protects stays as it is.
/// </summary>
public static class KeyWrap
{
    public const int DefaultIterations = 600_000;
    private const int SaltSize = 16;
    private static ReadOnlySpan<byte> Magic => "KFK1"u8;

    /// <summary>A new random master key, and its wrapped form.</summary>
    /// <param name="context">What the key belongs to (the vault id): the wrapped key opens only there.</param>
    public static (WrappedKey Wrapped, byte[] MasterKey) Create(string passPhrase, ReadOnlySpan<byte> context, int iterations = DefaultIterations)
    {
        var masterKey = RandomNumberGenerator.GetBytes(SecretBox.KeySize);
        return (Seal(masterKey, passPhrase, context, iterations), masterKey);
    }

    /// <summary><paramref name="secret"/> sealed with <paramref name="passPhrase"/>.</summary>
    /// <param name="context">What the secret belongs to (a vault, an identity): the wrapped key opens only there.</param>
    public static WrappedKey Seal(byte[] secret, string passPhrase, ReadOnlySpan<byte> context, int iterations = DefaultIterations)
    {
        if (string.IsNullOrEmpty(passPhrase))
        {
            throw new ArgumentException("The pass phrase is empty", nameof(passPhrase));
        }

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        return new WrappedKey(salt, iterations, new SecretBox(Derive(passPhrase, salt, iterations)).Seal(Magic, secret, context));
    }

    /// <summary>The secret, or null when the pass phrase is wrong (or the wrapped key damaged).</summary>
    public static byte[]? Unlock(WrappedKey wrapped, string passPhrase, ReadOnlySpan<byte> context)
    {
        try
        {
            return new SecretBox(Derive(passPhrase, wrapped.Salt, wrapped.Iterations)).Open(Magic, wrapped.Sealed, context);
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    // Normalized, so the same pass phrase typed with another keyboard layout or input method matches.
    private static byte[] Derive(string passPhrase, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(passPhrase.Normalize(NormalizationForm.FormC)),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            SecretBox.KeySize);
}
