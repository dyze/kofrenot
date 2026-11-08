namespace Kofrenot.Core.Tests.Graph;

public sealed class MappingVersionsTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 8, 9, 0, 0, TimeSpan.Zero);

    private static readonly MappingDefinition Books = new(MappingDefinition.Json, "book", ["title"], null, [], [], []);

    private readonly KnowledgeGraph _graph = new();
    private readonly Node _mapping = new(Guid.NewGuid(), TypeCatalog.Mapping.Id, "Books (JSON)", Start);

    public MappingVersionsTests() => _graph.Apply(new NodeAdded(Guid.NewGuid(), Start, _mapping));

    [Fact]
    public void AMappingVersion_ReplacesTheOneBefore_AndBothStayInItsHistory()
    {
        var first = Edit(Books, []);
        var second = Edit(Books with { NameKeys = ["title_fr", "title"] }, [first.Id]);

        Assert.Equal([second], _graph.MappingVersionsOf(_mapping.Id));
        Assert.Equal([first, second], _graph.ContentHistoryOf(_mapping.Id).OrderBy(version => version.At));
        Assert.False(_graph.Apply(second));
    }

    [Fact]
    public void ItsHistory_ListsEachDefinition_AndRestoresAnOldOne()
    {
        var first = Edit(Books, []);
        Edit(Books with { NameKeys = ["title_fr"] }, [first.Id]);

        var entries = NodeHistory.Of(_graph, _mapping).Where(entry => entry.Kind == VersionKind.View).ToList();
        var old = Assert.Single(entries, entry => entry.Id == first.Id);
        var restored = Assert.IsType<MappingEdited>(Assert.Single(new RestorePlanner(_graph).Plan(_mapping, old)!.Changes));

        Assert.Equal(2, entries.Count);
        Assert.Equal(("Definition", "Records as book"), (old.What, old.Label));
        Assert.Equal(Books, restored.Definition);
        Assert.Equal([entries[0].Id], restored.Replaces);
    }

    [Fact]
    public void AMapping_HasTheImportIcon() =>
        Assert.Equal("import", IconPaths.NameOf(NodeVisual.Of(_graph, _mapping)));

    private MappingEdited Edit(MappingDefinition definition, IReadOnlyList<Guid> replaces)
    {
        var version = new MappingEdited(Guid.NewGuid(), Start.AddMinutes(_graph.ContentHistoryOf(_mapping.Id).Count() + 1), _mapping.Id, definition, replaces);
        _graph.Apply(version);
        return version;
    }
}
