namespace Kofrenot.Core.Tests.Browse;

public sealed class DeletionPlannerTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly KnowledgeGraph _graph = new();
    private readonly ManualClock _clock = new();
    private readonly Node _dune = new(Guid.NewGuid(), TypeCatalog.Note.Id, "Dune", Start);
    private readonly Node _frank = new(Guid.NewGuid(), TypeCatalog.Generic.Id, "Frank Herbert", Start);
    private readonly DeletionPlanner _planner;

    public DeletionPlannerTests()
    {
        _graph.Add(_dune);
        _graph.Add(_frank);
        _planner = new DeletionPlanner(_graph, _clock);
    }

    [Fact]
    public void ANode_GoesWithItsLinks_TheirCopies_AndItsPins()
    {
        var link = Link(_dune, _frank);
        var copy = Link(_dune, _frank);
        var pin = new NodePinned(Guid.NewGuid(), Start, _dune.Id);
        _graph.Apply(pin);

        var deletion = _planner.Node(_dune);

        Assert.Equal(1, deletion.Links);
        Assert.Equal(new[] { _dune.Id, link.Id, copy.Id, pin.Id }.Order(), deletion.Change.Removes.Order());
        Assert.Equal(_clock.Now, deletion.Change.At);
        Assert.Contains("Delete “Dune” and its link?", deletion.Question, StringComparison.Ordinal);
        _graph.Apply(deletion.Change);
        Assert.Null(_graph.FindNode(_dune.Id));
        Assert.Empty(_graph.LinksOf(_frank.Id));
        Assert.Empty(_graph.PinnedNodes);
    }

    [Fact]
    public void TheQuestion_CountsTheLinks()
    {
        Assert.StartsWith("Delete “Frank Herbert”?", _planner.Node(_frank).Question, StringComparison.Ordinal);
        Link(_frank, _dune);
        Link(_frank, new Node(Guid.NewGuid(), TypeCatalog.Generic.Id, "Arrakis", Start));

        Assert.StartsWith("Delete “Frank Herbert” and its 2 links?", _planner.Node(_frank).Question, StringComparison.Ordinal);
    }

    [Fact]
    public void ALink_GoesWithItsCopies()
    {
        var link = Link(_dune, _frank);
        var copy = Link(_dune, _frank);

        Assert.Equal(new[] { link.Id, copy.Id }.Order(), _planner.Link(link).Removes.Order());
    }

    private Link Link(Node from, Node to)
    {
        _graph.Add(to);
        var link = new Link(Guid.NewGuid(), TypeCatalog.RelatesTo.Id, from.Id, to.Id, Start);
        _graph.Add(link);
        return link;
    }
}
