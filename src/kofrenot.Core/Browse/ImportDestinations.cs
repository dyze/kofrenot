using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.Core.Logging;

namespace Kofrenot.Core.Browse;

/// <summary>Where an import goes: a node of a tree view, linked by the view's link type.</summary>
public sealed record ImportDestination(Guid View, Guid Node);

/// <summary>A destination whose view and node are there: the parent link the imported items get.</summary>
public sealed record ResolvedDestination(Node View, Node Node, ParentLink Under)
{
    /// <summary>"Projects › Invoices 2026".</summary>
    public string Label => $"{View.Name} › {Node.Name}";
}

/// <summary>
/// The last destination of an import (files dropped or shared, text shared), per vault, offered
/// first at the next one. Kept on this device only (<see cref="NodeListsFile{T}"/>), never in the vault.
/// </summary>
public sealed class ImportDestinations(string path, ILog? log = null)
{
    private readonly NodeListsFile<ImportDestination> _file = new(path, "Import", "import destinations", log);

    /// <summary>The destination last chosen in this vault; null for none.</summary>
    public ImportDestination? Of(Guid vault) => _file.Of(vault) is [var last, ..] ? last : null;

    /// <summary>Keeps <paramref name="destination"/> as the vault's next one; null for none.</summary>
    public void Set(Guid vault, ImportDestination? destination)
    {
        if (Of(vault) != destination)
        {
            _file.Change(vault, destinations =>
            {
                destinations.Clear();
                if (destination is not null)
                {
                    destinations.Add(destination);
                }
            });
        }
    }

    /// <summary>The destination's view and node, or null when one is gone (removed, not synced yet) or the view is no tree.</summary>
    public static ResolvedDestination? Resolve(KnowledgeGraph graph, ImportDestination? destination) =>
        destination is not null
        && graph.FindNode(destination.View) is { } view
        && TreeOf(graph, view) is { } tree
        && graph.FindNode(destination.Node) is { } node
            ? new ResolvedDestination(view, node, new ParentLink(node, tree.LinkType))
            : null;

    /// <summary>The tree views, by name: where an import can go.</summary>
    public static IReadOnlyList<Node> TreeViews(KnowledgeGraph graph) => graph.Nodes
        .Where(node => node.Type == TypeCatalog.View.Id && TreeOf(graph, node) is { } tree && graph.FindNode(tree.Root) is not null)
        .OrderBy(node => NameKey.Of(node.Name), StringComparer.Ordinal)
        .ThenBy(node => node.Created)
        .ToList();

    /// <summary>The latest definition of <paramref name="view"/> when it is a tree.</summary>
    public static TreeDefinition? TreeOf(KnowledgeGraph graph, Node view) =>
        graph.ViewVersionsOf(view.Id) is [.., var latest] ? latest.Definition as TreeDefinition : null;
}
