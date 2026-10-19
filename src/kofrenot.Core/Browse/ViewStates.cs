using Kofrenot.Core.Logging;
using Kofrenot.Core.Views;

namespace Kofrenot.Core.Browse;

/// <summary>
/// The nodes expanded in each tree view, per vault, by <see cref="TreeItem.Key"/>: a view opens
/// again as it was left, also after a restart. Kept on this device only (<see cref="LocalJsonFile{T}"/>),
/// holding ids, never names. A path no longer in the tree is ignored.
/// </summary>
public sealed class ViewStates(string path, ILog? log = null)
{
    private readonly LocalJsonFile<Dictionary<Guid, Dictionary<Guid, HashSet<string>>>> _file = new(path, "View", "view states", log);
    private Dictionary<Guid, Dictionary<Guid, HashSet<string>>>? _byVault;

    private Dictionary<Guid, Dictionary<Guid, HashSet<string>>> ByVault => _byVault ??= _file.Load();

    /// <summary>The keys of the nodes expanded in a view.</summary>
    public IReadOnlySet<string> ExpandedIn(Guid vault, Guid view) =>
        ByVault.TryGetValue(vault, out var views) && views.TryGetValue(view, out var expanded) ? expanded : new HashSet<string>();

    /// <summary>A node expanded or collapsed in a view: saved when it changes.</summary>
    public void SetExpanded(Guid vault, Guid view, string key, bool isExpanded)
    {
        if (!ByVault.TryGetValue(vault, out var views))
        {
            ByVault[vault] = views = [];
        }

        if (!views.TryGetValue(view, out var expanded))
        {
            views[view] = expanded = [];
        }

        if (isExpanded ? expanded.Add(key) : expanded.Remove(key))
        {
            _file.Save(ByVault);
        }
    }
}
