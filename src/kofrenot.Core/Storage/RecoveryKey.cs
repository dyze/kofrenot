using System.Security.Cryptography;
using System.Text;

namespace Kofrenot.Core.Storage;

/// <summary>
/// 32 random bytes that open an identity when its pass phrase is lost, shown once to the user as
/// 52 Base32 characters in groups of 4 (<c>ABCD-EFGH-…</c>). Never stored, never logged.
/// </summary>
public sealed class RecoveryKey
{
    public const int Size = 32;
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    private const int Length = (Size * 8 + 4) / 5;
    private const int GroupSize = 4;

    private RecoveryKey(byte[] bytes) => Bytes = bytes;

    internal byte[] Bytes { get; }

    public static RecoveryKey New() => new(RandomNumberGenerator.GetBytes(Size));

    /// <summary>
    /// The key typed by the user, or null when it is not one. Case, spaces and dashes do not
    /// matter, and digits read for letters (0, 1, 8 for O, I, B) are taken as those.
    /// </summary>
    public static RecoveryKey? Parse(string text)
    {
        var characters = text.ToUpperInvariant()
            .Where(c => !char.IsWhiteSpace(c) && c != '-')
            .Select(c => c switch { '0' => 'O', '1' => 'I', '8' => 'B', _ => c })
            .ToArray();
        if (characters.Length != Length)
        {
            return null;
        }

        var bytes = new byte[Size];
        int buffer = 0, bits = 0, index = 0;
        foreach (var value in characters.Select(c => Alphabet.IndexOf(c)))
        {
            if (value < 0)
            {
                return null;
            }

            (buffer, bits) = ((buffer << 5) | value, bits + 5);
            if (bits >= 8)
            {
                bits -= 8;
                bytes[index++] = (byte)(buffer >> bits);
            }

            buffer &= (1 << bits) - 1;
        }

        // The last character carries one bit: the rest must be zero, so each key has one spelling.
        return buffer == 0 ? new RecoveryKey(bytes) : null;
    }

    public override string ToString()
    {
        var text = new StringBuilder(Length + Length / GroupSize);
        int buffer = 0, bits = 0;
        foreach (var value in Bytes)
        {
            (buffer, bits) = ((buffer << 8) | value, bits + 8);
            while (bits >= 5)
            {
                bits -= 5;
                Append(text, buffer >> bits);
            }

            buffer &= (1 << bits) - 1;
        }

        Append(text, buffer << (5 - bits));
        return text.ToString();
    }

    private static void Append(StringBuilder text, int value)
    {
        if (text.Length > 0 && (text.Length + 1) % (GroupSize + 1) == 0)
        {
            text.Append('-');
        }

        text.Append(Alphabet[value & 31]);
    }
}
