using Kofrenot.Core.Logging;
using Kofrenot.Core.Views;

namespace Kofrenot.Core.Browse;

/// <summary>How a view was left on this device: the nodes expanded in its tree, whether it shows details, a list's cards, a graph's link names, a query view's sort.</summary>
public sealed class ViewState
{
    /// <summary>The <see cref="TreeItem.Key"/> of each node expanded.</summary>
    public HashSet<string> Expanded { get; set; } = [];

    /// <summary>The details (type, size, modified) are shown next to each node.</summary>
    public bool ShowsDetails { get; set; }

    /// <summary>A list shows its nodes as cards, not rows.</summary>
    public bool ShowsCards { get; set; }

    /// <summary>A tree shows the unlinked nodes beside it (<see cref="UnlinkedNodes"/>), to put them in it.</summary>
    public bool ShowsUnlinked { get; set; }

    /// <summary>A graph writes the name of each link on its line.</summary>
    public bool ShowsLinkNames { get; set; }

    /// <summary>The column a query view is sorted by (<see cref="QuerySort.NameColumn"/>, then its columns from 1).</summary>
    public int SortColumn { get; set; }

    /// <summary>A query view is sorted Z→A.</summary>
    public bool SortDescending { get; set; }
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

    /// <summary>Whether a tree view shows the unlinked nodes beside it (off by default).</summary>
    public bool ShowsUnlinked(Guid vault, Guid view) => Find(vault, view)?.ShowsUnlinked ?? false;

    public void SetShowsUnlinked(Guid vault, Guid view, bool showsUnlinked) => Change(vault, view, state =>
    {
        var changed = state.ShowsUnlinked != showsUnlinked;
        state.ShowsUnlinked = showsUnlinked;
        return changed;
    });

    /// <summary>Whether a graph view writes the name of each link on its line (off by default: in a tooltip).</summary>
    public bool ShowsLinkNames(Guid vault, Guid view) => Find(vault, view)?.ShowsLinkNames ?? false;

    public void SetShowsLinkNames(Guid vault, Guid view, bool showsLinkNames) => Change(vault, view, state =>
    {
        var changed = state.ShowsLinkNames != showsLinkNames;
        state.ShowsLinkNames = showsLinkNames;
        return changed;
    });

    /// <summary>How a query view is sorted (by name, A→Z, by default).</summary>
    public QuerySort SortOf(Guid vault, Guid view) =>
        Find(vault, view) is { } state ? new QuerySort(state.SortColumn, state.SortDescending) : QuerySort.ByName;

    public void SetSort(Guid vault, Guid view, QuerySort sort) => Change(vault, view, state =>
    {
        var changed = (state.SortColumn, state.SortDescending) != (sort.Column, sort.Descending);
        (state.SortColumn, state.SortDescending) = (sort.Column, sort.Descending);
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
