using System.Security.Cryptography;

namespace Kofrenot.Core.Storage;

/// <summary>
/// Seals data for an identity, knowing only its public key: ECDH P-256 between a one-time key pair
/// and that key, HKDF-SHA256 (salt: the one-time public key), then AES-256-GCM:
/// <c>magic · one-time public key (uncompressed point) · nonce · ciphertext · tag</c>. Only the
/// identity's private key opens it.
/// </summary>
internal static class PublicKeyBox
{
    private const int PointSize = 65;
    private const int CoordinateSize = 32;

    public static byte[] Seal(ReadOnlySpan<byte> magic, ReadOnlySpan<byte> info, byte[] publicKey, ReadOnlySpan<byte> plain, ReadOnlySpan<byte> associated)
    {
        using var recipient = ECDiffieHellman.Create();
        recipient.ImportSubjectPublicKeyInfo(publicKey, out _);
        using var oneTime = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var parameters = oneTime.ExportParameters(includePrivateParameters: false);
        byte[] point = [0x04, .. parameters.Q.X!, .. parameters.Q.Y!];

        byte[] header = [.. magic, .. point];
        var key = BoxKey(oneTime.DeriveRawSecretAgreement(recipient.PublicKey), point, info);
        return new SecretBox(key).Seal(header, plain, associated);
    }

    /// <exception cref="InvalidDataException">Not data starting with <paramref name="magic"/>, damaged, or sealed for another identity or place.</exception>
    public static byte[] Open(ReadOnlySpan<byte> magic, ReadOnlySpan<byte> info, UnlockedIdentity identity, byte[] data, ReadOnlySpan<byte> associated)
    {
        if (data.Length < magic.Length + PointSize || !data.AsSpan().StartsWith(magic))
        {
            throw new InvalidDataException("Not the expected kind of kofrenot data");
        }

        var header = data[..(magic.Length + PointSize)];
        var point = header[magic.Length..];
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
            throw new InvalidDataException("The sealed data is damaged", ex);
        }

        return new SecretBox(BoxKey(secret, point, info)).Open(header, data, associated);
    }

    private static byte[] BoxKey(byte[] secret, byte[] point, ReadOnlySpan<byte> info) =>
        HKDF.DeriveKey(HashAlgorithmName.SHA256, secret, SecretBox.KeySize, point, info.ToArray());
}
