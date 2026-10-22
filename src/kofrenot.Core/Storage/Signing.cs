using System.Numerics;
using System.Security.Cryptography;

namespace Kofrenot.Core.Storage;

/// <summary>
/// An identity's signing key (ECDSA P-256), derived from its private key: identities need no
/// second secret, and its owner signs what only it may write in a shared vault (key epochs,
/// members). Signatures are 64 bytes (r · s).
/// </summary>
public static class Signing
{
    public const int SignatureSize = 64;

    private static readonly BigInteger Order = BigInteger.Parse("0FFFFFFFF00000000FFFFFFFFFFFFFFFFBCE6FAADA7179E84F3B9CAC2FC632551", System.Globalization.NumberStyles.HexNumber);

    /// <summary>The public signing key of <paramref name="identity"/> (SubjectPublicKeyInfo).</summary>
    public static byte[] PublicKeyOf(UnlockedIdentity identity)
    {
        using var key = KeyOf(identity);
        return key.ExportSubjectPublicKeyInfo();
    }

    public static byte[] Sign(UnlockedIdentity identity, ReadOnlySpan<byte> data)
    {
        using var key = KeyOf(identity);
        return key.SignData(data, HashAlgorithmName.SHA256);
    }

    /// <summary>Whether <paramref name="signature"/> is the signature of <paramref name="data"/> by <paramref name="publicKey"/>; false for a damaged key or signature.</summary>
    public static bool Verify(byte[] publicKey, ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature)
    {
        try
        {
            using var key = ECDsa.Create();
            key.ImportSubjectPublicKeyInfo(publicKey, out _);
            return signature.Length == SignatureSize && key.VerifyData(data, signature, HashAlgorithmName.SHA256);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    // A scalar in [1, n): HKDF of the private key, counted up in the rare case it falls out of range.
    private static ECDsa KeyOf(UnlockedIdentity identity)
    {
        for (byte counter = 0; ; counter++)
        {
            var scalar = HKDF.DeriveKey(HashAlgorithmName.SHA256, identity.PrivateKey, 32, identity.Id.ToByteArray(), [.. "kofrenot signing key "u8, counter]);
            var value = new BigInteger(scalar, isUnsigned: true, isBigEndian: true);
            if (value > 0 && value < Order)
            {
                return ECDsa.Create(new ECParameters { Curve = ECCurve.NamedCurves.nistP256, D = scalar });
            }
        }
    }
}
