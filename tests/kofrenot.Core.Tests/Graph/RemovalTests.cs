namespace Kofrenot.Core.Tests.Graph;

public sealed class RemovalTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly KnowledgeGraph _graph = new();

    [Fact]
    public void ARemovedNode_IsNoLongerShown_NorItsLinks()
    {
        var (dune, herbert) = (Add("Dune"), Add("Frank Herbert"));
        Add(NewLink(dune, herbert));

        Assert.True(_graph.Apply(Removal(dune.Id)));

        Assert.Null(_graph.FindNode(dune.Id));
        Assert.Empty(_graph.NodesNamed("Dune"));
        Assert.Equal([herbert], _graph.Nodes);
        Assert.Empty(_graph.LinksOf(herbert.Id));
    }

    [Fact]
    public void ARemovalBeforeItsNode_HidesItOnceArrived()
    {
        var dune = NewNode("Dune");
        _graph.Apply(Removal(dune.Id));

        Assert.True(_graph.Apply(new NodeAdded(Guid.NewGuid(), Start, dune)));

        Assert.Null(_graph.FindNode(dune.Id));
        Assert.Empty(_graph.NodesNamed("Dune"));
    }

    [Fact]
    public void ARenameOfARemovedNode_DoesNotShowIt()
    {
        var dune = Add("Dune");
        _graph.Apply(Removal(dune.Id));

        _graph.Apply(new NodeRenamed(Guid.NewGuid(), Start.AddMinutes(1), dune.Id, "Dune (novel)", [dune.Id]));

        Assert.Empty(_graph.Nodes);
        Assert.Empty(_graph.NodesNamed("Dune (novel)"));
    }

    [Fact]
    public void ARemovedNode_IsNoLongerPinned()
    {
        var dune = Add("Dune");
        _graph.Apply(new NodePinned(Guid.NewGuid(), Start, dune.Id));

        _graph.Apply(Removal(dune.Id));

        Assert.Empty(_graph.PinnedNodes);
    }

    [Fact]
    public void ARemovedLink_IsNoLongerShown()
    {
        var (dune, herbert) = (Add("Dune"), Add("Frank Herbert"));
        var link = Add(NewLink(dune, herbert));

        _graph.Apply(Removal(link.Id));

        Assert.Empty(_graph.LinksOf(dune.Id));
        Assert.Empty(_graph.LinksOf(herbert.Id));
        Assert.Empty(_graph.Links);
        Assert.Null(_graph.FindLink(link.Type, dune.Id, herbert.Id));
        Assert.Empty(_graph.CopiesOf(link));
    }

    [Fact]
    public void ARemovalBeforeItsLink_HidesIt()
    {
        var (dune, herbert) = (Add("Dune"), Add("Frank Herbert"));
        var link = NewLink(dune, herbert);
        _graph.Apply(Removal(link.Id));

        Assert.False(_graph.Apply(new LinkAdded(Guid.NewGuid(), Start, link)));

        Assert.Empty(_graph.LinksOf(dune.Id));
    }

    [Fact]
    public void ALinkToItself_IsRemoved()
    {
        var dune = Add("Dune");
        var link = Add(NewLink(dune, dune));

        _graph.Apply(Removal(link.Id));

        Assert.Empty(_graph.LinksOf(dune.Id));
    }

    [Fact]
    public void CopiesOfALink_AreListedOldestFirst_EvenTypedTheOtherWay()
    {
        var (dune, herbert) = (Add("Dune"), Add("Frank Herbert"));
        var newer = NewLink(herbert, dune, minutes: 5);
        var older = NewLink(dune, herbert, minutes: 1);

        Assert.True(_graph.Apply(new LinkAdded(Guid.NewGuid(), Start, newer)));
        Assert.True(_graph.Apply(new LinkAdded(Guid.NewGuid(), Start, older)));

        Assert.Equal([older.Id, newer.Id], _graph.CopiesOf(newer));
        Assert.Equal([older], _graph.LinksOf(dune.Id));
        Assert.Equal([older], _graph.LinksOf(herbert.Id));
        Assert.Single(_graph.Links);
    }

    [Fact]
    public void RemovingTheLinkShown_ShowsItsNextCopy()
    {
        var (dune, herbert) = (Add("Dune"), Add("Frank Herbert"));
        var older = Add(NewLink(dune, herbert, minutes: 1));
        var newer = Add(NewLink(dune, herbert, minutes: 5));

        _graph.Apply(Removal(older.Id));

        Assert.Equal([newer], _graph.LinksOf(dune.Id));
        Assert.Same(newer, _graph.FindLink(newer.Type, dune.Id, herbert.Id));
    }

    [Fact]
    public void RemovingANewerCopy_KeepsTheLinkShown()
    {
        var (dune, herbert) = (Add("Dune"), Add("Frank Herbert"));
        var older = Add(NewLink(dune, herbert, minutes: 1));
        var newer = Add(NewLink(dune, herbert, minutes: 5));

        _graph.Apply(Removal(newer.Id));

        Assert.Equal([older], _graph.LinksOf(dune.Id));
        Assert.Equal([older.Id], _graph.CopiesOf(older));
    }

    [Fact]
    public void RemovingEveryCopy_RemovesTheLink()
    {
        var (dune, herbert) = (Add("Dune"), Add("Frank Herbert"));
        var older = Add(NewLink(dune, herbert, minutes: 1));
        Add(NewLink(dune, herbert, minutes: 5));

        _graph.Apply(Removal([.. _graph.CopiesOf(older)]));

        Assert.Empty(_graph.LinksOf(dune.Id));
        Assert.Empty(_graph.Links);
    }

    [Fact]
    public void ALinkAddedAgain_AfterItsRemoval_IsShown()
    {
        var (dune, herbert) = (Add("Dune"), Add("Frank Herbert"));
        var removed = Add(NewLink(dune, herbert));
        _graph.Apply(Removal(removed.Id));

        var again = Add(NewLink(dune, herbert, minutes: 5));

        Assert.Equal([again], _graph.LinksOf(dune.Id));
    }

    [Fact]
    public void RemovingAnUnknownId_ChangesNoLink()
    {
        var (dune, herbert) = (Add("Dune"), Add("Frank Herbert"));
        var link = Add(NewLink(dune, herbert));

        _graph.Apply(Removal(Guid.NewGuid(), link.Id, link.Id));

        Assert.Empty(_graph.LinksOf(dune.Id));
    }

    [Fact]
    public void ARemovalAppliedTwice_ChangesNothing()
    {
        var removal = Removal(Add("Dune").Id);
        _graph.Apply(removal);

        Assert.False(_graph.Apply(removal));
    }

    private Node Add(string name)
    {
        var node = NewNode(name);
        _graph.Apply(new NodeAdded(Guid.NewGuid(), node.Created, node));
        return node;
    }

    private Link Add(Link link)
    {
        _graph.Apply(new LinkAdded(Guid.NewGuid(), link.Created, link));
        return link;
    }

    private static Node NewNode(string name) => new(Guid.NewGuid(), TypeCatalog.Generic.Id, name, Start);

    private static Link NewLink(Node from, Node to, int minutes = 0) =>
        new(Guid.NewGuid(), TypeCatalog.RelatesTo.Id, from.Id, to.Id, Start.AddMinutes(minutes));

    private static ChangesRemoved Removal(params Guid[] ids) => new(Guid.NewGuid(), Start.AddMinutes(10), ids);
}
