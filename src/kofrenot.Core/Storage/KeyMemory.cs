using Kofrenot.Core.Logging;

namespace Kofrenot.Core.Storage;

/// <summary>
/// What the user asked to remember to open a vault (<i>Remember pass phrase</i>), by vault id,
/// protected by <see cref="ISecretProtector"/>: its identity's private key (format 6), or a format 5
/// vault's master key until it is upgraded. A key is kept rather than the pass phrase: it opens the
/// vault at once, without the slow key derivation, and stays valid when the pass phrase changes.
/// A missing or damaged file means nothing remembered.
/// </summary>
public sealed class KeyMemory(string path, ISecretProtector protector, ILog? log = null)
{
    private const string Category = "Vault";
    private const string IdentitySuffix = ".identity";
    private const int IdSize = 16;

    private readonly ILog _log = log ?? NullLog.Instance;
    private readonly ProtectedStore _store = new(path, protector, "pass phrase", log ?? NullLog.Instance);

    public bool Has(Guid vault) => _store.Has(IdentityKeyOf(vault)) || _store.Has(ProtectedStore.KeyOf(vault));

    /// <summary>The remembered identity, or null (none, or it cannot be read on this account).</summary>
    public UnlockedIdentity? RecallIdentity(Guid vault) =>
        _store.Recall(IdentityKeyOf(vault)) is { Length: > IdSize } kept
            ? new UnlockedIdentity(new Guid(kept.AsSpan(0, IdSize)), kept[IdSize..])
            : null;

    /// <summary>The remembered master key of a format 5 vault, or null.</summary>
    public byte[]? RecallMasterKey(Guid vault) => _store.Recall(ProtectedStore.KeyOf(vault));

    /// <summary>Remembers what opens the vault: its identity, replacing a master key kept before; without one, its master key.</summary>
    public void Remember(VaultAccess access)
    {
        var vault = access.Header.Id;
        if (access.Identity is { } identity)
        {
            _store.Remember(IdentityKeyOf(vault), [.. identity.Id.ToByteArray(), .. identity.PrivateKey]);
            _store.Forget(ProtectedStore.KeyOf(vault));
        }
        else
        {
            _store.Remember(ProtectedStore.KeyOf(vault), access.Keys.KeyOf(0));
        }

        _log.Info(Category, $"Pass phrase of vault {vault} remembered");
    }

    public void Forget(Guid vault)
    {
        if (_store.Forget(IdentityKeyOf(vault)) | _store.Forget(ProtectedStore.KeyOf(vault)))
        {
            _log.Info(Category, $"Remembered pass phrase of vault {vault} forgotten");
        }
    }

    private static string IdentityKeyOf(Guid vault) => ProtectedStore.KeyOf(vault) + IdentitySuffix;
}
