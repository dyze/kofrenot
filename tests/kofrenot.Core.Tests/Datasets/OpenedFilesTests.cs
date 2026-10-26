using Kofrenot.Core.Datasets;
using Kofrenot.Core.Logging;

namespace Kofrenot.Core.Tests.Datasets;

public sealed class OpenedFilesTests : IDisposable
{
    private readonly TempFolder _temp = new();
    private readonly RingLog _log = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void EachFile_GetsAFolderOfItsOwn_KeepingItsName()
    {
        var opened = new OpenedFiles(_temp.Sub("open"));

        var first = opened.NewPath("report.pdf");
        var second = opened.NewPath(@"..\report.pdf");

        Assert.Equal("report.pdf", Path.GetFileName(first));
        Assert.Equal("report.pdf", Path.GetFileName(second));
        Assert.NotEqual(Path.GetDirectoryName(first), Path.GetDirectoryName(second));
        Assert.True(Directory.Exists(Path.GetDirectoryName(first)));
        Assert.StartsWith(_temp.Sub("open"), second);
    }

    [Fact]
    public void Clear_DeletesTheFilesOpenedBefore()
    {
        var opened = new OpenedFiles(_temp.Sub("open"));
        File.WriteAllText(opened.NewPath("a.txt"), "a");
        File.WriteAllText(opened.NewPath("b.txt"), "b");

        opened.Clear();

        Assert.Empty(Directory.GetFileSystemEntries(_temp.Sub("open")));
    }

    [Fact]
    public void Clear_WithNothingOpened_DoesNothing()
    {
        new OpenedFiles(_temp.Sub("open")).Clear();

        Assert.False(Directory.Exists(_temp.Sub("open")));
    }

    [Fact]
    public void AFileStillInUse_StaysUntilTheNextStart_AndIsLogged()
    {
        var opened = new OpenedFiles(_temp.Sub("open"), _log);
        var inUse = opened.NewPath("a.txt");
        File.WriteAllText(opened.NewPath("b.txt"), "b");

        using (new FileStream(inUse, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            opened.Clear();
        }

        Assert.Equal(Path.GetDirectoryName(inUse), Assert.Single(Directory.GetDirectories(_temp.Sub("open"))));
        Assert.Contains(_log.Entries, entry => entry.Level == LogLevel.Warning);
    }
}
