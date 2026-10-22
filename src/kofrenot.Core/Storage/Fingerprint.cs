using System.Security.Cryptography;

namespace Kofrenot.Core.Storage;

/// <summary>
/// What two people compare, out of band, before one shares a vault with the other: the first
/// 160 bits of the SHA-256 of an identity's public keys (encryption, then signing when it has one),
/// as 32 Base32 characters in 8 groups of 4. A server handing out another key shows another
/// fingerprint.
/// </summary>
public static class Fingerprint
{
    private const int Bytes = 20;

    public static string Of(byte[] publicKey, byte[]? signingKey = null) =>
        Base32.Encode(SHA256.HashData([.. publicKey, .. signingKey ?? []]).AsSpan(0, Bytes));
}
