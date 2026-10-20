namespace Kofrenot.Core.Tests.Input;

public sealed class LinkChangePlannerTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly KnowledgeGraph _graph = new();
    private readonly ManualClock _clock = new() { Now = Start.AddMinutes(10) };
    private readonly Node _box = new(Guid.NewGuid(), "generic", "Box", Start);
    private readonly Node _room = new(Guid.NewGuid(), "generic", "Room", Start);

    public LinkChangePlannerTests()
    {
        _graph.Add(_box);
        _graph.Add(_room);
    }

    private static LinkVerb Verb(LinkType type, bool reverse = false) => new(type, reverse);

    private Link AddLink(string type, Guid from, Guid to)
    {
        var link = new Link(Guid.NewGuid(), type, from, to, Start);
        _graph.Add(link);
        return link;
    }

    private LinkChangePlan Plan(Link link, Guid seenFrom, LinkVerb target) => new LinkChangePlanner(_graph, _clock).Plan(link, seenFrom, target);

    [Fact]
    public void AnotherType_RemovesTheLink_AndAddsTheNewOne_ReadFromTheNodeSeen()
    {
        var link = AddLink(TypeCatalog.RelatesTo.Id, _box.Id, _room.Id);

        var plan = Plan(link, _room.Id, Verb(TypeCatalog.Contains));

        Assert.Equal(LinkChangeProblem.None, plan.Problem);
        var removal = Assert.IsType<ChangesRemoved>(plan.Changes[0]);
        Assert.Equal([link.Id], removal.Removes);
        var added = Assert.IsType<LinkAdded>(plan.Changes[1]).Link;
        Assert.Equal((TypeCatalog.Contains.Id, _room.Id, _box.Id, Start.AddMinutes(10)), (added.Type, added.From, added.To, added.Created));

        foreach (var change in plan.Changes)
        {
            _graph.Apply(change);
        }

        Assert.Equal("contains", _graph.Types.LinkName(Assert.Single(_graph.LinksOf(_room.Id)), _room.Id));
    }

    [Fact]
    public void TheReverseWay_TurnsTheLinkAround()
    {
        var link = AddLink(TypeCatalog.Contains.Id, _room.Id, _box.Id);

        var added = Assert.IsType<LinkAdded>(Plan(link, _room.Id, Verb(TypeCatalog.Contains, reverse: true)).Changes[1]).Link;

        Assert.Equal((_box.Id, _room.Id), (added.From, added.To));
    }

    [Fact]
    public void TheWayItReads_IsUnchanged()
    {
        var link = AddLink(TypeCatalog.Contains.Id, _room.Id, _box.Id);

        Assert.Equal(LinkChangeProblem.Unchanged, Plan(link, _room.Id, Verb(TypeCatalog.Contains)).Problem);
        Assert.Equal(LinkChangeProblem.Unchanged, Plan(link, _box.Id, Verb(TypeCatalog.Contains, reverse: true)).Problem);
        Assert.Empty(Plan(link, _room.Id, Verb(TypeCatalog.Contains)).Changes);
    }

    [Fact]
    public void ALinkThatExistsAlready_IsRefused()
    {
        var link = AddLink(TypeCatalog.RelatesTo.Id, _box.Id, _room.Id);
        AddLink(TypeCatalog.Contains.Id, _room.Id, _box.Id);

        Assert.Equal(LinkChangeProblem.Exists, Plan(link, _room.Id, Verb(TypeCatalog.Contains)).Problem);
    }

    [Fact]
    public void ALinkOfAnUnknownType_CanBeChanged()
    {
        var link = AddLink("cites", _box.Id, _room.Id);

        Assert.Equal(LinkChangeProblem.None, Plan(link, _box.Id, Verb(TypeCatalog.RelatesTo)).Problem);
        Assert.Null(_graph.Types.VerbOf(link, _box.Id));
    }

    [Fact]
    public void ItsCopies_AreRemovedToo()
    {
        var link = AddLink(TypeCatalog.RelatesTo.Id, _box.Id, _room.Id);
        var copy = new Link(Guid.NewGuid(), TypeCatalog.RelatesTo.Id, _box.Id, _room.Id, Start.AddMinutes(1));
        _graph.Add(copy);

        var removal = Assert.IsType<ChangesRemoved>(Plan(link, _box.Id, Verb(TypeCatalog.IsA)).Changes[0]);

        Assert.Equal([link.Id, copy.Id], removal.Removes);
    }
}
