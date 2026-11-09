using System.Buffers.Binary;

namespace Kofrenot.Core.Storage;

/// <summary>
/// Wraps a vault key of one epoch for an identity (<see cref="PublicKeyBox"/>), signed by who wrote it (<see cref="Signing"/>):
/// <c>"KFW2" · one-time public key (uncompressed point) · nonce · vault key · tag · signature</c>,
/// bound to the vault, the epoch and the identity. <c>"KFW1"</c> (unsigned) is still read where no
/// signature is asked.
/// </summary>
public static class EpochKeys
{
    private const int PointSize = 65;
    private static ReadOnlySpan<byte> Magic => "KFW2"u8;
    private static ReadOnlySpan<byte> Unsigned => "KFW1"u8;
    private static ReadOnlySpan<byte> Info => "kofrenot vault key"u8;

    /// <summary>The vault key of <paramref name="epoch"/> wrapped for an identity, signed by <paramref name="signer"/>.</summary>
    public static byte[] Wrap(byte[] vaultKey, byte[] identityPublicKey, Guid vault, int epoch, Guid identity, UnlockedIdentity signer)
    {
        var associated = Associated(vault, epoch, identity);
        var body = PublicKeyBox.Seal(Magic, Info, identityPublicKey, vaultKey, associated);
        return [.. body, .. Signing.Sign(signer, [.. associated, .. body])];
    }

    /// <summary>Whether <paramref name="wrapped"/> is a key of this vault, epoch and identity signed by <paramref name="signedBy"/>; no key needed to tell.</summary>
    public static bool IsSignedBy(byte[] wrapped, Guid vault, int epoch, Guid identity, byte[] signedBy) =>
        wrapped.AsSpan().StartsWith(Magic) && wrapped.Length > Magic.Length + PointSize + Signing.SignatureSize
        && Signing.Verify(signedBy, [.. Associated(vault, epoch, identity), .. wrapped.AsSpan(..^Signing.SignatureSize)], wrapped.AsSpan(^Signing.SignatureSize..));

    /// <summary>The vault key; when <paramref name="signedBy"/> is given, only from a wrap that key signed.</summary>
    /// <exception cref="InvalidDataException">Damaged, wrapped for another identity, vault or epoch, or not signed as asked.</exception>
    public static byte[] Unwrap(byte[] wrapped, UnlockedIdentity identity, Guid vault, int epoch, byte[]? signedBy = null)
    {
        var associated = Associated(vault, epoch, identity.Id);
        var body = wrapped.AsSpan();
        if (body.StartsWith(Magic) && body.Length > Signing.SignatureSize)
        {
            var signature = body[^Signing.SignatureSize..];
            body = body[..^Signing.SignatureSize];
            if (signedBy is not null && !Signing.Verify(signedBy, [.. associated, .. body], signature))
            {
                throw new InvalidDataException("The wrapped vault key is not signed by the vault's owner");
            }
        }
        else if (!body.StartsWith(Unsigned) || signedBy is not null)
        {
            throw new InvalidDataException(signedBy is not null ? "The wrapped vault key is not signed" : "Not a wrapped vault key");
        }

        return PublicKeyBox.Open(body[..Magic.Length], Info, identity, body.ToArray(), associated);
    }

    // vault · epoch (4 bytes, big-endian) · identity
    private static byte[] Associated(Guid vault, int epoch, Guid identity)
    {
        var associated = new byte[16 + sizeof(int) + 16];
        vault.ToByteArray().CopyTo(associated, 0);
        BinaryPrimitives.WriteInt32BigEndian(associated.AsSpan(16), epoch);
        identity.ToByteArray().CopyTo(associated, 16 + sizeof(int));
        return associated;
    }
}
