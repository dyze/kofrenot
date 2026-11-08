namespace Kofrenot.Core.Tests.Imports;

public sealed class MappingPlannerTests
{
    private readonly BookVault _vault = new();
    private readonly ManualClock _clock = new();
    private readonly MappingPlanner _planner;

    public MappingPlannerTests() => _planner = new MappingPlanner(_vault.Graph, _clock);

    [Fact]
    public void AMappingAdded_IsANode_WithItsFirstVersion_ListedByName()
    {
        Apply(_planner.Add(" Films (JSON) ", BookVault.Books with { NodeType = "film" }));
        var changes = _planner.Add("Books (JSON)", BookVault.Books);
        Apply(changes);
        _vault.Add("Dune");

        var mappings = _planner.Mappings;
        var state = _planner.Load(mappings[0]);

        Assert.Equal(["Books (JSON)", "Films (JSON)"], mappings.Select(mapping => mapping.Name));
        Assert.Equal(TypeCatalog.Mapping.Id, mappings[0].Type);
        Assert.Equal((BookVault.Books, false), (state.Definition, state.IsConflict));
        Assert.Equal([], Assert.IsType<MappingEdited>(changes[1]).Replaces);
    }

    [Fact]
    public void TheRules_AreSuggestedByName_ForTheSmallFieldsOnly()
    {
        var book = _vault.Graph.Types.FindNodeType("book")!;

        var rules = MappingPlanner.Suggest(book, ["title", "ISBN", "publisher.publisher", "published", "cover", "isbn"]);

        Assert.Equal(
            [new FieldRule("ISBN", BookVault.Isbn.Id), new FieldRule("published", BookVault.Published.Id), new FieldRule("publisher.publisher", BookVault.Publisher.Id)],
            rules);
    }

    [Fact]
    public void AMappingWithoutVersion_HasNoDefinition() =>
        Assert.Null(_planner.Load(_vault.Add("Books", TypeCatalog.Mapping.Id)).Definition);

    [Fact]
    public void NothingChanged_SavesNothing_ARenameOrADefinitionSavesIt()
    {
        Apply(_planner.Add("Books", BookVault.Books));
        var mapping = _planner.Mappings[0];
        var loaded = _planner.Load(mapping);

        Assert.Empty(_planner.Save(mapping, "Books ", BookVault.Books, loaded));
        var renamed = Assert.IsType<NodeRenamed>(Assert.Single(_planner.Save(mapping, "Books (JSON)", BookVault.Books, loaded)));
        var edited = Assert.IsType<MappingEdited>(Assert.Single(_planner.Save(mapping, "Books", BookVault.Books with { Match = null }, loaded)));

        Assert.Equal("Books (JSON)", renamed.Name);
        Assert.Equal([mapping.Id], renamed.Replaces);
        Assert.Equal([loaded.Versions[0].Id], edited.Replaces);
    }

    [Fact]
    public void AConflict_IsResolvedBySaving_EvenTheSameDefinition()
    {
        Apply(_planner.Add("Books", BookVault.Books));
        var mapping = _planner.Mappings[0];
        _vault.Graph.Apply(new MappingEdited(Guid.NewGuid(), BookVault.Start, mapping.Id, BookVault.Books with { Match = null }, []));
        var loaded = _planner.Load(mapping);

        var saved = Assert.IsType<MappingEdited>(Assert.Single(_planner.Save(mapping, "Books", loaded.Definition!, loaded)));

        Assert.True(loaded.IsConflict);
        Assert.Equal(2, saved.Replaces.Count);
    }

    private void Apply(IEnumerable<Change> changes)
    {
        foreach (var change in changes)
        {
            _vault.Graph.Apply(change);
        }
    }
}
