namespace Kofrenot.Core.Storage;

/// <summary>
/// The format of a vault's files (header and change files): one number, raised by every breaking
/// change. A kofrenot reads the formats up to <see cref="Current"/> included; a newer one asks
/// the user to upgrade kofrenot.
/// </summary>
public static class VaultFormat
{
    /// <summary>The newest format this version reads, and the one it writes.</summary>
    public const int Current = 1;

    /// <exception cref="NewerVaultFormatException"><paramref name="format"/> is newer than <see cref="Current"/>.</exception>
    public static void EnsureSupported(int format)
    {
        if (format > Current)
        {
            throw new NewerVaultFormatException(format);
        }
    }
}

/// <summary>The vault was written by a newer kofrenot: this one must be upgraded to use it.</summary>
public sealed class NewerVaultFormatException(int format)
    : NotSupportedException($"The vault has format {format}, written by a newer kofrenot: this one reads up to format {VaultFormat.Current}")
{
    public int Format { get; } = format;
}
