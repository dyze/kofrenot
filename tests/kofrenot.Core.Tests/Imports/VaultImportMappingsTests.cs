namespace Kofrenot.Core.Tests.Imports;

/// <summary>Mappings imported from another vault: their definition, and the types it names, copied.</summary>
public sealed class VaultImportMappingsTests
{
    private static readonly MappingDefinition Books = new(
        MappingDefinition.Json, "book", ["title"], "published", [], [new FieldRule("year", "published")], [new LinkRule("authors", "person", "wrote", IsReverse: true)]);

    private readonly TwoVaults _vaults = new();

    public VaultImportMappingsTests()
    {
        _vaults.DefineBook(Source);
        Source.Apply(new NodeTypeDefined(Guid.NewGuid(), TwoVaults.Start, "person", "Person", null, null, [], []));
        Source.Apply(new LinkTypeDefined(Guid.NewGuid(), TwoVaults.Start, "wrote", "wrote", "written by", [], []));
    }

    private KnowledgeGraph Source => _vaults.Source;

    private KnowledgeGraph Target => _vaults.Target;

    [Fact]
    public void AMapping_IsAdded_WithItsDefinition_AndTheTypesItNames()
    {
        var mapping = AddMapping(Books);

        var plan = _vaults.Plan([mapping]);
        _vaults.Write(plan);

        Assert.Equal(["Mapping", "definition"], Assert.Single(plan.Of(RecordOutcome.New)).Details);
        Assert.Equal(["Also adds node types Book, Person", "Also adds link type “wrote”"], plan.Summary);
        Assert.Equal(Books, Assert.Single(Target.MappingVersionsOf(mapping.Id)).Definition);
        Assert.Equal(("Book", "Person"), (Target.Types.NodeTypeName("book"), Target.Types.NodeTypeName("person")));
        Assert.NotNull(Target.Types.FindLinkType("wrote"));
        Assert.Contains(Target.Types.FindNodeType("book")!.Fields, field => field.Id == Books.Match);
    }

    [Fact]
    public void AMappingImportedAgain_IsUnchanged_ThenGetsItsNewDefinition()
    {
        var mapping = AddMapping(Books);
        _vaults.Write(_vaults.Plan([mapping]));
        Assert.Single(_vaults.Plan([mapping]).Of(RecordOutcome.Unchanged));

        var edited = Books with { NameKeys = ["title_fr", "title"] };
        Source.Apply(new MappingEdited(Guid.NewGuid(), TwoVaults.Start, mapping.Id, edited, Source.MappingVersionsOf(mapping.Id).Select(version => version.Id).ToList()));
        var plan = _vaults.Plan([mapping]);
        _vaults.Write(plan);

        Assert.Equal(["definition changed"], Assert.Single(plan.Of(RecordOutcome.Updated)).Details);
        Assert.Equal(edited, Assert.Single(Target.MappingVersionsOf(mapping.Id)).Definition);
    }

    [Fact]
    public void AMappingSkipped_CopiesNoType()
    {
        var mapping = AddMapping(Books);

        var write = _vaults.Plan([mapping]).WriteFor(_ => RecordChoice.Skip);

        Assert.Empty(write.Changes);
    }

    [Fact]
    public void AMapping_WhoseTypeOrLinkTypeHasANameTakenHere_IsNotImported()
    {
        Target.Apply(new NodeTypeDefined(Guid.NewGuid(), TwoVaults.Start, "personne", "Person", null, null, [], []));
        var byPerson = AddMapping(Books);
        var byWrote = AddMapping(Books with { Links = [new LinkRule("authors", "book", "wrote", IsReverse: true)] }, "Other books");
        Target.Apply(new LinkTypeDefined(Guid.NewGuid(), TwoVaults.Start, "authored", "wrote", null, [], []));

        var plan = _vaults.Plan([byPerson, byWrote]);

        Assert.Equal(
            ["its type “Person” has the name of another type here: rename one first", "its link type “wrote” has a phrase of another link type here: rename one first"],
            plan.Items.Select(item => item.Reason));
    }

    private Node AddMapping(MappingDefinition definition, string name = "Books (JSON)")
    {
        var mapping = TwoVaults.Add(Source, name, TypeCatalog.Mapping.Id);
        Source.Apply(new MappingEdited(Guid.NewGuid(), TwoVaults.Start, mapping.Id, definition, []));
        return mapping;
    }
}
