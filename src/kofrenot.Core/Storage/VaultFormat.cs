namespace Kofrenot.Core.Storage;

/// <summary>
/// The format of a vault's files (header and change files): one number, raised by every breaking
/// change. A kofrenot reads the formats up to <see cref="Current"/> included. A newer file also
/// says which older formats may still use it: <c>oldestReader</c>, the oldest that can read it, skipping
/// what it does not know; <c>oldestWriter</c>, the oldest that can write to the vault (both the file's
/// format when missing). Newer than both: read only; newer than <c>oldestReader</c>: not opened, and the
/// user is asked to upgrade kofrenot.
/// </summary>
public static class VaultFormat
{
    /// <summary>The newest format this version reads, and the one it writes.</summary>
    public const int Current = 11;

    /// <summary>The oldest format that reads what this one writes, skipping what it does not know (<c>oldestReader</c>).</summary>
    public const int OldestReader = 10;

    /// <summary>The oldest format that may write to a vault this one wrote to (<c>oldestWriter</c>).</summary>
    public const int OldestWriter = 11;

    // The first format whose kofrenot reads oldestReader and oldestWriter: from 0.15, not 0.14.
    private const int FirstLevels = 10;
    private const string FirstLevelsVersion = "0.15";

    // The first kofrenot writing each format, from format 1.
    private static readonly string[] FirstVersions = ["0.1", "0.2", "0.3", "0.4", "0.5", "0.7", "0.8", "0.11", "0.13", "0.14", "0.16"];

    /// <summary>The first kofrenot writing <paramref name="format"/> ("0.4").</summary>
    public static string Since(int format) =>
        format is >= 1 and <= Current ? FirstVersions[format - 1] : throw new ArgumentOutOfRangeException(nameof(format), format, "Not a known vault format");

    /// <summary>What the user is asked before a vault of <paramref name="format"/> is upgraded to <see cref="Current"/> (<see cref="Vault.NeedsUpgrade"/>).</summary>
    public static string UpgradeQuestion(int format) =>
        $"This vault is in format {format}, written by kofrenot {Since(format)}. This kofrenot writes format {Current}:"
        + $" once it changes the vault, {WhatOlderKofrenotDo}."
        + "\n\nUpdate kofrenot on your other devices first."
        + "\n\nUpgrade the vault and open it? Until then, this kofrenot does not use it.";

    /// <summary>Why a vault of <paramref name="format"/>, newer than <see cref="Current"/>, is not opened.</summary>
    public static string NewerMessage(int format) =>
        $"This vault was written by a newer kofrenot (vault format {format}; this one reads up to format {Current}). Install the latest kofrenot to open it.";

    /// <summary>Why a vault of <paramref name="format"/>, its upgrade not agreed, is not used.</summary>
    public static string OlderMessage(int format) =>
        $"This vault is in format {format}: this kofrenot uses it once upgraded to format {Current}. Then {WhatOlderKofrenotDo}.";

    // What the kofrenot of the older formats do with a vault this one wrote to.
    private static string WhatOlderKofrenotDo =>
        $"kofrenot older than {Since(OldestWriter)} can no longer change it, and older than {FirstReaderOf(OldestReader)} no longer opens it";

    // The first kofrenot reading a newer format's files as a reader of this one: from 0.15 for format 10.
    private static string FirstReaderOf(int format) => format == FirstLevels ? FirstLevelsVersion : Since(format);

    /// <summary>Why a vault written by a newer kofrenot, which this one may read but not write (<see cref="Vault.IsReadOnly"/>), is read only.</summary>
    public const string ReadOnlyMessage = "This vault was changed by a newer kofrenot: some details are hidden, and it cannot be changed here. Install the latest kofrenot to change it.";

    /// <summary>Whether a file of <paramref name="format"/>, readable from <paramref name="oldestReader"/> (its format when null), is read by this kofrenot.</summary>
    /// <exception cref="NewerVaultFormatException">It is not: its <paramref name="oldestReader"/> is newer than <see cref="Current"/>.</exception>
    public static void EnsureSupported(int format, int? oldestReader = null)
    {
        if ((oldestReader ?? format) > Current)
        {
            throw new NewerVaultFormatException(format);
        }
    }

    /// <summary>Whether a file's <c>oldestReader</c> or <c>oldestWriter</c> fits its <paramref name="format"/>: missing, or a format from 1 to it.</summary>
    internal static bool IsLevel(int? level, int format) => level is not { } value || (value >= 1 && value <= format);
}

/// <summary>The vault was written by a newer kofrenot: this one must be upgraded to use it.</summary>
public sealed class NewerVaultFormatException(int format)
    : NotSupportedException($"The vault has format {format}, written by a newer kofrenot: this one reads up to format {VaultFormat.Current}")
{
    public int Format { get; } = format;
}
