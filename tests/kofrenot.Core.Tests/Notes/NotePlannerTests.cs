namespace Kofrenot.Core.Tests.Notes;

public sealed class NotePlannerTests
{
    private readonly ManualClock _clock = new();
    private readonly KnowledgeGraph _graph = new();
    private readonly Dictionary<Guid, string> _contents = [];
    private readonly NotePlanner _planner;
    private readonly Node _note;

    public NotePlannerTests()
    {
        _planner = new NotePlanner(_graph, _clock);
        _note = new Node(Guid.NewGuid(), TypeCatalog.Note.Id, "Ideas", _clock.Now);
        _graph.Add(_note);
    }

    [Fact]
    public void ANoteNeverSaved_IsEmpty()
    {
        var state = Load();

        Assert.Equal(string.Empty, state.Text);
        Assert.Empty(state.Versions);
        Assert.Empty(state.Conflict);
        Assert.False(state.IsMissing);
    }

    [Fact]
    public void TheFirstSave_ReplacesNothing_ThenTheLatestVersionIsLoaded()
    {
        var first = Save("Hello", Load());

        Assert.Equal((_note.Id, _clock.Now), (first.Node, first.At));
        Assert.Empty(first.Replaces);
        Assert.Equal("Hello", Load().Text);

        var second = Save("Hello, world", Load());

        Assert.Equal([first.Id], second.Replaces);
        Assert.Equal([second], _graph.NoteVersionsOf(_note.Id));
        Assert.Equal("Hello, world", Load().Text);
    }

    [Fact]
    public void TheSameText_HasNothingToSave()
    {
        Save("Hello", Load());

        Assert.Null(_planner.Save(_note, "Hello", Load()));
        Assert.Null(_planner.Save(_note, string.Empty, new NoteState([], string.Empty, [])));
    }

    [Fact]
    public void TwoSavesFromTheSameVersion_MakeAConflict_ThatTheNextSaveResolves()
    {
        var loaded = Load();
        var mine = Save("Mine", loaded);
        _clock.Advance(TimeSpan.FromMinutes(1));
        var theirs = Save("Theirs", loaded);

        var conflict = Load();

        Assert.Equal([mine, theirs], conflict.Versions);
        Assert.Equal("Theirs", conflict.Text);
        Assert.Equal([new ConflictVersion(mine.Id, mine.At, "Mine"), new ConflictVersion(theirs.Id, theirs.At, "Theirs")], conflict.Conflict);

        var kept = _planner.Save(_note, "Theirs", conflict)!;
        _graph.Apply(kept);

        Assert.Equal([mine.Id, theirs.Id], kept.Replaces);
        Assert.Empty(Load().Conflict);
    }

    [Fact]
    public void AVersionNotSyncedYet_IsMissing_AndCannotBeSaved()
    {
        _graph.Apply(new NoteEdited(Guid.NewGuid(), _clock.Now, _note.Id, []));

        var state = Load();

        Assert.True(state.IsMissing);
        Assert.Throws<InvalidOperationException>(() => _planner.Save(_note, "Text", state));
    }

    [Fact]
    public void AVersionAppliedTwice_ChangesNothing()
    {
        var version = new NoteEdited(Guid.NewGuid(), _clock.Now, _note.Id, []);

        Assert.True(_graph.Apply(version));
        Assert.False(_graph.Apply(version));
        Assert.Empty(_graph.NoteVersionsOf(Guid.NewGuid()));
    }

    private NoteState Load() => _planner.Load(_note, id => _contents.GetValueOrDefault(id));

    private NoteEdited Save(string text, NoteState loaded)
    {
        var version = _planner.Save(_note, text, loaded)!;
        _contents[version.Id] = text;
        _graph.Apply(version);
        return version;
    }
}
