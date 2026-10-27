namespace Kofrenot.Core.Tests.Graph;

public sealed class CustomNodeTypesTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly FieldDefinition Born = new("born", "Born", FieldKind.Date);

    private readonly KnowledgeGraph _graph = new();

    private static NodeTypeDefined Define(string type, string name, int minutes, IReadOnlyList<FieldDefinition>? fields = null, params Guid[] replaces) =>
        new(Guid.NewGuid(), Start.AddMinutes(minutes), type, name, "Contact", "blue", fields ?? [Born], replaces);

    [Fact]
    public void AVault_HasTheBuiltInTypesOnly_UntilOneIsDefined()
    {
        Assert.Empty(_graph.NodeTypes.All);
        Assert.Same(TypeCatalog.BuiltIn, _graph.Types);
    }

    [Fact]
    public void ATypeDefined_JoinsTheCatalog_AfterTheBuiltInOnes()
    {
        var person = Define("person", "Person", 1);

        Assert.True(_graph.Apply(person));
        Assert.False(_graph.Apply(person));

        var type = Assert.Single(_graph.NodeTypes.All).Type;
        Assert.Equal(("person", "Person", "Contact", NodeColor.Blue), (type.Id, type.Name, type.Icon, type.Color));
        Assert.Equal([Born], type.Fields);
        Assert.False(type.HasContent);
        Assert.Equal([.. TypeCatalog.BuiltIn.NodeTypes, type], _graph.Types.NodeTypes);
        Assert.Equal("Person", _graph.Types.NodeTypeName("person"));
        Assert.Equal(TypeCatalog.BuiltIn.LinkTypes, _graph.Types.LinkTypes);
        Assert.Same(_graph.Types, _graph.Types);
        Assert.Same(_graph.NodeTypes.All[0], _graph.NodeTypes.Find("person"));
        Assert.Null(_graph.NodeTypes.Find("author"));
    }

    [Fact]
    public void TheCatalog_KeepsTheUsersLinkTypesAndAliases()
    {
        _graph.Apply(Define("person", "Person", 1));
        _graph.Apply(new LinkTypeDefined(Guid.NewGuid(), Start.AddMinutes(2), "wrote", "wrote", "written by", []));
        _graph.Apply(new AliasAdded(Guid.NewGuid(), Start.AddMinutes(3), "contains", false, "holds"));

        Assert.NotNull(_graph.Types.FindLinkType("wrote"));
        Assert.Contains("holds", _graph.Types.FindLinkType("contains")!.Phrases);
        Assert.NotNull(_graph.Types.FindNodeType("person"));
    }

    [Fact]
    public void ALaterDefinition_ChangesTheType()
    {
        var person = Define("person", "Person", 1);
        _graph.Apply(person);
        var died = new FieldDefinition("died", "Died", FieldKind.Date);

        _graph.Apply(Define("person", "Human", 2, [Born, died], person.Id));

        var custom = Assert.Single(_graph.NodeTypes.All);
        Assert.Equal("Human", custom.Type.Name);
        Assert.Equal([Born, died], custom.Type.Fields);
        Assert.False(custom.IsConflict);
        Assert.Equal(2, custom.Ids.Count);
    }

    [Fact]
    public void DefinitionsMadeApart_AreAConflict_ShowingTheLatest()
    {
        var person = Define("person", "Person", 1);
        _graph.Apply(person);
        _graph.Apply(Define("person", "Human", 3, null, person.Id));
        _graph.Apply(Define("person", "People", 2, null, person.Id));

        var custom = Assert.Single(_graph.NodeTypes.All);
        Assert.True(custom.IsConflict);
        Assert.Equal(["People", "Human"], custom.Definitions.Select(definition => definition.Name));
        Assert.Equal("Human", custom.Type.Name);
    }

    [Fact]
    public void Types_AreListedOldestFirst()
    {
        _graph.Apply(Define("person", "Person", 2));
        _graph.Apply(Define("book", "Book", 1));

        Assert.Equal(["book", "person"], _graph.NodeTypes.All.Select(custom => custom.Type.Id));
    }

    [Fact]
    public void ARemovedType_LeavesTheCatalog()
    {
        var person = Define("person", "Person", 1);
        _graph.Apply(person);
        var catalog = _graph.Types;

        _graph.Apply(new ChangesRemoved(Guid.NewGuid(), Start.AddMinutes(2), [person.Id]));

        Assert.Empty(_graph.NodeTypes.All);
        Assert.NotSame(catalog, _graph.Types);
        Assert.Null(_graph.Types.FindNodeType("person"));
    }

    [Fact]
    public void ADefinitionOfABuiltInType_IsIgnored()
    {
        _graph.Apply(Define("note", "My note", 1));

        Assert.Empty(_graph.NodeTypes.All);
        Assert.Equal("Note", _graph.Types.NodeTypeName("note"));
    }

    [Fact]
    public void AnUnknownColour_IsNone()
    {
        _graph.Apply(new NodeTypeDefined(Guid.NewGuid(), Start, "person", "Person", null, "ultraviolet", [], []));

        Assert.Equal(NodeColor.None, _graph.NodeTypes.All[0].Type.Color);
    }

    [Fact]
    public void TheBuiltInTypesWithContent_AreNoteDatasetAndView()
    {
        Assert.Equal(["note", "dataset", "view"], TypeCatalog.BuiltIn.NodeTypes.Where(type => type.HasContent).Select(type => type.Id));
        Assert.Equal(FieldKind.Markdown, Assert.Single(TypeCatalog.Note.Fields).Kind);
    }

    [Fact]
    public void NodeTypes_AreEqualByTheirId()
    {
        Assert.Equal(new NodeType("person", "Person"), new NodeType("person", "Human", [Born]));
        Assert.Equal(new NodeType("person", "Person").GetHashCode(), new NodeType("person", "Human").GetHashCode());
        Assert.NotEqual(new NodeType("person", "Person"), new NodeType("author", "Person"));
    }

    [Fact]
    public void FieldDefinitions_AreEqualByTheirParts()
    {
        Assert.Equal(new FieldDefinition("f", "Format", FieldKind.Choice, ["Novel"]), new FieldDefinition("f", "Format", FieldKind.Choice, ["Novel"]));
        Assert.Equal(new FieldDefinition("f", "Format", FieldKind.Choice, ["Novel"]).GetHashCode(), new FieldDefinition("f", "Format", FieldKind.Choice, ["Novel"]).GetHashCode());
        Assert.NotEqual(new FieldDefinition("f", "Format", FieldKind.Choice, ["Novel"]), new FieldDefinition("f", "Format", FieldKind.Choice, ["Poem"]));
        Assert.False(Born.Equals(null));
    }
}
