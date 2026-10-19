using Kofrenot.Core.Logging;

namespace Kofrenot.Core.Browse;

/// <summary>Lists of nodes per vault, kept on this device only (<see cref="LocalJsonFile{T}"/>), holding ids, never names.</summary>
/// <param name="what">What the lists hold, for the log ("recent nodes").</param>
internal sealed class NodeListsFile(string path, string category, string what, ILog? log = null)
{
    private readonly LocalJsonFile<Dictionary<Guid, List<Guid>>> _file = new(path, category, what, log);
    private Dictionary<Guid, List<Guid>>? _byVault;

    private Dictionary<Guid, List<Guid>> ByVault => _byVault ??= _file.Load();

    public IReadOnlyList<Guid> Of(Guid vault) => ByVault.TryGetValue(vault, out var nodes) ? nodes : [];

    /// <summary>Changes a vault's list, then saves.</summary>
    public void Change(Guid vault, Action<List<Guid>> change)
    {
        if (!ByVault.TryGetValue(vault, out var nodes))
        {
            ByVault[vault] = nodes = [];
        }

        change(nodes);
        _file.Save(ByVault);
    }
}
