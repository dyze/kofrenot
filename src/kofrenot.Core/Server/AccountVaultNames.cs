using System.Buffers.Binary;
using System.Text;
using Kofrenot.Core.Storage;

namespace Kofrenot.Core.Server;

/// <summary>
/// A server vault's name as kept in its account's data, so a vault not added yet shows by its name.
/// Sealed for the account's identity (<see cref="PublicKeyBox"/>): any device adds one with its
/// public key only, only its private key reads it. Bound to its store and identity, and padded to
/// <see cref="Block"/> bytes so its length does not show:
/// <c>"KFV1" · one-time public key · nonce · (length, 2 bytes big-endian · UTF-8 · zeros) · tag</c>.
/// </summary>
public static class AccountVaultNames
{
    public const int Block = 256;
    private const int LengthSize = sizeof(ushort);
    private static ReadOnlySpan<byte> Magic => "KFV1"u8;
    private static ReadOnlySpan<byte> Info => "kofrenot vault name"u8;

    public static byte[] Seal(string name, Identity identity, Guid store)
    {
        var text = Encoding.UTF8.GetBytes(name);
        if (text.Length > ushort.MaxValue)
        {
            throw new ArgumentException("The name is too long", nameof(name));
        }

        var plain = new byte[(LengthSize + text.Length + Block - 1) / Block * Block];
        BinaryPrimitives.WriteUInt16BigEndian(plain, (ushort)text.Length);
        text.CopyTo(plain, LengthSize);
        return PublicKeyBox.Seal(Magic, Info, identity.PublicKey, plain, Associated(store, identity.Id));
    }

    /// <summary>The name; null when it is damaged, or sealed for another store or identity.</summary>
    public static string? Open(byte[] sealedName, UnlockedIdentity identity, Guid store)
    {
        try
        {
            var plain = PublicKeyBox.Open(Magic, Info, identity, sealedName, Associated(store, identity.Id));
            return Encoding.UTF8.GetString(plain, LengthSize, BinaryPrimitives.ReadUInt16BigEndian(plain));
        }
        // A length beyond the data: written by a faulty kofrenot.
        catch (Exception ex) when (ex is InvalidDataException or ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    // store · identity
    private static byte[] Associated(Guid store, Guid identity) => [.. store.ToByteArray(), .. identity.ToByteArray()];
}
