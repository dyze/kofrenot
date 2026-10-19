namespace Kofrenot.Core.Tests.Browse;

public sealed class ViewStatesTests : IDisposable
{
    private readonly TempFolder _temp = new();
    private readonly RingLog _log = new();
    private readonly Guid _vault = Guid.NewGuid();
    private readonly Guid _view = Guid.NewGuid();

    private string FilePath => Path.Combine(_temp.Sub("data"), "view-states.json");

    [Fact]
    public void NoFile_MeansNothingExpanded() =>
        Assert.Empty(States().ExpandedIn(_vault, _view));

    [Fact]
    public void ExpandedNodes_AreKeptPerView_AndInTheFile()
    {
        var states = States();

        states.SetExpanded(_vault, _view, "a", isExpanded: true);
        states.SetExpanded(_vault, _view, "a/b", isExpanded: true);

        Assert.Equal(["a", "a/b"], States().ExpandedIn(_vault, _view).Order());
        Assert.Empty(states.ExpandedIn(_vault, Guid.NewGuid()));
        Assert.Empty(states.ExpandedIn(Guid.NewGuid(), _view));
    }

    [Fact]
    public void ACollapsedNode_IsNoLongerExpanded()
    {
        var states = States();
        states.SetExpanded(_vault, _view, "a", isExpanded: true);

        states.SetExpanded(_vault, _view, "a", isExpanded: false);
        states.SetExpanded(_vault, _view, "never", isExpanded: false);

        Assert.Empty(States().ExpandedIn(_vault, _view));
    }

    [Fact]
    public void ADamagedFile_MeansNothingExpanded_AndIsReported()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, "{ not json");

        Assert.Empty(States().ExpandedIn(_vault, _view));
        Assert.Contains(_log.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("view states cannot be read"));
    }

    [Fact]
    public void ASaveThatFails_IsOnlyLogged()
    {
        // A folder where the file should be: it cannot be written.
        Directory.CreateDirectory(FilePath);
        var states = States();

        states.SetExpanded(_vault, _view, "a", isExpanded: true);

        Assert.Single(states.ExpandedIn(_vault, _view));
        Assert.Contains(_log.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("view states cannot be saved"));
    }

    public void Dispose() => _temp.Dispose();

    private ViewStates States() => new(FilePath, _log);
}
