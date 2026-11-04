namespace Kofrenot.Core.Tests.Browse;

public sealed class ViewTabsTests : IDisposable
{
    private readonly TempFolder _temp = new();
    private readonly RingLog _log = new();
    private readonly Guid _vault = Guid.NewGuid();

    private string FilePath => Path.Combine(_temp.Sub("data"), "tabs.json");

    [Fact]
    public void NoFile_MeansNone() =>
        Assert.Empty(Tabs().Of(_vault));

    [Fact]
    public void OpenedViews_AreKeptInTheirOrder_EachOnce_AndInTheFile()
    {
        var (files, memos) = (Guid.NewGuid(), Guid.NewGuid());
        var tabs = Tabs();

        tabs.Open(_vault, files);
        tabs.Open(_vault, memos);
        tabs.Open(_vault, files);

        Assert.Equal([files, memos], tabs.Of(_vault));
        Assert.Equal([files, memos], Tabs().Of(_vault));
        Assert.Empty(tabs.Of(Guid.NewGuid()));
    }

    [Fact]
    public void AClosedView_IsNoLongerOpen()
    {
        var (files, memos) = (Guid.NewGuid(), Guid.NewGuid());
        var tabs = Tabs();
        tabs.Open(_vault, files);
        tabs.Open(_vault, memos);

        tabs.Close(_vault, files);
        tabs.Close(_vault, Guid.NewGuid());

        Assert.Equal([memos], Tabs().Of(_vault));
    }

    [Fact]
    public void AMovedView_GoesBeforeTheOneDroppedOn_OrLast_AndIsKept()
    {
        var (files, memos, books) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var tabs = Tabs();
        tabs.Open(_vault, files);
        tabs.Open(_vault, memos);
        tabs.Open(_vault, books);

        tabs.Move(_vault, books, before: files);
        Assert.Equal([books, files, memos], Tabs().Of(_vault));

        tabs.Move(_vault, books, before: null);
        Assert.Equal([files, memos, books], Tabs().Of(_vault));

        tabs.Move(_vault, files, before: Guid.NewGuid());
        Assert.Equal([memos, books, files], Tabs().Of(_vault));
    }

    [Fact]
    public void AViewNotOpen_OrPutWhereItIs_ChangesNothing()
    {
        var (files, memos) = (Guid.NewGuid(), Guid.NewGuid());
        var tabs = Tabs();
        tabs.Open(_vault, files);
        tabs.Open(_vault, memos);

        tabs.Move(_vault, Guid.NewGuid(), before: files);
        tabs.Move(_vault, files, before: files);
        tabs.Move(_vault, files, before: memos);

        Assert.Equal([files, memos], Tabs().Of(_vault));
    }

    [Fact]
    public void ADamagedFile_MeansNone_AndIsReported()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, "{ not json");

        Assert.Empty(Tabs().Of(_vault));
        Assert.Contains(_log.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("open tabs cannot be read"));
    }

    public void Dispose() => _temp.Dispose();

    private ViewTabs Tabs() => new(FilePath, _log);
}
