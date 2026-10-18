namespace Kofrenot.Core.Tests.Views;

public sealed class OperationPreviewTests
{
    private readonly TreeGraph _tree = new();

    [Fact]
    public void ADeletion_ListsItsNodes_ThenTheirOtherParents()
    {
        var (projects, alpha, shared, archive) = (_tree.Add("Projects"), _tree.Add("Alpha"), _tree.Add("Shared", TypeCatalog.Note.Id), _tree.Add("Archive"));
        _tree.Contains(projects, alpha);
        _tree.Contains(alpha, shared);
        _tree.Contains(archive, shared);
        var plan = new DeletePlanner(_tree.Graph, TypeCatalog.Contains.Id).Plan(TreeGraph.Item(alpha, projects));

        var preview = OperationPreview.Delete(plan, _tree.Graph.Types);

        Assert.True(preview.CanProceed);
        Assert.Equal("Deletes 2 nodes and 3 links:", preview.Summary);
        Assert.Equal(["Alpha (Generic)", "Shared (Note)", "“Shared” is also under “Archive”: deleted there too"], preview.Lines);
    }

    [Fact]
    public void ADeletionOfOneNode_SaysSo()
    {
        var plan = new DeletePlanner(_tree.Graph, TypeCatalog.Contains.Id).Plan(TreeGraph.Item(_tree.Add("Alpha")));

        Assert.Equal("Deletes 1 node and 0 links:", OperationPreview.Delete(plan, _tree.Graph.Types).Summary);
    }

    [Fact]
    public void ACopy_ListsItsNodes()
    {
        var (projects, alpha, archive) = (_tree.Add("Projects"), _tree.Add("Alpha"), _tree.Add("Archive"));
        _tree.Contains(projects, alpha);
        var plan = new CopyPlanner(_tree.Graph, TypeCatalog.Contains.Id).Plan(projects, archive, _ => true);

        var preview = OperationPreview.Copy(plan, archive, _tree.Graph.Types);

        Assert.True(preview.CanProceed);
        Assert.Equal("Copies 2 nodes under “Archive”, as new nodes:", preview.Summary);
        Assert.Equal(["Alpha (Generic)", "Projects (Generic)"], preview.Lines);
    }

    [Fact]
    public void ACopyOfContentNotSynced_CannotProceed_AndListsIt()
    {
        var (ideas, archive) = (_tree.Add("Ideas", TypeCatalog.Note.Id), _tree.Add("Archive"));
        _tree.Apply([new NoteEdited(Guid.NewGuid(), TreeGraph.Start, ideas.Id, [])]);
        var plan = new CopyPlanner(_tree.Graph, TypeCatalog.Contains.Id).Plan(ideas, archive, _ => false);

        var preview = OperationPreview.Copy(plan, archive, _tree.Graph.Types);

        Assert.False(preview.CanProceed);
        Assert.Equal(["Ideas (Note)"], preview.Lines);
    }

    [Theory]
    [InlineData(MoveProblem.Root, "The node the tree starts from cannot move.")]
    [InlineData(MoveProblem.SameParent, "The node is already there.")]
    [InlineData(MoveProblem.IntoItsSubtree, "A node cannot move into itself or under one of its children.")]
    [InlineData(MoveProblem.None, null)]
    public void AMoveRefused_SaysWhy(MoveProblem problem, string? refusal) =>
        Assert.Equal(refusal, OperationPreview.Refusal(problem));
}
