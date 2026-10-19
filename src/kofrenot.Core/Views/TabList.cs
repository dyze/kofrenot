using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Views;

/// <summary>A tab of the main window: the search (no view), or a view node.</summary>
public sealed record Tab(Node? View)
{
    public static Tab Search { get; } = new(View: null);

    public bool IsSearch => View is null;

    public string Name => View?.Name ?? "Search";
}

/// <summary>
/// The tabs of the main window for a vault: the search first, always there, then the views open
/// on this device (<see cref="ViewTabs"/>) that the graph knows as views.
/// </summary>
public sealed class TabList(ViewTabs open)
{
    public IReadOnlyList<Tab> Of(KnowledgeGraph graph, Guid vault) =>
    [
        Tab.Search,
        .. open.Of(vault).Select(graph.FindNode).OfType<Node>().Where(IsView).Select(view => new Tab(view)),
    ];

    /// <summary>
    /// The tab <paramref name="step"/> tabs away from the one showing <paramref name="current"/>
    /// (null, or a view no longer open: the search), wrapping around: Ctrl+Tab is 1, Ctrl+Shift+Tab -1.
    /// </summary>
    public Tab Next(KnowledgeGraph graph, Guid vault, Guid? current, int step)
    {
        var tabs = Of(graph, vault);
        var index = Math.Max(0, tabs.ToList().FindIndex(tab => tab.View?.Id == current));
        return tabs[(((index + step) % tabs.Count) + tabs.Count) % tabs.Count];
    }

    /// <summary>The views not open as tabs, by name: what a new tab can show.</summary>
    public IReadOnlyList<Node> Closed(KnowledgeGraph graph, Guid vault)
    {
        var tabs = open.Of(vault);
        return NodeOrder.ByName(graph.Nodes.Where(node => IsView(node) && !tabs.Contains(node.Id)));
    }

    private static bool IsView(Node node) => node.Type == TypeCatalog.View.Id;
}
