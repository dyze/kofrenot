using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Views;

/// <summary>A list view: every node of a type, by name. Read from the graph each time, so it shows changes.</summary>
public sealed class FlatView(KnowledgeGraph graph, FlatDefinition definition)
{
    public IReadOnlyList<Node> Nodes => NodeOrder.ByName(graph.Nodes.Where(node => node.Type == definition.NodeType));
}
