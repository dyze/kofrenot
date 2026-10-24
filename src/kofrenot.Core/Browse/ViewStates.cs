using Kofrenot.Core.Logging;
using Kofrenot.Core.Views;

namespace Kofrenot.Core.Browse;

/// <summary>How a view was left on this device: the nodes expanded in its tree, whether it shows details, and a list's cards.</summary>
public sealed class ViewState
{
    /// <summary>The <see cref="TreeItem.Key"/> of each node expanded.</summary>
    public HashSet<string> Expanded { get; set; } = [];

    /// <summary>The details (type, size, modified) are shown next to each node.</summary>
    public bool ShowsDetails { get; set; }

    /// <summary>A list shows its nodes as cards, not rows.</summary>
    public bool ShowsCards { get; set; }
}

/// <summary>
/// How each view was left, per vault (<see cref="ViewState"/>): it opens again so, also after a
/// restart. Kept on this device only (<see cref="LocalJsonFile{T}"/>), holding ids, never names. A
/// path no longer in the tree is ignored.
/// </summary>
public sealed class ViewStates(string path, ILog? log = null)
{
    private readonly LocalJsonFile<Dictionary<Guid, Dictionary<Guid, ViewState>>> _file = new(path, "View", "view states", log);
    private Dictionary<Guid, Dictionary<Guid, ViewState>>? _byVault;

    private Dictionary<Guid, Dictionary<Guid, ViewState>> ByVault => _byVault ??= _file.Load();

    /// <summary>The keys of the nodes expanded in a view.</summary>
    public IReadOnlySet<string> ExpandedIn(Guid vault, Guid view) => Find(vault, view)?.Expanded ?? new HashSet<string>();

    /// <summary>A node expanded or collapsed in a view: saved when it changes.</summary>
    public void SetExpanded(Guid vault, Guid view, string key, bool isExpanded) =>
        Change(vault, view, state => isExpanded ? state.Expanded.Add(key) : state.Expanded.Remove(key));

    /// <summary>Whether a view shows the details of its nodes (off by default).</summary>
    public bool ShowsDetails(Guid vault, Guid view) => Find(vault, view)?.ShowsDetails ?? false;

    public void SetShowsDetails(Guid vault, Guid view, bool showsDetails) => Change(vault, view, state =>
    {
        var changed = state.ShowsDetails != showsDetails;
        state.ShowsDetails = showsDetails;
        return changed;
    });

    /// <summary>Whether a list view shows its nodes as cards (off by default: rows).</summary>
    public bool ShowsCards(Guid vault, Guid view) => Find(vault, view)?.ShowsCards ?? false;

    public void SetShowsCards(Guid vault, Guid view, bool showsCards) => Change(vault, view, state =>
    {
        var changed = state.ShowsCards != showsCards;
        state.ShowsCards = showsCards;
        return changed;
    });

    private ViewState? Find(Guid vault, Guid view) =>
        ByVault.TryGetValue(vault, out var views) ? views.GetValueOrDefault(view) : null;

    /// <summary>Changes a view's state, then saves it when <paramref name="change"/> says it changed.</summary>
    private void Change(Guid vault, Guid view, Func<ViewState, bool> change)
    {
        if (!ByVault.TryGetValue(vault, out var views))
        {
            ByVault[vault] = views = [];
        }

        if (!views.TryGetValue(view, out var state))
        {
            views[view] = state = new ViewState();
        }

        if (change(state))
        {
            _file.Save(ByVault);
        }
    }
}
