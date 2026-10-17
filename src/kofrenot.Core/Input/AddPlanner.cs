using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

/// <summary>Turns a request of the input box into the changes Enter would add (<see cref="AddPlan"/>).</summary>
public sealed class AddPlanner(KnowledgeGraph graph, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    /// <param name="choices">Which homonym on each side, and the type of each new node (default: the oldest, generic).</param>
    public AddPlan Plan(InputRequest request, AddChoices? choices = null)
    {
        choices ??= AddChoices.Default;
        return request switch
        {
            NodeRequest node => PlanNode(node, choices.LeftType ?? TypeCatalog.Generic),
            LinkRequest link => PlanLink(link, choices),
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.GetType().Name, "Unknown request"),
        };
    }

    private NodePlan PlanNode(NodeRequest request, NodeType type)
    {
        var now = _clock.GetUtcNow();
        return new NodePlan(request.Name, type, graph.NodesNamed(request.Name), [Added(NewNode(request.Name, type, now), now)]);
    }

    private LinkPlan PlanLink(LinkRequest request, AddChoices choices)
    {
        var left = Side(request.Left, choices.LeftType, choices.Left);
        var right = request.Right.Length == 0 ? null : Side(request.Right, choices.RightType, choices.Right);
        var problem = right is null ? LinkProblem.SecondNodeMissing
            : IsSameNode(left, right) ? LinkProblem.SameNode
            : LinkProblem.None;
        if (right is null || problem != LinkProblem.None)
        {
            return new LinkPlan(left, request.Type, right, problem, Existing: null, Changes: []);
        }

        if (left.Chosen is { } leftNode && right.Chosen is { } rightNode
            && graph.FindLink(request.Type.Id, leftNode.Id, rightNode.Id) is { } existing)
        {
            return new LinkPlan(left, request.Type, right, LinkProblem.AlreadyLinked, existing, Changes: []);
        }

        var now = _clock.GetUtcNow();
        var changes = new List<Change>();
        var from = Resolve(left, now, changes);
        var to = Resolve(right, now, changes);
        var link = new Link(Guid.NewGuid(), request.Type.Id, from, to, now);
        changes.Add(new LinkAdded(Guid.NewGuid(), now, link));
        return new LinkPlan(left, request.Type, right, LinkProblem.None, Existing: null, changes);
    }

    private PlannedSide Side(string name, NodeType? type, int choice)
    {
        var candidates = graph.NodesNamed(name);
        return new PlannedSide(name, type ?? TypeCatalog.Generic, candidates, Math.Clamp(choice, 0, Math.Max(0, candidates.Count - 1)));
    }

    private static bool IsSameNode(PlannedSide left, PlannedSide right) =>
        left.IsNew && right.IsNew
            ? NameKey.Of(left.Name) == NameKey.Of(right.Name)
            : left.Chosen?.Id == right.Chosen?.Id;

    // The chosen node's id, or a new node's, added to the changes.
    private static Guid Resolve(PlannedSide side, DateTimeOffset now, List<Change> changes)
    {
        if (side.Chosen is { } chosen)
        {
            return chosen.Id;
        }

        var node = NewNode(side.Name, side.Type, now);
        changes.Add(Added(node, now));
        return node.Id;
    }

    private static Node NewNode(string name, NodeType type, DateTimeOffset now) => new(Guid.NewGuid(), type.Id, name, now);

    private static NodeAdded Added(Node node, DateTimeOffset now) => new(Guid.NewGuid(), now, node);
}
