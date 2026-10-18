namespace Kofrenot.Core.Tests.Datasets;

public sealed class DatasetPlannerTests
{
    private static readonly DatasetFile Survey = new("survey.csv", 10, new string('a', 64));
    private static readonly DatasetFile Results = new("results.csv", 20, new string('b', 64));

    private readonly ManualClock _clock = new();
    private readonly KnowledgeGraph _graph = new();
    private readonly HashSet<Guid> _files = [];
    private readonly DatasetPlanner _planner;
    private readonly Node _dataset;

    public DatasetPlannerTests()
    {
        _planner = new DatasetPlanner(_graph, _clock);
        _dataset = new Node(Guid.NewGuid(), TypeCatalog.Dataset.Id, "Survey", _clock.Now);
        _graph.Add(_dataset);
    }

    [Fact]
    public void ADatasetNeverImported_HasNoFile()
    {
        var state = Load();

        Assert.Null(state.Latest);
        Assert.Empty(state.Versions);
        Assert.Empty(state.Conflict);
        Assert.False(state.IsMissing);
    }

    [Fact]
    public void TheFirstImport_ReplacesNothing_ThenTheLatestVersionIsLoaded()
    {
        var first = Import(Survey, Load());

        Assert.Equal((_dataset.Id, _clock.Now, Survey), (first.Node, first.At, first.File));
        Assert.Empty(first.Replaces);
        Assert.Equal(first, Load().Latest);

        var second = Import(Results, Load());

        Assert.Equal([first.Id], second.Replaces);
        Assert.Equal([second], _graph.DatasetVersionsOf(_dataset.Id));
        Assert.Equal(Results, Load().Latest!.File);
    }

    [Fact]
    public void TheSameFile_HasNothingToImport_ButAnotherNameOrContentHas()
    {
        Import(Survey, Load());

        Assert.Null(_planner.Import(_dataset, Survey, Load()));
        Assert.NotNull(_planner.Import(_dataset, Survey with { Name = "other.csv" }, Load()));
        Assert.NotNull(_planner.Import(_dataset, Survey with { Sha256 = new string('c', 64) }, Load()));
    }

    [Fact]
    public void TwoImportsFromTheSameVersion_MakeAConflict_ThatKeepingOneResolves()
    {
        var loaded = Load();
        var mine = Import(Survey, loaded);
        _clock.Advance(TimeSpan.FromMinutes(1));
        var theirs = Import(Results, loaded);

        var conflict = Load();

        Assert.Equal([mine, theirs], conflict.Versions);
        Assert.Equal(theirs, conflict.Latest);
        Assert.Equal([new DatasetChoice(mine, true), new DatasetChoice(theirs, true)], conflict.Conflict);
        Assert.NotNull(_planner.Import(_dataset, Results, conflict));

        var kept = _planner.Keep(_dataset, mine, conflict);
        _graph.Apply(kept);

        Assert.Equal(Survey, kept.File);
        Assert.Equal([mine.Id, theirs.Id], kept.Replaces);
        Assert.NotEqual(mine.Id, kept.Id);
        Assert.Empty(Load().Conflict);
    }

    [Fact]
    public void OnlyAnAvailableVersionOfTheConflict_CanBeKept()
    {
        var loaded = Load();
        var mine = Import(Survey, loaded);
        var theirs = new DatasetEdited(Guid.NewGuid(), _clock.Now, _dataset.Id, Results, []);
        _graph.Apply(theirs);

        var conflict = Load();

        Assert.Equal(new DatasetChoice(theirs, false), conflict.Conflict.Single(choice => choice.Version == theirs));
        Assert.Throws<InvalidOperationException>(() => _planner.Keep(_dataset, theirs, conflict));
        Assert.Throws<InvalidOperationException>(() => _planner.Keep(_dataset, mine, Load() with { Conflict = [] }));
    }

    [Fact]
    public void AVersionNotSyncedYet_IsMissing_ButCanBeImportedOver()
    {
        var notSynced = new DatasetEdited(Guid.NewGuid(), _clock.Now, _dataset.Id, Survey, []);
        _graph.Apply(notSynced);

        var state = Load();

        Assert.True(state.IsMissing);
        Assert.Equal([notSynced.Id], _planner.Import(_dataset, Results, state)!.Replaces);
    }

    [Fact]
    public void TheFirstVersionOfANewDataset_ReplacesNothing()
    {
        var node = Guid.NewGuid();

        var first = DatasetPlanner.First(node, Survey, _clock.Now);

        Assert.Equal((node, _clock.Now, Survey), (first.Node, first.At, first.File));
        Assert.Empty(first.Replaces);
    }

    [Fact]
    public void AVersionAppliedTwice_ChangesNothing()
    {
        var version = new DatasetEdited(Guid.NewGuid(), _clock.Now, _dataset.Id, Survey, []);

        Assert.True(_graph.Apply(version));
        Assert.False(_graph.Apply(version));
        Assert.Empty(_graph.DatasetVersionsOf(Guid.NewGuid()));
        Assert.Empty(_graph.NoteVersionsOf(_dataset.Id));
    }

    private DatasetState Load() => _planner.Load(_dataset, _files.Contains);

    private DatasetEdited Import(DatasetFile file, DatasetState loaded)
    {
        var version = _planner.Import(_dataset, file, loaded)!;
        _files.Add(version.Id);
        _graph.Apply(version);
        return version;
    }
}
