using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Views;

/// <summary>The order of nodes in a view: by name as the search compares them (<see cref="NameKey"/>), homonyms oldest first.</summary>
internal static class NodeOrder
{
    public static IReadOnlyList<Node> ByName(IEnumerable<Node> nodes) => nodes
        .OrderBy(node => NameKey.Of(node.Name), StringComparer.Ordinal)
        .ThenBy(node => node.Created)
        .ThenBy(node => node.Id)
        .ToList();
}
