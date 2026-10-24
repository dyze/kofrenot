using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

/// <summary>
/// Turns a colour chosen for a node into the change that gives it. The change replaces every
/// current colour, so choosing any colour of a node in conflict resolves the conflict.
/// </summary>
public sealed class ColorPlanner(KnowledgeGraph graph, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    /// <summary>The change, or null when <paramref name="color"/> is already the node's and there is no conflict.</summary>
    public NodeColored? Plan(Node node, NodeColor color)
    {
        var current = graph.ColorsOf(node.Id);
        var unchanged = current switch
        {
            [] => color == NodeColor.None,
            [{ } only] => only.Color == color,
            _ => false,
        };
        return unchanged
            ? null
            : new NodeColored(Guid.NewGuid(), _clock.GetUtcNow(), node.Id, NodeColorNames.Of(color), current.Select(version => version.Id).ToList());
    }
}
