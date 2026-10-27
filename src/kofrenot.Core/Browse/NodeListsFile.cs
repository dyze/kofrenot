using Kofrenot.Core.Logging;

namespace Kofrenot.Core.Browse;

/// <summary>Lists of nodes per vault, kept on this device only (<see cref="LocalJsonFile{T}"/>), holding ids (and when, for recent nodes), never names.</summary>
/// <param name="what">What the lists hold, for the log ("recent nodes").</param>
internal sealed class NodeListsFile<T>(string path, string category, string what, ILog? log = null)
{
    private readonly LocalJsonFile<Dictionary<Guid, List<T>>> _file = new(path, category, what, log);
    private Dictionary<Guid, List<T>>? _byVault;

    private Dictionary<Guid, List<T>> ByVault => _byVault ??= _file.Load();

    public IReadOnlyList<T> Of(Guid vault) => ByVault.TryGetValue(vault, out var nodes) ? nodes : [];

    /// <summary>Changes a vault's list, then saves.</summary>
    public void Change(Guid vault, Action<List<T>> change)
    {
        if (!ByVault.TryGetValue(vault, out var nodes))
        {
            ByVault[vault] = nodes = [];
        }

        change(nodes);
        _file.Save(ByVault);
    }
}
