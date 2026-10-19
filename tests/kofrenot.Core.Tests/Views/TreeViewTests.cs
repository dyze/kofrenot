namespace Kofrenot.Core.Tests.Views;

public sealed class TreeViewTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly KnowledgeGraph _graph = new();

    [Fact]
    public void AnUnknownRoot_HasNoTree() =>
        Assert.Null(Tree(Guid.NewGuid()).Root);

    [Fact]
    public void ARemovedRoot_HasNoTree()
    {
        var projects = Add("Projects");
        _graph.Apply(new ChangesRemoved(Guid.NewGuid(), Start, [projects.Id]));

        Assert.Null(Tree(projects.Id).Root);
    }

    [Fact]
    public void TheRoot_HasNoParent()
    {
        var projects = Add("Projects");

        var root = Tree(projects.Id).Root!;

        Assert.Same(projects, root.Node);
        Assert.Equal([projects.Id], root.Path);
        Assert.Null(root.Parent);
        Assert.False(root.CanExpand);
    }

    [Fact]
    public void Children_AreTheNodesContained_ByName()
    {
        var (projects, beta, alpha) = (Add("Projects"), Add("beta"), Add("Alpha"));
        Contains(projects, beta);
        Contains(projects, alpha);
        var tree = Tree(projects.Id);

        var children = tree.ChildrenOf(tree.Root!);

        Assert.True(tree.Root!.CanExpand);
        Assert.Equal([alpha, beta], children.Select(child => child.Node));
        Assert.All(children, child => Assert.Equal(projects.Id, child.Parent));
        Assert.Equal([projects.Id, alpha.Id], children[0].Path);
        Assert.Equal($"{projects.Id}/{alpha.Id}", children[0].Key);
        Assert.Equal(tree.Root.Key, children[0].ParentKey);
        Assert.Null(tree.Root.ParentKey);
    }

    [Fact]
    public void OtherLinks_AndTheReverseWay_AreNotChildren()
    {
        var (projects, alpha, archive) = (Add("Projects"), Add("Alpha"), Add("Archive"));
        _graph.Apply(new LinkAdded(Guid.NewGuid(), Start, new Link(Guid.NewGuid(), TypeCatalog.RelatesTo.Id, projects.Id, alpha.Id, Start)));
        Contains(archive, projects);

        Assert.False(Tree(projects.Id).Root!.CanExpand);
    }

    [Fact]
    public void ANodeWithSeveralParents_IsShownUnderEach()
    {
        var (projects, alpha, beta, shared) = (Add("Projects"), Add("Alpha"), Add("Beta"), Add("Shared"));
        Contains(projects, alpha);
        Contains(projects, beta);
        Contains(alpha, shared);
        Contains(beta, shared);
        var tree = Tree(projects.Id);

        var under = tree.ChildrenOf(tree.Root!).Select(parent => Assert.Single(tree.ChildrenOf(parent))).ToList();

        Assert.All(under, item => Assert.Same(shared, item.Node));
        Assert.Equal([alpha.Id, beta.Id], under.Select(item => item.Parent!.Value));
    }

    [Fact]
    public void ACycle_IsCut_WhereANodeComesBackOnItsPath()
    {
        var (projects, alpha) = (Add("Projects"), Add("Alpha"));
        Contains(projects, alpha);
        Contains(alpha, projects);
        var tree = Tree(projects.Id);

        var alphaItem = Assert.Single(tree.ChildrenOf(tree.Root!));
        var back = Assert.Single(tree.ChildrenOf(alphaItem));

        Assert.Same(projects, back.Node);
        Assert.False(back.CanExpand);
        Assert.Empty(tree.ChildrenOf(back));
    }

    [Fact]
    public void ASymmetricType_LeadsEitherWay()
    {
        var (dune, herbert) = (Add("Dune"), Add("Frank Herbert"));
        _graph.Apply(new LinkAdded(Guid.NewGuid(), Start, new Link(Guid.NewGuid(), TypeCatalog.RelatesTo.Id, herbert.Id, dune.Id, Start)));
        var tree = new TreeView(_graph, new TreeDefinition(dune.Id, TypeCatalog.RelatesTo.Id));

        var child = Assert.Single(tree.ChildrenOf(tree.Root!));

        Assert.Same(herbert, child.Node);
        Assert.False(Assert.Single(tree.ChildrenOf(child)).CanExpand);
    }

    [Fact]
    public void ALinkAddedLater_IsShown()
    {
        var (projects, alpha) = (Add("Projects"), Add("Alpha"));
        var tree = Tree(projects.Id);
        Assert.False(tree.Root!.CanExpand);

        Contains(projects, alpha);

        Assert.Same(alpha, Assert.Single(tree.ChildrenOf(tree.Root!)).Node);
    }

    private TreeView Tree(Guid root) => new(_graph, new TreeDefinition(root, TypeCatalog.Contains.Id));

    private Node Add(string name)
    {
        var node = new Node(Guid.NewGuid(), TypeCatalog.Generic.Id, name, Start);
        _graph.Apply(new NodeAdded(Guid.NewGuid(), Start, node));
        return node;
    }

    private void Contains(Node parent, Node child) =>
        _graph.Apply(new LinkAdded(Guid.NewGuid(), Start, new Link(Guid.NewGuid(), TypeCatalog.Contains.Id, parent.Id, child.Id, Start)));
}
