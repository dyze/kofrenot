using System.Security.Cryptography;

namespace Kofrenot.Core.Storage;

/// <summary>
/// 32 random bytes that open an identity when its pass phrase is lost, shown once to the user as
/// 52 Base32 characters in groups of 4 (<c>ABCD-EFGH-…</c>). Never stored, never logged.
/// </summary>
public sealed class RecoveryKey
{
    public const int Size = 32;

    private RecoveryKey(byte[] bytes) => Bytes = bytes;

    internal byte[] Bytes { get; }

    public static RecoveryKey New() => new(RandomNumberGenerator.GetBytes(Size));

    /// <summary>
    /// The key typed by the user, or null when it is not one. Case, spaces and dashes do not
    /// matter, and digits read for letters (0, 1, 8 for O, I, B) are taken as those.
    /// </summary>
    public static RecoveryKey? Parse(string text) => Base32.Decode(text, Size) is { } bytes ? new RecoveryKey(bytes) : null;

    public override string ToString() => Base32.Encode(Bytes);
}
