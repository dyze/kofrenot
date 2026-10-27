namespace Kofrenot.Core.Tests.Input;

public sealed class ValueSearchTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly FieldDefinition Born = new("born", "Born", FieldKind.Date);
    private static readonly FieldDefinition Place = new("place", "Birthplace", FieldKind.Text);

    private readonly KnowledgeGraph _graph = new();

    public ValueSearchTests() => _graph.Apply(new NodeTypeDefined(Guid.NewGuid(), Start, "person", "Person", null, null, [Born, Place], []));

    private Node Person(string name, params (FieldDefinition Field, string Value)[] values)
    {
        var node = new Node(Guid.NewGuid(), "person", name, Start.AddMinutes(_graph.Nodes.Count));
        _graph.Add(node);
        foreach (var (field, value) in values)
        {
            _graph.Apply(new FieldSet(Guid.NewGuid(), Start, node.Id, "person", field.Id, field.Name, field.Kind, value, []));
        }

        return node;
    }

    [Fact]
    public void AValue_IsFound_AfterTheNames_WithItsField()
    {
        Person("Frank Herbert", (Place, "Tacoma, Washington"));
        Person("Tacoma");

        var hits = GraphSearch.Find(_graph, "wash");

        var hit = Assert.Single(hits);
        Assert.Equal(("Frank Herbert", "Birthplace: Tacoma, Washington", false), (hit.Node.Name, hit.Field, hit.IsExact));
        Assert.Equal(["Tacoma", "Frank Herbert"], GraphSearch.Find(_graph, "tacoma").Select(found => found.Node.Name));
        Assert.Null(GraphSearch.Find(_graph, "tacoma")[0].Field);
    }

    [Fact]
    public void ANodeFoundByName_IsNotFoundAgainByValue()
    {
        Person("Tacoma", (Place, "Tacoma"));

        Assert.Single(GraphSearch.Find(_graph, "tacoma"));
    }

    [Fact]
    public void Values_MatchByTheStartOfAWord_AndShowTheFirstFieldMatching()
    {
        Person("Frank Herbert", (Born, "1920-10-08"), (Place, "1920 Main Street"));

        Assert.Equal("Born: 1920-10-08", Assert.Single(GraphSearch.Find(_graph, "1920")).Field);
        Assert.Equal("Birthplace: 1920 Main Street", Assert.Single(GraphSearch.Find(_graph, "main")).Field);
        Assert.Empty(GraphSearch.Find(_graph, "ain"));
    }

    [Fact]
    public void NodesFoundByValue_AreByName_WithTheirHomonymRank_AndTheLimit()
    {
        Person("Paul", (Place, "Caladan"));
        Person("Leto", (Place, "Caladan"));
        Person("Paul", (Place, "Caladan"));

        var hits = GraphSearch.Find(_graph, "cala");

        Assert.Equal(["Leto", "Paul", "Paul"], hits.Select(hit => hit.Node.Name));
        Assert.Equal(["", "¹", "²"], hits.Select(hit => hit.Mark));
        Assert.Equal(2, GraphSearch.Find(_graph, "cala", limit: 2).Count);
    }

    [Fact]
    public void AClearedValue_OrARemovedNode_IsNotFound()
    {
        var frank = Person("Frank Herbert", (Place, "Tacoma"));
        var cleared = _graph.FieldsOf(frank.Id)[0].Shown;
        _graph.Apply(cleared with { Id = Guid.NewGuid(), Value = null, Replaces = [cleared.Id] });

        Assert.Empty(GraphSearch.Find(_graph, "tacoma"));

        var brian = Person("Brian Herbert", (Place, "Seattle"));
        _graph.Apply(new ChangesRemoved(Guid.NewGuid(), Start, [brian.Id]));

        Assert.Empty(GraphSearch.Find(_graph, "seattle"));
    }

    [Fact]
    public void ACheckbox_IsFoundAsShown()
    {
        var read = new FieldDefinition("read", "Read", FieldKind.Checkbox);
        _graph.Apply(new NodeTypeDefined(Guid.NewGuid(), Start, "book", "Book", null, null, [read], []));
        var dune = new Node(Guid.NewGuid(), "book", "Dune", Start);
        _graph.Add(dune);
        _graph.Apply(new FieldSet(Guid.NewGuid(), Start, dune.Id, "book", "read", "Read", FieldKind.Checkbox, "true", []));

        Assert.Equal("Read: Yes", Assert.Single(GraphSearch.Find(_graph, "yes")).Field);
        Assert.Empty(GraphSearch.Find(_graph, "true"));
    }
}
