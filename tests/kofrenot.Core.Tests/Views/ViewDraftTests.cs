namespace Kofrenot.Core.Tests.Views;

public sealed class ViewDraftTests
{
    private readonly TreeGraph _tree = new();

    [Fact]
    public void ATree_IsEditedWithItsRootAndLinkType()
    {
        var projects = _tree.Add("Projects");

        var draft = ViewDraft.Of(new TreeDefinition(projects.Id, TypeCatalog.IsA.Id), _tree.Graph);

        Assert.Equal((ViewDisplay.Tree, projects, TypeCatalog.IsA), (draft.Display, draft.Root, draft.LinkType));
        Assert.Equal(new TreeDefinition(projects.Id, TypeCatalog.IsA.Id), draft.Definition);
    }

    [Fact]
    public void ATreeWhoseRootIsGone_HasItsRootToChooseAgain()
    {
        var draft = ViewDraft.Of(new TreeDefinition(Guid.NewGuid(), TypeCatalog.Contains.Id), _tree.Graph);

        Assert.Null(draft.Root);
        Assert.Null(draft.Definition);
    }

    [Fact]
    public void AList_IsEditedWithItsNodeType()
    {
        var draft = ViewDraft.Of(new FlatDefinition(TypeCatalog.Dataset.Id), _tree.Graph);

        Assert.Equal((ViewDisplay.Flat, TypeCatalog.Dataset), (draft.Display, draft.NodeType));
        Assert.Equal(new FlatDefinition(TypeCatalog.Dataset.Id), draft.Definition);
    }

    [Fact]
    public void TypesThisVersionDoesNotKnow_AreKeptByTheirId()
    {
        var root = _tree.Add("Projects");

        Assert.Equal(new TreeDefinition(root.Id, "part-of"), ViewDraft.Of(new TreeDefinition(root.Id, "part-of"), _tree.Graph).Definition);
        Assert.Equal(new FlatDefinition("book"), ViewDraft.Of(new FlatDefinition("book"), _tree.Graph).Definition);
    }

    [Fact]
    public void AnUnknownDefinition_IsEditedFromTheDefault() =>
        Assert.Equal(ViewDraft.Default, ViewDraft.Of(new OtherDefinition(), _tree.Graph));

    [Fact]
    public void Roots_AreFoundByName()
    {
        var projects = _tree.Add("Projects");
        _tree.Add("Dune");

        Assert.Equal([projects], ViewDraft.FindRoots(_tree.Graph, "proj").Select(hit => hit.Node));
    }

    [Fact]
    public void AView_IsDescribedInASentence()
    {
        var projects = _tree.Add("Projects");

        Assert.Equal("A tree from “Projects” through “contains”", ViewDraft.Describe(new TreeDefinition(projects.Id, TypeCatalog.Contains.Id), _tree.Graph));
        Assert.Equal("A tree from “Projects” through “part-of”", ViewDraft.Describe(new TreeDefinition(projects.Id, "part-of"), _tree.Graph));
        Assert.Equal("A tree from a node not here: removed, or not synced yet", ViewDraft.Describe(new TreeDefinition(Guid.NewGuid(), TypeCatalog.Contains.Id), _tree.Graph));
        Assert.Equal("A list of every Note", ViewDraft.Describe(new FlatDefinition(TypeCatalog.Note.Id), _tree.Graph));
        Assert.Equal("No definition yet", ViewDraft.Describe(null, _tree.Graph));
    }

    private sealed record OtherDefinition : ViewDefinition;
}
