using Kofrenot.Core.Logging;

namespace Kofrenot.Core.Storage;

/// <summary>What opens a vault: its header, its keys, and the identity they were unwrapped with (none for a format 5 vault not upgraded).</summary>
public sealed record VaultAccess(VaultHeader Header, KeyRing Keys, UnlockedIdentity? Identity);

/// <summary>
/// The keys of a vault (<c>keys/</c>, since format 6): its identities
/// (<c>identities/&lt;id&gt;.json</c>), and its key of each epoch wrapped for each of them
/// (<c>epoch-N/&lt;identity&gt;.kfw</c>). A format 5 vault has none until it is upgraded: its
/// master key, in its header, becomes epoch 0.
/// </summary>
public static class VaultKeys
{
    private const string Category = "Vault";
    private const string IdentitiesFolder = "identities";
    private const string EpochPrefix = "epoch-";
    private const string IdentityExtension = ".json";
    private const string WrapExtension = ".kfw";

    /// <summary>Whether the vault has identities: it was created or upgraded in format 6.</summary>
    public static bool HasIdentities(string folder) => IdentityFiles(folder).Any();

    /// <summary>
    /// Unlocks the vault with its pass phrase: through the identity it opens, or, for a format 5
    /// vault, its header's key. Null when the pass phrase is wrong.
    /// </summary>
    /// <exception cref="InvalidDataException">The identity opens, but no key of the vault is wrapped for it.</exception>
    public static VaultAccess? Unlock(string folder, VaultHeader header, string passPhrase, ILog? log = null)
    {
        foreach (var identity in ReadIdentities(folder, log))
        {
            if (identity.Unlock(passPhrase) is { } unlocked)
            {
                return Open(folder, header, unlocked);
            }
        }

        return header.Unlock(passPhrase) is { } masterKey ? new VaultAccess(header, KeyRing.Of(masterKey), Identity: null) : null;
    }

    /// <summary>Unlocks the vault with the recovery key of one of its identities; null when it is none of theirs.</summary>
    /// <exception cref="InvalidDataException">The identity opens, but no key of the vault is wrapped for it.</exception>
    public static VaultAccess? Unlock(string folder, VaultHeader header, RecoveryKey recovery, ILog? log = null) =>
        ReadIdentities(folder, log).Select(identity => identity.Unlock(recovery)).FirstOrDefault(unlocked => unlocked is not null) is { } found
            ? Open(folder, header, found)
            : null;

    /// <summary>Unlocks the vault with an identity already unlocked (remembered); null when no key of the vault is wrapped for it.</summary>
    public static VaultAccess? Unlock(string folder, VaultHeader header, UnlockedIdentity identity, ILog? log = null) =>
        RingOf(folder, header, identity, log) is { } keys ? new VaultAccess(header, keys, identity) : null;

    /// <summary>
    /// Gives a vault its keys: a new identity sealed by <paramref name="passPhrase"/>, and
    /// <paramref name="key"/> as epoch 0, wrapped for it. Returns the recovery key, to show once.
    /// </summary>
    /// <exception cref="IOException">A file cannot be written.</exception>
    public static (VaultAccess Access, RecoveryKey Recovery) Create(string folder, VaultHeader header, byte[] key, string passPhrase, int iterations = KeyWrap.DefaultIterations)
    {
        var (identity, unlocked, recovery) = Identity.Create(passPhrase, iterations);
        var wrapPath = WrapOf(folder, 0, identity.Id);
        Directory.CreateDirectory(Path.GetDirectoryName(wrapPath)!);
        AtomicFile.Write(wrapPath, EpochKeys.Wrap(key, identity.PublicKey, header.Id, 0, identity.Id), overwrite: false);

        // The identity last: one found always has its key.
        var identityPath = IdentityOf(folder, identity.Id);
        Directory.CreateDirectory(Path.GetDirectoryName(identityPath)!);
        identity.Write(identityPath, overwrite: false);
        return (new VaultAccess(header, KeyRing.Of(key), unlocked), recovery);
    }

