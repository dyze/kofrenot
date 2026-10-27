using Kofrenot.Core.Logging;

namespace Kofrenot.Core.Browse;

/// <summary>
/// The nodes kept offline on this device, per vault: their content is downloaded ahead
/// (<see cref="Storage.Vault.KeepOfflineAsync"/>). Kept on this device only (<see cref="NodeListsFile{T}"/>),
/// never in the vault: each device chooses its own.
/// </summary>
public sealed class OfflineNodes(string path, ILog? log = null)
{
    private readonly NodeListsFile<Guid> _file = new(path, "Offline", "nodes kept offline", log);

    /// <summary>The nodes kept offline in this vault, the first kept first.</summary>
    public IReadOnlyList<Guid> Of(Guid vault) => _file.Of(vault);

    public bool Has(Guid vault, Guid node) => Of(vault).Contains(node);

    /// <summary>Keeps a node offline, or no longer, and saves.</summary>
    public void Set(Guid vault, Guid node, bool kept) => _file.Change(vault, nodes =>
    {
        nodes.Remove(node);
        if (kept)
        {
            nodes.Add(node);
        }
    });
}
