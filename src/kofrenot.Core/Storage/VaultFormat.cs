namespace Kofrenot.Core.Storage;

/// <summary>
/// The format of a vault's files (header and change files): one number, raised by every breaking
/// change. A kofrenot reads the formats up to <see cref="Current"/> included; a newer one asks
/// the user to upgrade kofrenot.
/// </summary>
public static class VaultFormat
{
    /// <summary>The newest format this version reads, and the one it writes.</summary>
    public const int Current = 5;

    // The first kofrenot writing each format, from format 1.
    private static readonly string[] FirstVersions = ["0.1", "0.2", "0.3", "0.4", "0.5"];

    /// <summary>The first kofrenot writing <paramref name="format"/> ("0.4").</summary>
    public static string Since(int format) =>
        format is >= 1 and <= Current ? FirstVersions[format - 1] : throw new ArgumentOutOfRangeException(nameof(format), format, "Not a known vault format");

    /// <summary>What the user is asked before a vault of <paramref name="format"/> is upgraded to <see cref="Current"/> (<see cref="Vault.NeedsUpgrade"/>).</summary>
    public static string UpgradeQuestion(int format) =>
        $"This vault is in format {format}, written by kofrenot {Since(format)}. This kofrenot writes format {Current}:"
        + $" once it changes the vault, kofrenot older than {Since(Current)} no longer opens it."
        + "\n\nUpdate kofrenot on your other devices first."
        + "\n\nUpgrade the vault and open it? Until then, this kofrenot does not use it.";

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
