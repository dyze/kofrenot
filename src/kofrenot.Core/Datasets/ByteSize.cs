using System.Globalization;

namespace Kofrenot.Core.Datasets;

/// <summary>A size in bytes as shown: "512 bytes", "1.2 KB", "3.4 MB" (units of 1024).</summary>
public static class ByteSize
{
    private static readonly string[] Units = ["KB", "MB", "GB", "TB"];

    /// <summary>How much of <paramref name="total"/> bytes is <paramref name="done"/>, from 0 to 100 (100 for nothing to do).</summary>
    public static int Percent(long done, long total) =>
        total <= 0 ? 100 : (int)Math.Clamp(done * 100 / total, 0, 100);

    public static string Format(long bytes, IFormatProvider? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        if (bytes < 1024)
        {
            return bytes == 1 ? "1 byte" : string.Create(culture, $"{bytes} bytes");
        }

        double size = bytes;
        var unit = -1;
        // 1023.95 would show as "1024.0": the next unit then.
        while (size >= 1023.95 && unit < Units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return string.Create(culture, $"{size:0.0} {Units[unit]}");
    }
}
