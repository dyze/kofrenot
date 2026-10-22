using System.Security.Cryptography;
using Kofrenot.Core.Logging;

namespace Kofrenot.Core.Storage;

/// <summary>
/// The wrapped keys of a vault, <c>keys/epoch-N/&lt;identity&gt;.&lt;random&gt;.kfw</c>: a random
/// part in each name, so no member can take the name the owner writes next (files on a server are
/// written once). Where a <see cref="KeySigner"/> is given, only the wraps it signed count: their
/// keys, and the latest epoch.
/// </summary>
public static partial class VaultKeys
{
    private const int RandomNameBytes = 8;

    /// <summary>The vault's latest key epoch, whoever wrote it; 0 for a vault without <c>keys/</c> (format 5: its master key).</summary>
    public static int LatestEpoch(string folder) => EpochsOf(folder).DefaultIfEmpty(0).Max();

    /// <summary>The latest epoch with a key signed by <paramref name="signedBy"/>; 0 when none.</summary>
    public static int LatestEpoch(string folder, Guid vault, byte[] signedBy) =>
        EpochsOf(folder).Where(epoch => WrapsOf(folder, epoch).Any(wrap => IsSigned(wrap, vault, epoch, signedBy))).DefaultIfEmpty(0).Max();

    /// <summary>
    /// Whether <paramref name="access"/> opens the files written last: it holds the latest epoch's key,
    /// among those <paramref name="signedBy"/> signed when given (a key planted by a member does not count).
    /// </summary>
    public static bool IsCurrent(string folder, VaultAccess access, byte[]? signedBy = null) =>
        access.Keys.CurrentEpoch >= (signedBy is null ? LatestEpoch(folder) : LatestEpoch(folder, access.Header.Id, signedBy));

    /// <summary>Whether the latest epoch <paramref name="signedBy"/> signed has a key for <paramref name="identity"/>.</summary>
    public static bool IsAttached(string folder, Guid vault, Guid identity, byte[] signedBy)
    {
        var latest = LatestEpoch(folder, vault, signedBy);
        return WrapsOf(folder, latest, identity).Any(wrap => IsSigned(wrap, vault, latest, signedBy));
    }

    private static KeyRing? RingOf(string folder, VaultHeader header, UnlockedIdentity identity, ILog? log, KeySigner? signer)
    {
        var keys = new Dictionary<int, byte[]>();
        foreach (var epoch in EpochsOf(folder))
        {
            foreach (var path in WrapsOf(folder, epoch, identity.Id))
            {
                try
                {
                    keys[epoch] = EpochKeys.Unwrap(File.ReadAllBytes(path), identity, header.Id, epoch, signer?.KeyFor(identity));
                    break;
                }
                catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
                {
                    log?.Warn(Category, $"A key of epoch {epoch} of vault {header.Id} is not used: {ex.Message}");
                }
            }
        }

        return keys.Count > 0 ? new KeyRing(keys) : null;
    }

    /// <summary>The epochs of the vault's keys (<c>keys/epoch-N</c>).</summary>
    private static IEnumerable<int> EpochsOf(string folder)
    {
        var keysFolder = VaultFolder.KeysOf(folder);
        return Directory.Exists(keysFolder)
            ? Directory.EnumerateDirectories(keysFolder, EpochPrefix + "*")
                .Select(epochFolder => int.TryParse(Path.GetFileName(epochFolder)[EpochPrefix.Length..], out var epoch) ? epoch : -1)
                .Where(epoch => epoch >= 0)
            : [];
    }

    /// <summary>The wrap files of an epoch: all, or those of one identity (named by it).</summary>
    private static IEnumerable<string> WrapsOf(string folder, int epoch, Guid? identity = null)
    {
        var epochFolder = Path.Combine(VaultFolder.KeysOf(folder), EpochPrefix + epoch);
        return Directory.Exists(epochFolder)
            ? Directory.EnumerateFiles(epochFolder, (identity?.ToString("N") ?? string.Empty) + "*" + WrapExtension)
                .Where(path => Path.GetExtension(path) == WrapExtension)
            : [];
    }

    // The identity a wrap is for is the first part of its name.
    private static bool IsSigned(string path, Guid vault, int epoch, byte[] signedBy) =>
        Guid.TryParseExact(Path.GetFileName(path)[..32], "N", out var identity) && IsSignedFor(path, vault, epoch, identity, signedBy);

    private static bool IsSignedFor(string path, Guid vault, int epoch, Guid identity, byte[] signedBy)
    {
        try
        {
            return EpochKeys.IsSignedBy(File.ReadAllBytes(path), vault, epoch, identity, signedBy);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Writes the vault's key of <paramref name="epoch"/> wrapped for <paramref name="identity"/>, unless <paramref name="signer"/> wrote one already.</summary>
    private static void WrapFor(string folder, Guid vault, int epoch, byte[] key, Identity identity, UnlockedIdentity signer) =>
        WrapFor(folder, vault, epoch, key, identity.Id, identity.PublicKey, signer);

    private static void WrapFor(string folder, Guid vault, int epoch, byte[] key, Guid identity, byte[] publicKey, UnlockedIdentity signer)
    {
        var signedBy = Signing.PublicKeyOf(signer);
        if (WrapsOf(folder, epoch, identity).Any(path => IsSignedFor(path, vault, epoch, identity, signedBy)))
        {
            return;
        }

        var name = $"{identity:N}.{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(RandomNameBytes))}{WrapExtension}";
        var path = Path.Combine(VaultFolder.KeysOf(folder), EpochPrefix + epoch, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        AtomicFile.Write(path, EpochKeys.Wrap(key, publicKey, vault, epoch, identity, signer), overwrite: false);
    }
}
