namespace Kofrenot.Core.Tests.Views;

public sealed class DeletePlannerTests
{
    private readonly TreeGraph _tree = new();
    private readonly ManualClock _clock = new();
    private readonly DeletePlanner _planner;

    public DeletePlannerTests() => _planner = new DeletePlanner(_tree.Graph, TypeCatalog.Contains.Id, _clock);

    [Fact]
    public void ANode_IsDeleted_WithItsWholeSubtree_AndEveryLinkOfThem()
    {
        var (projects, beta, alpha, notes, other) = (_tree.Add("Projects"), _tree.Add("Beta"), _tree.Add("Alpha"), _tree.Add("Notes"), _tree.Add("Other"));
        _tree.Contains(projects, alpha);
        _tree.Contains(alpha, beta);
        _tree.Contains(beta, notes);
        _tree.Link(TypeCatalog.RelatesTo.Id, notes, other);

        var plan = _planner.Plan(TreeGraph.Item(alpha, projects));

        Assert.Equal([alpha, beta, notes], plan.Nodes);
        Assert.Equal(4, plan.Links.Count);
        Assert.Empty(plan.OtherParents);
        Assert.Equal(_clock.Now, plan.Change.At);

        _tree.Graph.Apply(plan.Change);
        Assert.Equal([other, projects], _tree.Graph.Nodes.OrderBy(node => node.Name));
        Assert.Empty(_tree.Graph.Links);
    }

    [Fact]
    public void ANodeWithAnotherParent_IsDeletedToo_AndThatParentListed()
    {
        var (projects, alpha, shared, archive) = (_tree.Add("Projects"), _tree.Add("Alpha"), _tree.Add("Shared"), _tree.Add("Archive"));
        _tree.Contains(projects, alpha);
        _tree.Contains(alpha, shared);
        _tree.Contains(archive, shared);
        _tree.Contains(archive, alpha);

        var plan = _planner.Plan(TreeGraph.Item(alpha, projects));

        Assert.Equal([alpha, shared], plan.Nodes);
        Assert.Equal(
            [(alpha, archive), (shared, archive)],
            plan.OtherParents.Select(other => (other.Node, other.Parent)).OrderBy(pair => pair.Node.Name));
    }

    [Fact]
    public void ACycle_IsDeletedOnce()
    {
        var (projects, alpha) = (_tree.Add("Projects"), _tree.Add("Alpha"));
        _tree.Contains(projects, alpha);
        _tree.Contains(alpha, projects);

        var plan = _planner.Plan(TreeGraph.Item(projects));

        Assert.Equal([alpha, projects], plan.Nodes);
        Assert.Equal(2, plan.Links.Count);
        Assert.Empty(plan.OtherParents);
    }

    [Fact]
    public void EveryCopyOfALink_IsRemoved()
    {
        var (projects, alpha) = (_tree.Add("Projects"), _tree.Add("Alpha"));
        var link = _tree.Contains(projects, alpha);
        var copy = _tree.Contains(projects, alpha);

        var plan = _planner.Plan(TreeGraph.Item(alpha, projects));

        Assert.Contains(link.Id, plan.Change.Removes);
        Assert.Contains(copy.Id, plan.Change.Removes);
    }
}
