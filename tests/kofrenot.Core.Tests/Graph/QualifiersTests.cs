namespace Kofrenot.Core.Tests.Graph;

/// <summary>Qualifiers: fields on links, defined by their link type or their own, merged over the copies of a link.</summary>
public sealed class QualifiersTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly FieldDefinition Date = new("date", "Date", FieldKind.Date);
    private static readonly FieldDefinition Source = new("source", "Source", FieldKind.Url);

    private readonly ManualClock _clock = new();
    private readonly KnowledgeGraph _graph = new();
    private readonly Node _dune = new(Guid.NewGuid(), TypeCatalog.Generic.Id, "Dune", Start);
    private readonly Node _frank = new(Guid.NewGuid(), TypeCatalog.Generic.Id, "Frank Herbert", Start);
    private readonly LinkTypeDefined _wrote;
    private readonly Link _link;

    public QualifiersTests()
    {
        _wrote = new LinkTypeDefined(Guid.NewGuid(), Start, "wrote", "wrote", "written by", [Date, Source], []);
        _link = new Link(Guid.NewGuid(), "wrote", _frank.Id, _dune.Id, Start.AddMinutes(1));
        foreach (Change change in new Change[] { new NodeAdded(Guid.NewGuid(), Start, _dune), new NodeAdded(Guid.NewGuid(), Start, _frank), _wrote, new LinkAdded(Guid.NewGuid(), Start, _link) })
        {
            _graph.Apply(change);
        }
    }

    private FieldSet Set(Link link, FieldDefinition field, string? value, int minutes = 2, params Guid[] replaces) =>
        new(Guid.NewGuid(), Start.AddMinutes(minutes), link, "wrote", field.Id, field.Name, field.Kind, value, replaces);

    [Fact]
    public void ALink_HasItsTypesQualifiers_EmptyUntilSet()
    {
        var fields = NodeFields.OfLink(_graph, _link);

        Assert.Equal(["Date", "Source"], fields.Select(field => field.Label));
        Assert.All(fields, field => Assert.Equal(("wrote", "wrote", false), (field.Owner, field.TypeName, field.HasValue)));
        Assert.Null(NodeFields.ShownOf(_graph, _link));
        Assert.Empty(_graph.QualifiersOf(_link));
    }

    [Fact]
    public void AQualifierSet_IsShown_OnTheLinkWhicheverWayItIsRead()
    {
        _graph.Apply(Set(_link, Date, "1965-08-01"));

        Assert.Equal("1965-08-01", NodeFields.OfLink(_graph, _link)[0].Value);
        Assert.Equal("Date", NodeFields.ShownOf(_graph, _link)!.Name);
        Assert.Equal(NodeFields.OfLink(_graph, _link).Select(field => field.Value), NodeFields.Of(_graph, FieldHolder.OfLink(_link.Id)).Select(field => field.Value));
        Assert.Equal([_link], _graph.LinksWithQualifiers);
        Assert.Empty(_graph.FieldsOf(_dune.Id));
    }

    [Fact]
    public void QualifiersSetOnTwoCopies_AreMerged_AndAConflictWhenBothSetOne()
    {
        var copy = _link with { Id = Guid.NewGuid(), Created = Start.AddMinutes(3) };
        _graph.Apply(new LinkAdded(Guid.NewGuid(), Start, copy));
        _graph.Apply(Set(_link, Date, "1965-08-01"));
        _graph.Apply(Set(copy, Date, "1966-01-01", minutes: 3));
        _graph.Apply(Set(copy, Source, "https://isfdb.org", minutes: 4));

        var fields = NodeFields.OfLink(_graph, _link);

        Assert.Equal(["1965-08-01", "1966-01-01"], fields[0].Conflict);
        Assert.Equal("https://isfdb.org", fields[1].Value);
        Assert.Equal(_link, _graph.FindLink(copy.Id));
        Assert.Equal(3, _graph.QualifierSetsOf(_link).Count());
        Assert.Equal([_link], _graph.LinksWithQualifiers);
    }

    [Fact]
    public void ARemovedLink_HidesItsQualifiers()
    {
        _graph.Apply(Set(_link, Date, "1965-08-01"));
        _graph.Apply(new ChangesRemoved(Guid.NewGuid(), Start.AddMinutes(5), [_link.Id]));

        Assert.Null(_graph.FindLink(_link.Id));
        Assert.Empty(NodeFields.Of(_graph, FieldHolder.OfLink(_link.Id)));
        Assert.Empty(_graph.LinksWithQualifiers);
    }

    [Fact]
    public void AQualifierTakenOutOfItsType_StaysOnTheLink_AsItsOwn()
    {
        _graph.Apply(Set(_link, Source, "https://isfdb.org"));
        _graph.Apply(_wrote with { Id = Guid.NewGuid(), At = Start.AddMinutes(4), Fields = [Date], Replaces = [_wrote.Id] });

        var fields = NodeFields.OfLink(_graph, _link);

        Assert.Equal(["Date", "Source"], fields.Select(field => field.Name));
        Assert.True(fields[1].IsOwn);
        Assert.Equal("https://isfdb.org", fields[1].Value);
    }

    [Fact]
    public void AnOwnQualifier_IsAdded_ToTheLink()
    {
        var planner = new FieldPlanner(_graph, _clock);

        var plan = planner.AddOwn(_link, "Confidence", FieldKind.Text, "high");
        _graph.Apply(plan.Changes.Single());

        var own = NodeFields.OfLink(_graph, _link)[^1];
        Assert.Equal(("Confidence", "high", true), (own.Name, own.Value, own.IsOwn));
        Assert.Equal(FieldHolder.OfLink(_link.Id), Assert.IsType<FieldSet>(plan.Changes[0]).Holder);
        Assert.Equal(FieldProblem.NameTaken, planner.AddOwn(_link, "confidence", FieldKind.Text, "low").Problem);
    }

    [Fact]
    public void AValueTyped_ReplacesTheCurrentOnes_OfEveryCopy()
    {
        var copy = _link with { Id = Guid.NewGuid(), Created = Start.AddMinutes(3) };
        _graph.Apply(new LinkAdded(Guid.NewGuid(), Start, copy));
        var first = Set(_link, Date, "1965-08-01");
        var second = Set(copy, Date, "1966-01-01", minutes: 3);
        _graph.Apply(first);
        _graph.Apply(second);

        var change = Assert.IsType<FieldSet>(new FieldPlanner(_graph, _clock).Set(_link, NodeFields.OfLink(_graph, _link)[0], "1965-08-01").Changes.Single());
        _graph.Apply(change);

        Assert.Equal(new[] { first.Id, second.Id }.Order(), change.Replaces.Order());
        Assert.Equal("1965-08-01", NodeFields.OfLink(_graph, _link)[0].Value);
        Assert.Empty(NodeFields.OfLink(_graph, _link)[0].Conflict);
    }

    [Fact]
    public void ABuiltInLinkType_GivenFields_KeepsItsNames()
    {
        _graph.Apply(new LinkTypeDefined(Guid.NewGuid(), Start, TypeCatalog.Contains.Id, "holds", null, [Date], []));
        var contains = new Link(Guid.NewGuid(), TypeCatalog.Contains.Id, _frank.Id, _dune.Id, Start);
        _graph.Apply(new LinkAdded(Guid.NewGuid(), Start, contains));

        Assert.Equal("contains", _graph.Types.FindLinkType(TypeCatalog.Contains.Id)!.Name);
        Assert.DoesNotContain(_graph.LinkTypes.All, custom => custom.Type.Id == TypeCatalog.Contains.Id);
        Assert.Equal([Date], Assert.Single(_graph.LinkTypes.BuiltInDefined).Fields);
        Assert.Equal(["Date"], NodeFields.OfLink(_graph, contains).Select(field => field.Name));
        Assert.Equal("contains", NodeFields.OfLink(_graph, contains)[0].TypeName);
    }

    [Fact]
    public void ABuiltInLinkTypesFields_Removed_AreGone()
    {
        var defined = new LinkTypeDefined(Guid.NewGuid(), Start, TypeCatalog.Contains.Id, "contains", "contained by", [Date], []);
        _graph.Apply(defined);
        _graph.Apply(new ChangesRemoved(Guid.NewGuid(), Start.AddMinutes(1), [defined.Id]));

        Assert.Empty(_graph.LinkTypes.BuiltInDefined);
        Assert.Empty(_graph.LinkTypes.FieldsOf(TypeCatalog.Contains.Id));
    }

    [Fact]
    public void ALinkOfATypeNotKnown_HasItsOwnQualifiersOnly()
    {
        var unknown = new Link(Guid.NewGuid(), "cited", _frank.Id, _dune.Id, Start);
        _graph.Apply(new LinkAdded(Guid.NewGuid(), Start, unknown));
        _graph.Apply(new FieldSet(Guid.NewGuid(), Start, unknown, FieldSet.Own, "page", "Page", FieldKind.Number, "12", []));

        var field = Assert.Single(NodeFields.OfLink(_graph, unknown));
        Assert.Equal(("Page", true), (field.Name, field.IsOwn));
    }

    [Fact]
    public void ALinksHistory_ListsItsQualifiersValues_AndRestoresOne_OverEveryCopy()
    {
        var copy = _link with { Id = Guid.NewGuid(), Created = Start.AddMinutes(3) };
        _graph.Apply(new LinkAdded(Guid.NewGuid(), Start, copy));
        var first = Set(_link, Date, "1965-08-01");
        var second = Set(copy, Date, "1966-01-01", minutes: 3, first.Id);
        _graph.Apply(first);
        _graph.Apply(second);

        var history = NodeHistory.OfLink(_graph, _link);

        Assert.Equal([("Date", "1966-01-01", true), ("Date", "1965-08-01", false)], history.Select(entry => (entry.What, entry.Label, entry.IsCurrent)));
        var planner = new RestorePlanner(_graph, _clock);
        Assert.Null(planner.Plan(_link, history[0]));
        var restored = Assert.IsType<FieldSet>(Assert.Single(planner.Plan(_link, history[1])!.Changes));
        Assert.Equal((FieldHolder.OfLink(_link.Id), "1965-08-01"), (restored.Holder, restored.Value));
        Assert.Equal([second.Id], restored.Replaces);
    }
}
