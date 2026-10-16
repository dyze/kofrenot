using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

/// <summary>Turns a request of the input box into the changes Enter would add (<see cref="AddPlan"/>).</summary>
public sealed class AddPlanner(KnowledgeGraph graph, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    /// <param name="leftChoice">For a link, which of the nodes named like the left side (0: the oldest).</param>
    /// <param name="rightChoice">The same for the right side.</param>
    public AddPlan Plan(InputRequest request, int leftChoice = 0, int rightChoice = 0) => request switch
    {
        NodeRequest node => PlanNode(node),
        LinkRequest link => PlanLink(link, leftChoice, rightChoice),
        _ => throw new ArgumentOutOfRangeException(nameof(request), request.GetType().Name, "Unknown request"),
    };

    private NodePlan PlanNode(NodeRequest request)
    {
        var now = _clock.GetUtcNow();
        return new NodePlan(request.Name, graph.NodesNamed(request.Name), [Added(NewNode(request.Name, now), now)]);
    }

    private LinkPlan PlanLink(LinkRequest request, int leftChoice, int rightChoice)
    {
        var left = Side(request.Left, leftChoice);
        var right = request.Right.Length == 0 ? null : Side(request.Right, rightChoice);
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

    private PlannedSide Side(string name, int choice)
    {
        var candidates = graph.NodesNamed(name);
        return new PlannedSide(name, candidates, Math.Clamp(choice, 0, Math.Max(0, candidates.Count - 1)));
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

        var node = NewNode(side.Name, now);
        changes.Add(Added(node, now));
        return node.Id;
    }

    private static Node NewNode(string name, DateTimeOffset now) => new(Guid.NewGuid(), TypeCatalog.Generic.Id, name, now);

    private static NodeAdded Added(Node node, DateTimeOffset now) => new(Guid.NewGuid(), now, node);
}