    /// <summary>
    /// Upgrades the keys of a format 5 vault: its master key, unlocked by its pass phrase, becomes
    /// epoch 0 of a new identity sealed by the same pass phrase. No file is rewritten. Null when
    /// the pass phrase is wrong.
    /// </summary>
    /// <exception cref="IOException">A file cannot be written.</exception>
    public static (VaultAccess Access, RecoveryKey Recovery)? Upgrade(string folder, VaultHeader header, string passPhrase, int iterations = KeyWrap.DefaultIterations) =>
        header.Unlock(passPhrase) is { } masterKey ? Create(folder, header, masterKey, passPhrase, iterations) : null;

    /// <summary>
    /// Seals the identity of <paramref name="access"/> by another pass phrase: its file is rewritten.
    /// A header still holding the master key is rewritten without it, so the old pass phrase no
    /// longer opens the vault.
    /// </summary>
    /// <exception cref="InvalidOperationException">No identity: the vault is not upgraded.</exception>
    /// <exception cref="InvalidDataException">The identity's file is damaged.</exception>
    /// <exception cref="IOException">A file cannot be read or written.</exception>
    public static VaultAccess ChangePassPhrase(string folder, VaultAccess access, string passPhrase, int iterations = KeyWrap.DefaultIterations)
    {
        var unlocked = access.Identity ?? throw new InvalidOperationException("The vault has no identity: upgrade it first");
        var path = IdentityOf(folder, unlocked.Id);
        Identity.Read(path).WithPassPhrase(unlocked, passPhrase, iterations).Write(path, overwrite: true);
        if (access.Header.Key is null)
        {
            return access;
        }

        var header = access.Header with { Format = Math.Max(access.Header.Format, VaultHeader.KeysApart), Key = null };
        header.Write(VaultFolder.HeaderOf(folder), overwrite: true);
        return access with { Header = header };
    }

    private static VaultAccess Open(string folder, VaultHeader header, UnlockedIdentity identity) =>
        Unlock(folder, header, identity) ?? throw new InvalidDataException("No key of the vault is wrapped for this identity");

    private static KeyRing? RingOf(string folder, VaultHeader header, UnlockedIdentity identity, ILog? log)
    {
        var keys = new Dictionary<int, byte[]>();
        var keysFolder = VaultFolder.KeysOf(folder);
        if (Directory.Exists(keysFolder))
        {
            foreach (var epochFolder in Directory.EnumerateDirectories(keysFolder, EpochPrefix + "*"))
            {
                if (int.TryParse(Path.GetFileName(epochFolder)[EpochPrefix.Length..], out var epoch)
                    && epoch >= 0
                    && WrapOf(folder, epoch, identity.Id) is var path
                    && File.Exists(path))
                {
                    try
                    {
                        keys[epoch] = EpochKeys.Unwrap(File.ReadAllBytes(path), identity, header.Id, epoch);
                    }
                    catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
                    {
                        log?.Warn(Category, $"The key of epoch {epoch} of vault {header.Id} cannot be read: {ex.Message}");
                    }
                }
            }
        }

        return keys.Count > 0 ? new KeyRing(keys) : null;
    }

    private static List<Identity> ReadIdentities(string folder, ILog? log)
    {
        var identities = new List<Identity>();
        foreach (var path in IdentityFiles(folder))
        {
            try
            {
                identities.Add(Identity.Read(path));
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
            {
                log?.Warn(Category, $"Identity {Path.GetFileNameWithoutExtension(path)} skipped: {ex.Message}");
            }
        }

        return identities;
    }

    // Skips files being written (.tmp) and anything else put there.
    private static IEnumerable<string> IdentityFiles(string folder)
    {
        var identities = Path.Combine(VaultFolder.KeysOf(folder), IdentitiesFolder);
        return Directory.Exists(identities)
            ? Directory.EnumerateFiles(identities, "*" + IdentityExtension).Where(path => Path.GetExtension(path) == IdentityExtension)
            : [];
    }

    private static string IdentityOf(string folder, Guid identity) =>
        Path.Combine(VaultFolder.KeysOf(folder), IdentitiesFolder, identity.ToString("N") + IdentityExtension);

    private static string WrapOf(string folder, int epoch, Guid identity) =>
        Path.Combine(VaultFolder.KeysOf(folder), EpochPrefix + epoch, identity.ToString("N") + WrapExtension);
}
