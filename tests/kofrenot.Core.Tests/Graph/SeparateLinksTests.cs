namespace Kofrenot.Core.Tests.Graph;

/// <summary>Separate links: several links of a type between the same nodes, each with its qualifiers, not copies of one.</summary>
public sealed class SeparateLinksTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly ManualClock _clock = new();
    private readonly KnowledgeGraph _graph = new();
    private readonly Node _ann = new(Guid.NewGuid(), TypeCatalog.Generic.Id, "Ann", Start);
    private readonly Node _bob = new(Guid.NewGuid(), TypeCatalog.Generic.Id, "Bob", Start);

    public SeparateLinksTests()
    {
        _graph.Add(_ann);
        _graph.Add(_bob);
        _graph.Apply(new LinkTypeDefined(Guid.NewGuid(), Start, "married-to", "married to", null, [new FieldDefinition("start", "Start", FieldKind.Date)], []));
    }

    private Link Add(Guid from, Guid to, int minutes, bool separate = false)
    {
        var link = new Link(Guid.NewGuid(), "married-to", from, to, Start.AddMinutes(minutes), separate);
        _graph.Apply(new LinkAdded(Guid.NewGuid(), Start, link));
        return link;
    }

    [Fact]
    public void ASeparateLink_IsShownBesideTheOther_NotAsACopy()
    {
        var first = Add(_ann.Id, _bob.Id, 1);
        var second = Add(_bob.Id, _ann.Id, 2, separate: true);

        Assert.Equal([first, second], _graph.LinksOf(_ann.Id));
        Assert.Equal([first, second], _graph.LinksBetween("married-to", _bob.Id, _ann.Id));
        Assert.Equal(first, _graph.FindLink("married-to", _ann.Id, _bob.Id));
        Assert.Equal([second.Id], _graph.CopiesOf(second));
        Assert.Equal([first.Id], _graph.CopiesOf(first));
    }

    [Fact]
    public void ACopy_StillCountsAsOne()
    {
        var first = Add(_ann.Id, _bob.Id, 1);
        var copy = Add(_ann.Id, _bob.Id, 2);

        Assert.Equal([first], _graph.LinksOf(_ann.Id));
        Assert.Equal([first.Id, copy.Id], _graph.CopiesOf(first));
    }

    [Fact]
    public void ASeparateLinkRemoved_LeavesTheOther()
    {
        var first = Add(_ann.Id, _bob.Id, 1);
        var second = Add(_ann.Id, _bob.Id, 2, separate: true);

        _graph.Apply(new ChangesRemoved(Guid.NewGuid(), Start.AddMinutes(3), _graph.CopiesOf(second)));

        Assert.Equal([first], _graph.LinksOf(_bob.Id));
        Assert.Null(_graph.FindLink(second.Id));
    }

    [Fact]
    public void EachSeparateLink_HasItsOwnQualifiers()
    {
        var first = Add(_ann.Id, _bob.Id, 1);
        var second = Add(_ann.Id, _bob.Id, 2, separate: true);
        _graph.Apply(new FieldSet(Guid.NewGuid(), Start, first, "married-to", "start", "Start", FieldKind.Date, "1995-06-01", []));
        _graph.Apply(new FieldSet(Guid.NewGuid(), Start, second, "married-to", "start", "Start", FieldKind.Date, "2015-06-01", []));

        Assert.Equal("1995-06-01", NodeFields.ShownOf(_graph, first)!.Value);
        Assert.Equal("2015-06-01", NodeFields.ShownOf(_graph, second)!.Value);
    }

    [Fact]
    public void ALinkChangedToAnotherType_KeepsItsQualifiers_AsItsOwn()
    {
        var link = Add(_ann.Id, _bob.Id, 1, separate: true);
        var start = new FieldSet(Guid.NewGuid(), Start, link, "married-to", "start", "Start", FieldKind.Date, "1995-06-01", []);
        var photo = new FieldSet(Guid.NewGuid(), Start, link, FieldSet.Own, "photo", "Photo", FieldKind.Image, null, [], new DatasetFile("photo.png", 10, new string('a', 64)));
        _graph.Apply(start);
        _graph.Apply(photo);
        _graph.Apply(new FieldSet(Guid.NewGuid(), Start, link, FieldSet.Own, "note", "Note", FieldKind.Text, null, []));

        var plan = new LinkChangePlanner(_graph, _clock).Plan(link, _ann.Id, TypeCatalog.RelatesTo.Forward);
        foreach (var change in plan.Changes)
        {
            _graph.Apply(change);
        }

        var changed = Assert.Single(_graph.LinksOf(_ann.Id));
        Assert.True(changed.IsSeparate);
        var fields = NodeFields.OfLink(_graph, changed);
        Assert.Equal([("Start", "1995-06-01", true), ("Photo", null, true)], fields.Select(field => (field.Name, field.Value, field.IsOwn)));
        Assert.Equal(photo.Id, fields[1].Image!.Content);
    }
}
