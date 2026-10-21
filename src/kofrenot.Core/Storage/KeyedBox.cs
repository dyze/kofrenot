using System.Buffers.Binary;
using System.Text;

namespace Kofrenot.Core.Storage;

/// <summary>
/// A kind of sealed file, by its magic: since format 6 followed by the epoch of its key
/// (<c>"KFC2" · epoch</c>, 4 bytes big-endian); before, without one (<c>"KFC1"</c>), read with epoch 0.
/// </summary>
public sealed class SealKind
{
    public const int HeaderSize = SecretBox.MagicSize + sizeof(uint);

    public static readonly SealKind Change = new("KFC1", "KFC2");
    public static readonly SealKind Note = new("KFN1", "KFN2");
    public static readonly SealKind Thumbnail = new("KFT1", "KFT2");
    public static readonly SealKind Dataset = new("KFD1", "KFD2");
    public static readonly SealKind Snapshot = new("KFS1", "KFS2");

    private readonly byte[] _legacy;
    private readonly byte[] _magic;

    private SealKind(string legacy, string magic) => (_legacy, _magic) = (Encoding.ASCII.GetBytes(legacy), Encoding.ASCII.GetBytes(magic));

    internal byte[] HeaderOf(int epoch)
    {
        var header = new byte[HeaderSize];
        _magic.CopyTo(header, 0);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(SecretBox.MagicSize), checked((uint)epoch));
        return header;
    }

    /// <summary>The header <paramref name="start"/> begins with, and the epoch of its key.</summary>
    /// <exception cref="InvalidDataException">Not this kind of file.</exception>
    internal (byte[] Header, int Epoch) Read(ReadOnlySpan<byte> start)
    {
        if (start.StartsWith(_legacy))
        {
            return (_legacy, 0);
        }

        if (start.Length >= HeaderSize && start.StartsWith(_magic)
            && BinaryPrimitives.ReadUInt32BigEndian(start[SecretBox.MagicSize..]) is var epoch and <= int.MaxValue)
        {
            return (start[..HeaderSize].ToArray(), (int)epoch);
        }

        throw new InvalidDataException("Not the expected kind of kofrenot data");
    }
}

/// <summary>
/// Seals a vault's files with the key of the current epoch (<see cref="KeyRing"/>), and opens
/// them with the key of theirs: small ones whole (<see cref="SecretBox"/>), datasets as streams
/// (<see cref="ChunkedBox"/>).
/// </summary>
public sealed class KeyedBox(KeyRing keys)
{
    public byte[] Seal(SealKind kind, ReadOnlySpan<byte> plain, ReadOnlySpan<byte> associated) =>
        new SecretBox(keys.CurrentKey).Seal(kind.HeaderOf(keys.CurrentEpoch), plain, associated);

    /// <exception cref="InvalidDataException">Not this kind of file, damaged, another key or place, or an epoch whose key is missing.</exception>
    public byte[] Open(SealKind kind, byte[] data, ReadOnlySpan<byte> associated)
    {
        var (header, epoch) = kind.Read(data);
        return new SecretBox(keys.KeyOf(epoch)).Open(header, data, associated);
    }

    /// <summary>Reads <paramref name="plain"/> to its end and writes it sealed to <paramref name="output"/>.</summary>
    public void Seal(SealKind kind, Stream plain, Stream output, ReadOnlySpan<byte> associated) =>
        new ChunkedBox(keys.CurrentKey).Seal(kind.HeaderOf(keys.CurrentEpoch), plain, output, associated);

    /// <summary>A stream reading <paramref name="sealedData"/> (seekable) decrypted. It owns <paramref name="sealedData"/>.</summary>
    /// <exception cref="InvalidDataException">As <see cref="Open(SealKind, byte[], ReadOnlySpan{byte})"/>; while reading, also cut off.</exception>
    public Stream Open(SealKind kind, Stream sealedData, ReadOnlySpan<byte> associated)
    {
        byte[] header;
        byte[] key;
        try
        {
            var start = new byte[SealKind.HeaderSize];
            var read = sealedData.ReadAtLeast(start, start.Length, throwOnEndOfStream: false);
            (header, var epoch) = kind.Read(start.AsSpan(0, read));
            key = keys.KeyOf(epoch);
            sealedData.Position = 0;
        }
        catch
        {
            sealedData.Dispose();
            throw;
        }

        return new ChunkedBox(key).Open(header, sealedData, associated);
    }
}
