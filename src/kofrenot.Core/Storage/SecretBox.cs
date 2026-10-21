using System.Security.Cryptography;

namespace Kofrenot.Core.Storage;

/// <summary>
/// Encrypts and authenticates data with AES-256-GCM, as <c>header · nonce · ciphertext · tag</c>.
/// The header starts with a 4-byte magic saying what the data is ("KFC1": a change, ...), and may
/// carry more plain bytes (a key epoch); it is authenticated. <c>associated</c> binds the data to
/// its place (a file's id): moved elsewhere, it no longer opens.
/// </summary>
public sealed class SecretBox
{
    public const int KeySize = 32;
    public const int MagicSize = 4;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key;

    public SecretBox(byte[] key)
    {
        if (key.Length != KeySize)
        {
            throw new ArgumentException($"The key must be {KeySize} bytes", nameof(key));
        }

        _key = key;
    }

    public byte[] Seal(ReadOnlySpan<byte> header, ReadOnlySpan<byte> plain, ReadOnlySpan<byte> associated)
    {
        CheckHeader(header);
        var output = new byte[header.Length + NonceSize + plain.Length + TagSize];
        var parts = new Parts(output, header.Length);
        header.CopyTo(output);
        RandomNumberGenerator.Fill(parts.Nonce);

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(parts.Nonce, plain, parts.Cipher, parts.Tag, Associated(header, associated));
        return output;
    }

    /// <exception cref="InvalidDataException">Not data with this header, damaged, or another key or place.</exception>
    public byte[] Open(ReadOnlySpan<byte> header, byte[] data, ReadOnlySpan<byte> associated)
    {
        CheckHeader(header);
        if (data.Length < header.Length + NonceSize + TagSize || !data.AsSpan(0, header.Length).SequenceEqual(header))
        {
            throw new InvalidDataException("Not the expected kind of kofrenot data");
        }

        var parts = new Parts(data, header.Length);
        var plain = new byte[parts.Cipher.Length];
        try
        {
            using var aes = new AesGcm(_key, TagSize);
            aes.Decrypt(parts.Nonce, parts.Cipher, parts.Tag, plain, Associated(header, associated));
        }
        catch (AuthenticationTagMismatchException ex)
        {
            throw new InvalidDataException("The data cannot be decrypted: damaged, or another key", ex);
        }

        return plain;
    }

    internal static void CheckHeader(ReadOnlySpan<byte> header)
    {
        if (header.Length < MagicSize)
        {
            throw new ArgumentException($"The header must start with a {MagicSize}-byte magic", nameof(header));
        }
    }

    // The header is authenticated too: data cannot be passed off as another kind, or another epoch.
    private static byte[] Associated(ReadOnlySpan<byte> header, ReadOnlySpan<byte> associated)
    {
        var all = new byte[header.Length + associated.Length];
        header.CopyTo(all);
        associated.CopyTo(all.AsSpan(header.Length));
        return all;
    }

    private readonly ref struct Parts(byte[] data, int headerSize)
    {
        public Span<byte> Nonce { get; } = data.AsSpan(headerSize, NonceSize);

        public Span<byte> Cipher { get; } = data.AsSpan(headerSize + NonceSize, data.Length - headerSize - NonceSize - TagSize);

        public Span<byte> Tag { get; } = data.AsSpan(data.Length - TagSize);
    }
}
