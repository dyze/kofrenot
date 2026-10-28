namespace Kofrenot.Core.Tests.History;

public sealed class RestorePlannerTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly ManualClock _clock = new() { Now = Start.AddHours(1) };
    private readonly KnowledgeGraph _graph = new();
    private readonly Node _dune = new(Guid.NewGuid(), TypeCatalog.Note.Id, "Dune", Start);
    private readonly RestorePlanner _planner;

    public RestorePlannerTests()
    {
        _graph.Add(_dune);
        _planner = new RestorePlanner(_graph, _clock);
    }

    [Fact]
    public void TheOnlyCurrentVersion_IsNotRestored() =>
        Assert.Null(_planner.Plan(_dune, Entry(_dune.Id)));

    [Fact]
    public void AName_IsGivenAgain_OverTheCurrentOne()
    {
        var renamed = Apply(new NodeRenamed(Guid.NewGuid(), Start.AddMinutes(1), _dune.Id, "Dune I", [_dune.Id]));

        var plan = Restore(_dune.Id);

        var change = Assert.IsType<NodeRenamed>(Assert.Single(plan.Changes));
        Assert.Equal(("Dune", _clock.Now), (change.Name, change.At));
        Assert.Equal([renamed.Id], change.Replaces);
        Assert.Empty(plan.Contents);
        Assert.Equal("Dune", _graph.FindNode(_dune.Id)!.Name);
    }

    [Fact]
    public void AColour_IsGivenAgain()
    {
        var yellow = Apply(new NodeColored(Guid.NewGuid(), Start.AddMinutes(1), _dune.Id, "yellow", []));
        var blue = Apply(new NodeColored(Guid.NewGuid(), Start.AddMinutes(2), _dune.Id, "blue", [yellow.Id]));

        var change = Assert.IsType<NodeColored>(Assert.Single(Restore(yellow.Id).Changes));

        Assert.Equal("yellow", change.Color);
        Assert.Equal([blue.Id], change.Replaces);
        Assert.Equal(NodeColor.Yellow, _graph.ColorOf(_dune.Id));
    }

    [Fact]
    public void AFieldValue_IsSetAgain_OverItsFieldsCurrentValue()
    {
        var first = Apply(new FieldSet(Guid.NewGuid(), Start.AddMinutes(1), _dune.Id, FieldSet.Own, "published", "Published", FieldKind.Text, "1965", []));
        var second = Apply(first with { Id = Guid.NewGuid(), At = Start.AddMinutes(2), Value = "1966", Replaces = [first.Id] });
        Apply(first with { Id = Guid.NewGuid(), Field = "pages", Name = "Pages", Value = "412" });

        var change = Assert.IsType<FieldSet>(Assert.Single(Restore(first.Id).Changes));

        Assert.Equal(("published", "1965", (Guid?)null), (change.Field, change.Value, change.Content));
        Assert.Equal([second.Id], change.Replaces);
        Assert.Equal("1965", Assert.Single(_graph.FieldsOf(_dune.Id), field => field.Field == "published").Shown.Value);
    }

    [Fact]
    public void AnImage_KeepsTheFileItWasWrittenWith()
    {
        var image = Apply(new FieldSet(Guid.NewGuid(), Start.AddMinutes(1), _dune.Id, FieldSet.Own, "cover", "Cover", FieldKind.Image, null, [], new DatasetFile("cover.png", 10, "11")));
        Apply(image with { Id = Guid.NewGuid(), At = Start.AddMinutes(2), File = null, Replaces = [image.Id] });

        var change = Assert.IsType<FieldSet>(Assert.Single(Restore(image.Id).Changes));

        Assert.Equal((image.File, image.Id), (change.File, change.Content));
        Assert.Equal(image.Id, change.Image!.Id);
    }

    [Fact]
    public void ANotesText_IsWrittenAgain_AsANewVersion()
    {
        var first = Apply(new NoteEdited(Guid.NewGuid(), Start.AddMinutes(1), _dune.Id, []));
        var second = Apply(new NoteEdited(Guid.NewGuid(), Start.AddMinutes(2), _dune.Id, [first.Id]));

        var plan = Restore(first.Id);

        var change = Assert.IsType<NoteEdited>(Assert.Single(plan.Changes));
        Assert.Equal([second.Id], change.Replaces);
        Assert.Equal(new ContentCopy(first, change), Assert.Single(plan.Contents));
    }

    [Fact]
    public void ADatasetsFile_IsWrittenAgain_WithItsDescription()
    {
        var first = Apply(new DatasetEdited(Guid.NewGuid(), Start.AddMinutes(1), _dune.Id, new DatasetFile("a.csv", 3, "00"), []));
        var second = Apply(new DatasetEdited(Guid.NewGuid(), Start.AddMinutes(2), _dune.Id, new DatasetFile("b.csv", 4, "11"), [first.Id]));

        var plan = Restore(first.Id);

        var change = Assert.IsType<DatasetEdited>(Assert.Single(plan.Changes));
        Assert.Equal(first.File, change.File);
        Assert.Equal([second.Id], change.Replaces);
        Assert.Equal(first, Assert.Single(plan.Contents).From);
    }

    [Fact]
    public void AViewsDefinition_IsWrittenAgain()
    {
        var first = Apply(new ViewEdited(Guid.NewGuid(), Start.AddMinutes(1), _dune.Id, new FlatDefinition(TypeCatalog.Note.Id), []));
        Apply(new ViewEdited(Guid.NewGuid(), Start.AddMinutes(2), _dune.Id, new FlatDefinition(TypeCatalog.Dataset.Id), [first.Id]));

        var plan = Restore(first.Id);

        Assert.Empty(plan.Contents);
        Assert.Equal(first.Definition, Assert.Single(_graph.ViewVersionsOf(_dune.Id)).Definition);
    }

    [Fact]
    public void AVersionOfAConflict_Restored_ResolvesIt()
    {
        var mine = Apply(new NodeRenamed(Guid.NewGuid(), Start.AddMinutes(1), _dune.Id, "Dune I", [_dune.Id]));
        var theirs = Apply(new NodeRenamed(Guid.NewGuid(), Start.AddMinutes(2), _dune.Id, "Dune 1", [_dune.Id]));

        var change = Assert.IsType<NodeRenamed>(Assert.Single(Restore(mine.Id).Changes));

        Assert.Equal([mine.Id, theirs.Id], change.Replaces);
        Assert.Equal("Dune I", Assert.Single(_graph.NamesOf(_dune.Id)).Name);
    }

    // Plans the restore of a version, and applies it.
    private RestorePlan Restore(Guid version)
    {
        var plan = _planner.Plan(_dune, Entry(version))!;
        Assert.All(plan.Changes, change => Assert.True(_graph.Apply(change)));
        return plan;
    }

    private HistoryEntry Entry(Guid version) => Assert.Single(NodeHistory.Of(_graph, _dune), entry => entry.Id == version);

    private T Apply<T>(T change)
        where T : Change
    {
        Assert.True(_graph.Apply(change));
        return change;
    }
}
