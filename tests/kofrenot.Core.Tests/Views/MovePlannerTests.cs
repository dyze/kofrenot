namespace Kofrenot.Core.Tests.Views;

public sealed class MovePlannerTests
{
    private readonly TreeGraph _tree = new();
    private readonly MovePlanner _planner;

    public MovePlannerTests() => _planner = new MovePlanner(_tree.Graph, TypeCatalog.Contains.Id);

    [Fact]
    public void ANode_LeavesTheParentItIsShownUnder_ForTheTarget_KeepingItsOtherParents()
    {
        var (projects, archive, other, alpha) = (_tree.Add("Projects"), _tree.Add("Archive"), _tree.Add("Other"), _tree.Add("Alpha"));
        _tree.Contains(projects, alpha);
        _tree.Contains(projects, alpha);
        _tree.Contains(other, alpha);

        var plan = _planner.Plan(TreeGraph.Item(alpha, projects), archive);
        _tree.Apply(plan.Changes);

        Assert.Equal(MoveProblem.None, plan.Problem);
        Assert.Null(_tree.Graph.FindLink(TypeCatalog.Contains.Id, projects.Id, alpha.Id));
        Assert.NotNull(_tree.Graph.FindLink(TypeCatalog.Contains.Id, archive.Id, alpha.Id));
        Assert.NotNull(_tree.Graph.FindLink(TypeCatalog.Contains.Id, other.Id, alpha.Id));
    }

    [Fact]
    public void ANodeAlreadyUnderTheTarget_OnlyLeavesItsParent()
    {
        var (projects, other, alpha) = (_tree.Add("Projects"), _tree.Add("Other"), _tree.Add("Alpha"));
        _tree.Contains(projects, alpha);
        _tree.Contains(other, alpha);

        var plan = _planner.Plan(TreeGraph.Item(alpha, projects), other);

        Assert.IsType<ChangesRemoved>(Assert.Single(plan.Changes));
    }

    [Fact]
    public void ALinkRemovedMeanwhile_OnlyAddsTheNewOne()
    {
        var (projects, archive, alpha) = (_tree.Add("Projects"), _tree.Add("Archive"), _tree.Add("Alpha"));

        var plan = _planner.Plan(TreeGraph.Item(alpha, projects), archive);

        Assert.IsType<LinkAdded>(Assert.Single(plan.Changes));
    }

    [Fact]
    public void TheRoot_CannotMove()
    {
        var (projects, archive) = (_tree.Add("Projects"), _tree.Add("Archive"));

        var plan = _planner.Plan(TreeGraph.Item(projects), archive);

        Assert.Equal(MoveProblem.Root, plan.Problem);
        Assert.Empty(plan.Changes);
    }

    [Fact]
    public void MovingUnderTheSameParent_IsRefused()
    {
        var (projects, alpha) = (_tree.Add("Projects"), _tree.Add("Alpha"));
        _tree.Contains(projects, alpha);

        var plan = _planner.Plan(TreeGraph.Item(alpha, projects), projects);

        Assert.Equal(MoveProblem.SameParent, plan.Problem);
        Assert.Empty(plan.Changes);
    }

    [Fact]
    public void MovingIntoItself_OrUnderIt_IsRefused()
    {
        var (projects, alpha, beta) = (_tree.Add("Projects"), _tree.Add("Alpha"), _tree.Add("Beta"));
        _tree.Contains(projects, alpha);
        _tree.Contains(alpha, beta);

        Assert.Equal(MoveProblem.IntoItsSubtree, _planner.Plan(TreeGraph.Item(alpha, projects), alpha).Problem);
        Assert.Equal(MoveProblem.IntoItsSubtree, _planner.Plan(TreeGraph.Item(alpha, projects), beta).Problem);
    }
}
