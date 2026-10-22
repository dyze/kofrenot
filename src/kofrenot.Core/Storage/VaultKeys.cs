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
public static partial class VaultKeys
{
    private const string Category = "Vault";
    private const string IdentitiesFolder = "identities";
    private const string EpochPrefix = "epoch-";
    private const string IdentityExtension = ".json";
    private const string WrapExtension = ".kfw";

    /// <summary>Whether the vault has identities: it was created or upgraded in format 6.</summary>
    public static bool HasIdentities(string folder) => IdentityFiles(folder).Any();

    /// <summary>
    /// Unlocks the vault with a pass phrase: through the identity it opens (<paramref name="account"/>
    /// first: a server account's, kept outside the vault), or, for a format 5 vault, its header's
    /// key. Null when the pass phrase is wrong.
    /// </summary>
    /// <exception cref="InvalidDataException">The identity opens, but no key of the vault is wrapped for it.</exception>
    public static VaultAccess? Unlock(string folder, VaultHeader header, string passPhrase, ILog? log = null, Identity? account = null, KeySigner? signer = null)
    {
        foreach (var identity in IdentitiesOf(folder, log, account))
        {
            if (identity.Unlock(passPhrase) is { } unlocked)
            {
                return Open(folder, header, unlocked, signer);
            }
        }

        return header.Unlock(passPhrase) is { } masterKey ? new VaultAccess(header, KeyRing.Of(masterKey), Identity: null) : null;
    }

    /// <summary>Unlocks the vault with the recovery key of one of its identities (or of <paramref name="account"/>); null when it is none of theirs.</summary>
    /// <exception cref="InvalidDataException">The identity opens, but no key of the vault is wrapped for it.</exception>
    public static VaultAccess? Unlock(string folder, VaultHeader header, RecoveryKey recovery, ILog? log = null, Identity? account = null, KeySigner? signer = null) =>
        IdentitiesOf(folder, log, account).Select(identity => identity.Unlock(recovery)).FirstOrDefault(unlocked => unlocked is not null) is { } found
            ? Open(folder, header, found, signer)
            : null;

    /// <summary>Unlocks the vault with an identity already unlocked (remembered); null when no key of the vault is wrapped for it.</summary>
    public static VaultAccess? Unlock(string folder, VaultHeader header, UnlockedIdentity identity, ILog? log = null, KeySigner? signer = null) =>
        RingOf(folder, header, identity, log, signer) is { } keys ? new VaultAccess(header, keys, identity) : null;

    /// <summary>
    /// Gives a vault its keys: a new identity sealed by <paramref name="passPhrase"/>, and
    /// <paramref name="key"/> as epoch 0, wrapped for it. Returns the recovery key, to show once.
    /// </summary>
    /// <exception cref="IOException">A file cannot be written.</exception>
    public static (VaultAccess Access, RecoveryKey Recovery) Create(string folder, VaultHeader header, byte[] key, string passPhrase, int iterations = KeyWrap.DefaultIterations)
    {
        var (identity, unlocked, recovery) = Identity.Create(passPhrase, iterations);
        WrapFor(folder, header.Id, 0, key, identity, unlocked);

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

    private static VaultAccess Open(string folder, VaultHeader header, UnlockedIdentity identity, KeySigner? signer) =>
        Unlock(folder, header, identity, signer: signer) ?? throw new InvalidDataException("No key of the vault is wrapped for this identity");

    // The account's identity first, then the vault's own.
    private static IEnumerable<Identity> IdentitiesOf(string folder, ILog? log, Identity? account) =>
        account is null ? ReadIdentities(folder, log) : [account, .. ReadIdentities(folder, log).Where(identity => identity.Id != account.Id)];

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
}
