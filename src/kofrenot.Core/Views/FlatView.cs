using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;

namespace Kofrenot.Core.Views;

/// <summary>A list view: every node of a type, by name. Read from the graph each time, so it shows changes.</summary>
public sealed class FlatView(KnowledgeGraph graph, FlatDefinition definition)
{
    public IReadOnlyList<Node> Nodes => NodeOrder.ByName(graph.Nodes.Where(node => graph.HasType(node.Id, definition.NodeType)));

    /// <summary>The nodes whose name matches <paramref name="text"/> (<see cref="GraphSearch.MatcherOf"/>), in the order shown.</summary>
    public IReadOnlyList<Node> Find(string text)
    {
        var matches = GraphSearch.MatcherOf(text);
        return Nodes.Where(node => matches(node.Name)).ToList();
    }
}
