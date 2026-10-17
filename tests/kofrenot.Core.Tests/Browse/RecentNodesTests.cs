namespace Kofrenot.Core.Tests.Browse;

public sealed class RecentNodesTests : IDisposable
{
    private readonly TempFolder _temp = new();
    private readonly RingLog _log = new();
    private readonly Guid _vault = Guid.NewGuid();

    private string FilePath => Path.Combine(_temp.Sub("data"), "recent.json");

    [Fact]
    public void NoFile_MeansNone() =>
        Assert.Empty(Store().Of(_vault));

    [Fact]
    public void TheLatestOpened_IsFirst_EachOnce_AndKeptInTheFile()
    {
        var (dune, herbert) = (Guid.NewGuid(), Guid.NewGuid());
        var store = Store();

        store.Opened(_vault, dune);
        store.Opened(_vault, herbert);
        store.Opened(_vault, dune);

        Assert.Equal([dune, herbert], store.Of(_vault));
        Assert.Equal([dune, herbert], Store().Of(_vault));
    }

    [Fact]
    public void EachVault_HasItsOwn()
    {
        var store = Store();
        var node = Guid.NewGuid();

        store.Opened(_vault, node);

        Assert.Empty(store.Of(Guid.NewGuid()));
    }

    [Fact]
    public void AtMostMax_AreKept()
    {
        var store = Store();
        var nodes = Enumerable.Range(0, RecentNodes.Max + 2).Select(_ => Guid.NewGuid()).ToList();

        nodes.ForEach(node => store.Opened(_vault, node));

        Assert.Equal(Enumerable.Reverse(nodes).Take(RecentNodes.Max), store.Of(_vault));
    }

    [Fact]
    public void ADamagedFile_MeansNone_AndIsReported()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, "{ not json");

        Assert.Empty(Store().Of(_vault));
        Assert.Contains(_log.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("cannot be read"));
    }

    [Fact]
    public void ASaveThatFails_IsOnlyLogged()
    {
        // A folder where the file should be: it cannot be written.
        Directory.CreateDirectory(FilePath);
        var store = Store();

        store.Opened(_vault, Guid.NewGuid());

        Assert.Single(store.Of(_vault));
        Assert.Contains(_log.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("cannot be saved"));
    }

    public void Dispose() => _temp.Dispose();

    private RecentNodes Store() => new(FilePath, _log);
}
