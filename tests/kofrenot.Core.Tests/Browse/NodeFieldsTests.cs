namespace Kofrenot.Core.Tests.Browse;

public sealed class NodeFieldsTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly FieldDefinition Born = new("born", "Born", FieldKind.Date);
    private static readonly FieldDefinition Website = new("website", "Website", FieldKind.Url);
    private static readonly FieldDefinition Genre = new("genre", "Genre", FieldKind.Choice, ["Novel", "Short story"]);

    private readonly KnowledgeGraph _graph = new();
    private readonly Node _frank = new(Guid.NewGuid(), TypeCatalog.Generic.Id, "Frank Herbert", Start);
    private readonly NodeTypeDefined _person = new(Guid.NewGuid(), Start, "person", "Person", "Contact", "blue", [Born, Website], []);

    public NodeFieldsTests()
    {
        _graph.Add(_frank);
        _graph.Apply(_person);
        _graph.Apply(new NodeTypeAdded(Guid.NewGuid(), Start.AddMinutes(1), _frank.Id, "person"));
    }

    private IReadOnlyList<NodeField> Fields => NodeFields.Of(_graph, _frank.Id);

    private FieldSet Set(string owner, FieldDefinition field, string? value, int minutes, params Guid[] replaces) =>
        new(Guid.NewGuid(), Start.AddMinutes(minutes), _frank.Id, owner, field.Id, field.Name, field.Kind, value, replaces);

    [Fact]
    public void ATypesFields_ShowEmpty_InTheirOrder()
    {
        var fields = Fields;

        Assert.Equal(["Born", "Website"], fields.Select(field => field.Label));
        Assert.All(fields, field => Assert.Equal((null, "", false), (field.Value, field.Display, field.IsOwn)));
        Assert.Equal(("person", "born", FieldKind.Date, "Person"), (fields[0].Owner, fields[0].Field, fields[0].Kind, fields[0].Type!.Name));
        Assert.Empty(fields[0].Current);
        Assert.Empty(fields[0].Conflict);
    }

    [Fact]
    public void AValueSet_IsShown()
    {
        var born = Set("person", Born, "1920-10-08", 2);
        _graph.Apply(born);

        var field = Fields[0];

        Assert.Equal(("1920-10-08", "1920-10-08"), (field.Value, field.Display));
        Assert.Equal([born.Id], field.Current);
    }

    [Fact]
    public void ACheckbox_IsShownAsYesOrNo_AndANoneValue_AsNone()
    {
        var done = new FieldDefinition("done", "Read", FieldKind.Checkbox);
        _graph.Apply(new NodeTypeDefined(Guid.NewGuid(), Start.AddMinutes(2), "person", "Person", null, null, [done], [_person.Id]));
        var yes = Set("person", done, "true", 3);
        _graph.Apply(yes);
        _graph.Apply(Set("person", done, null, 4, yes.Id));
        _graph.Apply(Set("person", done, "false", 5, yes.Id));

        var field = Assert.Single(Fields);

        Assert.Equal("No", field.Display);
        Assert.Equal(["(none)", "No"], field.Conflict);
    }

    [Fact]
    public void TypesFields_ComeInTheOrderOfTheTypes_WithTheirChoices()
    {
        _graph.Apply(new NodeTypeDefined(Guid.NewGuid(), Start, "author", "Author", null, null, [Genre], []));
        _graph.Apply(new NodeTypeAdded(Guid.NewGuid(), Start.AddMinutes(2), _frank.Id, "author"));

        Assert.Equal(["Born", "Website", "Genre"], Fields.Select(field => field.Label));
        Assert.Equal(["Novel", "Short story"], Fields[2].Choices);
    }

    [Fact]
    public void ContentFields_AreNotListed()
    {
        _graph.Apply(new NodeTypeAdded(Guid.NewGuid(), Start.AddMinutes(2), _frank.Id, "note"));

        Assert.Equal(["Born", "Website"], Fields.Select(field => field.Label));
    }

    [Fact]
    public void AnOwnField_ShowsAfter_OnlyWithAValue()
    {
        var place = new FieldDefinition(Guid.NewGuid().ToString(), "Birthplace", FieldKind.Text);
        var tacoma = Set(FieldSet.Own, place, "Tacoma", 2);
        _graph.Apply(tacoma);

        var own = Fields[^1];
        Assert.Equal(("Birthplace", "Tacoma", true, (NodeType?)null), (own.Label, own.Value, own.IsOwn, own.Type));

        _graph.Apply(Set(FieldSet.Own, place, null, 3, tacoma.Id));

        Assert.Equal(["Born", "Website"], Fields.Select(field => field.Label));
    }

    [Fact]
    public void FieldsOfTheSameName_SayWhereTheyComeFrom()
    {
        _graph.Apply(Set(FieldSet.Own, new FieldDefinition("x", "born", FieldKind.Text), "Tacoma", 2));

        Assert.Equal(["Born (Person)", "Website", "born (own)"], Fields.Select(field => field.Label));
    }

    [Fact]
    public void AFieldRemovedFromItsType_KeepsItsValue_AsOwn()
    {
        _graph.Apply(Set("person", Website, "https://dune.example", 2));
        _graph.Apply(Set("person", Born, "1920-10-08", 3));
        _graph.Apply(new NodeTypeDefined(Guid.NewGuid(), Start.AddMinutes(4), "person", "Person", null, null, [Born], [_person.Id]));

        var fields = Fields;

        Assert.Equal(["Born", "Website"], fields.Select(field => field.Label));
        Assert.Equal(("person", "https://dune.example", true), (fields[1].Owner, fields[1].Value, fields[1].IsOwn));
    }

    [Fact]
    public void AFieldRenamedInItsType_KeepsItsValue()
    {
        _graph.Apply(Set("person", Born, "1920-10-08", 2));
        _graph.Apply(new NodeTypeDefined(Guid.NewGuid(), Start.AddMinutes(3), "person", "Person", null, null, [Born with { Name = "Birth date" }, Website], [_person.Id]));

        Assert.Equal(("Birth date", "1920-10-08", false), (Fields[0].Label, Fields[0].Value, Fields[0].IsOwn));
    }

    [Fact]
    public void ATypeTakenOut_LeavesItsValues_AsOwn()
    {
        _graph.Apply(Set("person", Born, "1920-10-08", 2));
        _graph.Apply(new NodeTypeRemoved(Guid.NewGuid(), Start.AddMinutes(3), _frank.Id, [.. _graph.TypesOf(_frank.Id).Where(type => type.Type == "person").Select(type => type.Id)]));

        var field = Assert.Single(Fields);
        Assert.Equal(("Born", "1920-10-08", true), (field.Label, field.Value, field.IsOwn));
    }

    [Fact]
    public void ATypeRemovedFromTheVault_LeavesTheNode_AndItsValues_AsOwn()
    {
        _graph.Apply(Set("person", Born, "1920-10-08", 2));

        _graph.Apply(new ChangesRemoved(Guid.NewGuid(), Start.AddMinutes(3), [_person.Id]));

        Assert.Equal(["generic"], _graph.TypeIdsOf(_frank.Id));
        Assert.Equal("generic", _graph.FindNode(_frank.Id)!.Type);
        Assert.True(Assert.Single(Fields).IsOwn);
    }

    [Fact]
    public void ATypeArrivingAfterItsNodes_ShowsOnThem()
    {
        var graph = new KnowledgeGraph();
        var frank = new Node(Guid.NewGuid(), "person", "Frank Herbert", Start);
        graph.Add(frank);
        var person = new NodeTypeDefined(Guid.NewGuid(), Start, "person", "Person", null, null, [Born], []);
        graph.Apply(new ChangesRemoved(Guid.NewGuid(), Start.AddMinutes(1), [person.Id]));

        Assert.Equal("person", graph.FindNode(frank.Id)!.Type);

        graph.Apply(person);

        Assert.Equal("generic", graph.FindNode(frank.Id)!.Type);
        Assert.Empty(graph.TypesOf(frank.Id));
    }
}
