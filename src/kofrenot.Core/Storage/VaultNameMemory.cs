using System.Text;
using Kofrenot.Core.Logging;

namespace Kofrenot.Core.Storage;

/// <summary>
/// The last name read in each vault, by vault id, so a locked vault is shown by its name. Kept
/// protected by <see cref="ISecretProtector"/>, as the name is encrypted in the vault.
/// </summary>
public sealed class VaultNameMemory(string path, ISecretProtector protector, ILog? log = null)
{
    private const string Category = "Vault";

    private readonly ILog _log = log ?? NullLog.Instance;
    private readonly ProtectedStore _store = new(path, protector, "vault name", log ?? NullLog.Instance);

    /// <summary>The name last read, or null (none yet, or it cannot be read on this account).</summary>
    public string? Recall(Guid vault) => _store.Recall(ProtectedStore.KeyOf(vault)) is { } name ? Encoding.UTF8.GetString(name) : null;

    /// <summary>Keeps <paramref name="name"/>; writes nothing when it is the one kept.</summary>
    public void Remember(Guid vault, string name)
    {
        if (Recall(vault) == name)
        {
            return;
        }

        _store.Remember(ProtectedStore.KeyOf(vault), Encoding.UTF8.GetBytes(name));
        _log.Debug(Category, $"Name of vault {vault} remembered");
    }

    public void Forget(Guid vault)
    {
        if (_store.Forget(ProtectedStore.KeyOf(vault)))
        {
            _log.Debug(Category, $"Name of vault {vault} forgotten");
        }
    }
}
