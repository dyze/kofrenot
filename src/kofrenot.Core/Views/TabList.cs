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

    /// <summary>The views not open as tabs, by name: what a new tab can show.</summary>
    public IReadOnlyList<Node> Closed(KnowledgeGraph graph, Guid vault)
    {
        var tabs = open.Of(vault);
        return NodeOrder.ByName(graph.Nodes.Where(node => IsView(node) && !tabs.Contains(node.Id)));
    }

    /// <summary>A view's definition: its latest version (the latest of a conflict), or null before the first.</summary>
    public static ViewDefinition? DefinitionOf(KnowledgeGraph graph, Node view) =>
        graph.ViewVersionsOf(view.Id) is [.., var latest] ? latest.Definition : null;

    private static bool IsView(Node node) => node.Type == TypeCatalog.View.Id;
}
