namespace Kofrenot.Core.Tests.History;

public sealed class NodeHistoryTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly KnowledgeGraph _graph = new();
    private readonly Node _dune = new(Guid.NewGuid(), TypeCatalog.Note.Id, "Dune", Start);

    public NodeHistoryTests() => _graph.Add(_dune);

    [Fact]
    public void ANewNode_HasItsName_NothingToRestore()
    {
        var entry = Assert.Single(NodeHistory.Of(_graph, _dune));

        Assert.Equal((_dune.Id, VersionKind.Name, "Dune", true, false), (entry.Id, entry.Kind, entry.Label, entry.IsCurrent, entry.CanRestore));
    }

    [Fact]
    public void EveryVersion_IsListed_NewestFirst_TheOldOnesToRestore()
    {
        var first = Apply(new NoteEdited(Guid.NewGuid(), Start.AddMinutes(1), _dune.Id, []));
        var second = Apply(new NoteEdited(Guid.NewGuid(), Start.AddMinutes(2), _dune.Id, [first.Id]));
        var renamed = Apply(new NodeRenamed(Guid.NewGuid(), Start.AddMinutes(3), _dune.Id, "Dune I", [_dune.Id]));
        var yellow = Apply(new NodeColored(Guid.NewGuid(), Start.AddMinutes(4), _dune.Id, "yellow", []));
        var born = Apply(Value(Start.AddMinutes(5), "1965", []));
        var cleared = Apply(Value(Start.AddMinutes(6), null, [born.Id]));

        var history = NodeHistory.Of(_graph, _dune);

        Assert.Equal(
            [
                (cleared.Id, VersionKind.Field, "(empty)", true),
                (yellow.Id, VersionKind.Color, "Yellow", true),
                (renamed.Id, VersionKind.Name, "Dune I", true),
                (second.Id, VersionKind.Text, string.Empty, true),
                (first.Id, VersionKind.Text, string.Empty, false),
                (_dune.Id, VersionKind.Name, "Dune", false),
            ],
            history.Where(entry => entry.Id != born.Id).Select(entry => (entry.Id, entry.Kind, entry.Label, entry.IsCurrent)));
        var value = Assert.Single(history, entry => entry.Id == born.Id);
        Assert.Equal(("Published", "1965", false, true), (value.What, value.Label, value.IsCurrent, value.CanRestore));
        Assert.Equal(["Published", "Colour", "Name", "Text", "Text", "Name"], history.Where(entry => entry.Id != born.Id).Select(entry => entry.What));
        Assert.Equal(history.Select(entry => !entry.IsCurrent), history.Select(entry => entry.CanRestore));
    }

    [Fact]
    public void EachVersionOfAConflict_IsCurrent_AndCanBeRestored()
    {
        Apply(new NodeRenamed(Guid.NewGuid(), Start.AddMinutes(1), _dune.Id, "Dune I", [_dune.Id]));
        Apply(new NodeRenamed(Guid.NewGuid(), Start.AddMinutes(2), _dune.Id, "Dune 1", [_dune.Id]));

        var names = NodeHistory.Of(_graph, _dune).Where(entry => entry.IsCurrent).ToList();

        Assert.Equal(2, names.Count);
        Assert.All(names, entry => Assert.True(entry.CanRestore));
    }

    [Fact]
    public void AFile_AView_ACheckbox_AndAnImage_SayWhatTheyHold()
    {
        Apply(new DatasetEdited(Guid.NewGuid(), Start, _dune.Id, new DatasetFile("survey.csv", 3, "00"), []));
        Apply(new ViewEdited(Guid.NewGuid(), Start, _dune.Id, new FlatDefinition(TypeCatalog.Note.Id), []));
        Apply(new FieldSet(Guid.NewGuid(), Start, _dune.Id, FieldSet.Own, "read", "Read", FieldKind.Checkbox, FieldValue.Of(true), []));
        Apply(new FieldSet(Guid.NewGuid(), Start, _dune.Id, FieldSet.Own, "cover", "Cover", FieldKind.Image, null, [], new DatasetFile("cover.png", 10, "11")));

        var history = NodeHistory.Of(_graph, _dune);
        var labels = history.Select(entry => entry.Label).ToList();

        Assert.Contains(history, entry => entry.What == "File");
        Assert.Contains(history, entry => entry.What == "Definition");
        Assert.Contains("survey.csv, 3 bytes", labels);
        Assert.Contains("A list of every note", labels, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Yes", labels);
        Assert.Contains("cover.png", labels);
    }

    private T Apply<T>(T change)
        where T : Change
    {
        Assert.True(_graph.Apply(change));
        return change;
    }

    private FieldSet Value(DateTimeOffset at, string? value, IReadOnlyList<Guid> replaces) =>
        new(Guid.NewGuid(), at, _dune.Id, FieldSet.Own, "published", "Published", FieldKind.Text, value, replaces);
}
