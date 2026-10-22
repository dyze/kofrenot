using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Kofrenot.Core.Storage;

/// <summary>
/// Wraps a vault key of one epoch for an identity: ECDH P-256 between a one-time key pair and the
/// identity's public key, HKDF-SHA256, then AES-256-GCM, signed by who wrote it (<see cref="Signing"/>):
/// <c>"KFW2" · one-time public key (uncompressed point) · nonce · vault key · tag · signature</c>,
/// bound to the vault, the epoch and the identity. <c>"KFW1"</c> (unsigned) is still read where no
/// signature is asked.
/// </summary>
public static class EpochKeys
{
    private const int PointSize = 65;
    private const int CoordinateSize = 32;
    private static ReadOnlySpan<byte> Magic => "KFW2"u8;
    private static ReadOnlySpan<byte> Unsigned => "KFW1"u8;
    private static ReadOnlySpan<byte> Info => "kofrenot vault key"u8;

    /// <summary>The vault key of <paramref name="epoch"/> wrapped for an identity, signed by <paramref name="signer"/>.</summary>
    public static byte[] Wrap(byte[] vaultKey, byte[] identityPublicKey, Guid vault, int epoch, Guid identity, UnlockedIdentity signer)
    {
        using var recipient = ECDiffieHellman.Create();
        recipient.ImportSubjectPublicKeyInfo(identityPublicKey, out _);
        using var oneTime = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var point = PointOf(oneTime.ExportParameters(includePrivateParameters: false));

        var header = new byte[Magic.Length + PointSize];
        Magic.CopyTo(header);
        point.CopyTo(header, Magic.Length);
        var key = BoxKey(oneTime.DeriveRawSecretAgreement(recipient.PublicKey), point);
        var associated = Associated(vault, epoch, identity);
        var body = new SecretBox(key).Seal(header, vaultKey, associated);
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

        if (body.Length < Magic.Length + PointSize)
        {
            throw new InvalidDataException("Not a wrapped vault key");
        }

        var header = body[..(Magic.Length + PointSize)].ToArray();
        var point = header[Magic.Length..];
        byte[] secret;
        try
        {
            using var oneTime = ECDiffieHellman.Create(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint { X = point[1..(1 + CoordinateSize)], Y = point[(1 + CoordinateSize)..] },
            });
            using var own = ECDiffieHellman.Create();
            own.ImportPkcs8PrivateKey(identity.PrivateKey, out _);
            secret = own.DeriveRawSecretAgreement(oneTime.PublicKey);
        }
        // Windows refuses a point off the curve as not supported.
        catch (Exception ex) when (ex is CryptographicException or PlatformNotSupportedException)
        {
            throw new InvalidDataException("The wrapped vault key is damaged", ex);
        }

        return new SecretBox(BoxKey(secret, point)).Open(header, body.ToArray(), associated);
    }

    private static byte[] PointOf(ECParameters parameters) => [0x04, .. parameters.Q.X!, .. parameters.Q.Y!];

    private static byte[] BoxKey(byte[] secret, byte[] point) =>
        HKDF.DeriveKey(HashAlgorithmName.SHA256, secret, SecretBox.KeySize, point, Info.ToArray());

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
