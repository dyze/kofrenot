using Kofrenot.Core.Logging;

namespace Kofrenot.Core.Storage;

/// <summary>
/// The master keys the user asked to remember (<i>Remember pass phrase</i>), by vault id, each
/// protected by <see cref="ISecretProtector"/>. The key is kept rather than the pass phrase: it
/// opens the vault at once, without the slow key derivation, and stays valid when the pass
/// phrase changes. A missing or damaged file means nothing remembered.
/// </summary>
public sealed class KeyMemory(string path, ISecretProtector protector, ILog? log = null)
{
    private const string Category = "Vault";

    private readonly ILog _log = log ?? NullLog.Instance;
    private readonly ProtectedStore _store = new(path, protector, "pass phrase", log ?? NullLog.Instance);

    public bool Has(Guid vault) => _store.Has(ProtectedStore.KeyOf(vault));

    /// <summary>The remembered key, or null (none, or it cannot be read on this account).</summary>
    public byte[]? Recall(Guid vault) => _store.Recall(ProtectedStore.KeyOf(vault));

    public void Remember(Guid vault, byte[] masterKey)
    {
        _store.Remember(ProtectedStore.KeyOf(vault), masterKey);
        _log.Info(Category, $"Pass phrase of vault {vault} remembered");
    }

    public void Forget(Guid vault)
    {
        if (_store.Forget(ProtectedStore.KeyOf(vault)))
        {
            _log.Info(Category, $"Remembered pass phrase of vault {vault} forgotten");
        }
    }
}
