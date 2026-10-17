using System.Security.Cryptography;
using System.Text;

namespace Kofrenot.Core.Storage;

/// <summary>
/// The master key of a vault, encrypted with a key derived from the pass phrase
/// (PBKDF2-SHA256), as stored in the header. Nothing secret.
/// </summary>
public sealed record WrappedKey(byte[] Salt, int Iterations, byte[] Sealed);

/// <summary>
/// Creates a vault's master key and unlocks it with the pass phrase. Changing the pass phrase
/// only wraps the same master key again, so the change files stay as they are.
/// </summary>
public static class KeyWrap
{
    public const int DefaultIterations = 600_000;
    private const int SaltSize = 16;
    private static ReadOnlySpan<byte> Magic => "KFK1"u8;

    /// <summary>A new random master key, and its wrapped form for the header.</summary>
    /// <param name="context">What the key belongs to (the vault id): the wrapped key opens only there.</param>
    public static (WrappedKey Wrapped, byte[] MasterKey) Create(string passPhrase, ReadOnlySpan<byte> context, int iterations = DefaultIterations)
    {
        if (string.IsNullOrEmpty(passPhrase))
        {
            throw new ArgumentException("The pass phrase is empty", nameof(passPhrase));
        }

        var masterKey = RandomNumberGenerator.GetBytes(SecretBox.KeySize);
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var sealedKey = new SecretBox(Derive(passPhrase, salt, iterations)).Seal(Magic, masterKey, context);
        return (new WrappedKey(salt, iterations, sealedKey), masterKey);
    }

    /// <summary>The master key, or null when the pass phrase is wrong (or the wrapped key damaged).</summary>
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
