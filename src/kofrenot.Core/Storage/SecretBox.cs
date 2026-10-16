using System.Security.Cryptography;

namespace Kofrenot.Core.Storage;

/// <summary>
/// Encrypts and authenticates data with AES-256-GCM, as <c>magic · nonce · ciphertext · tag</c>.
/// The 4-byte magic says what the data is ("KFC1": a change, ...). <c>associated</c> binds the data
/// to its place (a file's id): moved elsewhere, it no longer opens.
/// </summary>
public sealed class SecretBox
{
    public const int KeySize = 32;
    public const int MagicSize = 4;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int Overhead = MagicSize + NonceSize + TagSize;

    private readonly byte[] _key;

    public SecretBox(byte[] key)
    {
        if (key.Length != KeySize)
        {
            throw new ArgumentException($"The key must be {KeySize} bytes", nameof(key));
        }

        _key = key;
    }

    public byte[] Seal(ReadOnlySpan<byte> magic, ReadOnlySpan<byte> plain, ReadOnlySpan<byte> associated)
    {
        CheckMagic(magic);
        var output = new byte[Overhead + plain.Length];
        var parts = new Parts(output);
        magic.CopyTo(parts.Magic);
        RandomNumberGenerator.Fill(parts.Nonce);

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(parts.Nonce, plain, parts.Cipher, parts.Tag, Associated(magic, associated));
        return output;
    }

    /// <exception cref="InvalidDataException">Not this kind of data, damaged, or another key or place.</exception>
    public byte[] Open(ReadOnlySpan<byte> magic, byte[] data, ReadOnlySpan<byte> associated)
    {
        CheckMagic(magic);
        if (data.Length < Overhead || !data.AsSpan(0, MagicSize).SequenceEqual(magic))
        {
            throw new InvalidDataException("Not the expected kind of kofrenot data");
        }

        var parts = new Parts(data);
        var plain = new byte[parts.Cipher.Length];
        try
        {
            using var aes = new AesGcm(_key, TagSize);
            aes.Decrypt(parts.Nonce, parts.Cipher, parts.Tag, plain, Associated(magic, associated));
        }
        catch (AuthenticationTagMismatchException ex)
        {
            throw new InvalidDataException("The data cannot be decrypted: damaged, or another key", ex);
        }

        return plain;
    }

    private static void CheckMagic(ReadOnlySpan<byte> magic)
    {
        if (magic.Length != MagicSize)
        {
            throw new ArgumentException($"The magic must be {MagicSize} bytes", nameof(magic));
        }
    }

    // The magic is authenticated too: data cannot be passed off as another kind.
    private static byte[] Associated(ReadOnlySpan<byte> magic, ReadOnlySpan<byte> associated)
    {
        var all = new byte[magic.Length + associated.Length];
        magic.CopyTo(all);
        associated.CopyTo(all.AsSpan(magic.Length));
        return all;
    }

    private readonly ref struct Parts(byte[] data)
    {
        public Span<byte> Magic { get; } = data.AsSpan(0, MagicSize);

        public Span<byte> Nonce { get; } = data.AsSpan(MagicSize, NonceSize);

        public Span<byte> Cipher { get; } = data.AsSpan(MagicSize + NonceSize, data.Length - Overhead);

        public Span<byte> Tag { get; } = data.AsSpan(data.Length - TagSize);
    }
}
