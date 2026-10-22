using System.Text;

namespace Kofrenot.Core.Storage;

/// <summary>
/// Bytes as Base32 (RFC 4648, no padding) in groups of 4 (<c>ABCD-EFGH-…</c>): easy to read out
/// and type. Reading ignores case, spaces and dashes, and takes 0, 1 and 8 for O, I and B.
/// </summary>
internal static class Base32
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    private const int GroupSize = 4;

    public static string Encode(ReadOnlySpan<byte> bytes)
    {
        var text = new StringBuilder();
        int buffer = 0, bits = 0;
        foreach (var value in bytes)
        {
            (buffer, bits) = ((buffer << 8) | value, bits + 8);
            while (bits >= 5)
            {
                bits -= 5;
                Append(text, buffer >> bits);
            }

            buffer &= (1 << bits) - 1;
        }

        if (bits > 0)
        {
            Append(text, buffer << (5 - bits));
        }

        return text.ToString();
    }

    /// <summary><paramref name="size"/> bytes read from <paramref name="text"/>; null when it is not that.</summary>
    public static byte[]? Decode(string text, int size)
    {
        var characters = text.ToUpperInvariant()
            .Where(c => !char.IsWhiteSpace(c) && c != '-')
            .Select(c => c switch { '0' => 'O', '1' => 'I', '8' => 'B', _ => c })
            .ToArray();
        if (characters.Length != (size * 8 + 4) / 5)
        {
            return null;
        }

        var bytes = new byte[size];
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

        // Bits left over must be zero, so the bytes have one spelling.
        return buffer == 0 ? bytes : null;
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
