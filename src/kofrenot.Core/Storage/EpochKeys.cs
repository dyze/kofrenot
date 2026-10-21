using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Kofrenot.Core.Storage;

/// <summary>
/// Wraps a vault key of one epoch for an identity: ECDH P-256 between a one-time key pair and the
/// identity's public key, HKDF-SHA256, then AES-256-GCM. <c>"KFW1" · one-time public key
/// (uncompressed point) · nonce · vault key · tag</c>, bound to the vault, the epoch and the identity.
/// </summary>
public static class EpochKeys
{
    private const int PointSize = 65;
    private const int CoordinateSize = 32;
    private static ReadOnlySpan<byte> Magic => "KFW1"u8;
    private static ReadOnlySpan<byte> Info => "kofrenot vault key"u8;

    public static byte[] Wrap(byte[] vaultKey, byte[] identityPublicKey, Guid vault, int epoch, Guid identity)
    {
        using var recipient = ECDiffieHellman.Create();
        recipient.ImportSubjectPublicKeyInfo(identityPublicKey, out _);
        using var oneTime = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var point = PointOf(oneTime.ExportParameters(includePrivateParameters: false));

        var header = new byte[Magic.Length + PointSize];
        Magic.CopyTo(header);
        point.CopyTo(header, Magic.Length);
        var key = BoxKey(oneTime.DeriveRawSecretAgreement(recipient.PublicKey), point);
        return new SecretBox(key).Seal(header, vaultKey, Associated(vault, epoch, identity));
    }

    /// <exception cref="InvalidDataException">Damaged, or wrapped for another identity, vault or epoch.</exception>
    public static byte[] Unwrap(byte[] wrapped, UnlockedIdentity identity, Guid vault, int epoch)
    {
        if (wrapped.Length < Magic.Length + PointSize || !wrapped.AsSpan().StartsWith(Magic))
        {
            throw new InvalidDataException("Not a wrapped vault key");
        }

        var header = wrapped.AsSpan(0, Magic.Length + PointSize);
        var point = header[Magic.Length..].ToArray();
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

        return new SecretBox(BoxKey(secret, point)).Open(header, wrapped, Associated(vault, epoch, identity.Id));
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
