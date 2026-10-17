using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Browse;

/// <summary>The change that pins a node, or unpins it (removing every pin it has).</summary>
public sealed class PinPlanner(KnowledgeGraph graph, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public Change Toggle(Node node) => graph.PinsOf(node.Id) is { Count: > 0 } pins
        ? new ChangesRemoved(Guid.NewGuid(), _clock.GetUtcNow(), pins)
        : new NodePinned(Guid.NewGuid(), _clock.GetUtcNow(), node.Id);
}
