using System.Text;

namespace Kofrenot.Core.Tests.Server;

public sealed class ServerSyncTests : IDisposable
{
    private readonly TempFolder _temp = new();
    private readonly RingLog _log = new();
    private readonly FakeStore _store = new();

    private string Mirror => _temp.Sub("mirror");

    private string State => _temp.Sub("mirror.sync.json");

    public void Dispose() => _temp.Dispose();

    [Fact]
    public async Task TheStoresFiles_AreDownloaded_Once()
    {
        _store.Add("kofrenot-vault.json", "{}");
        _store.Add("changes/ab/ab12.kfc", "change");

        var first = await Sync().RunAsync();
        var second = await Sync().RunAsync();

        Assert.Equal(new SyncResult(2, 0), first);
        Assert.Equal(new SyncResult(0, 0), second);
        Assert.Equal("change", File.ReadAllText(Path.Combine(Mirror, "changes", "ab", "ab12.kfc")));
        Assert.Contains(_log.Entries, entry => entry.Level == LogLevel.Info && entry.Message.Contains("2 files downloaded"));
    }

    [Fact]
    public async Task TheFeed_IsReadPageByPage()
    {
        _store.PageSize = 2;
        foreach (var name in new[] { "a", "b", "c", "d", "e" })
        {
            _store.Add(name, name);
        }

        var result = await Sync().RunAsync();

        Assert.Equal(5, result.Downloaded);
        Assert.Equal([0L, 2L, 4L], _store.FeedCalls);
    }

    [Fact]
    public async Task TheMirrorsFiles_AreUploaded_HeaderAndKeysFirst_ChangesLast()
    {
        Write("changes/ab/ab12.kfc", "change");
        Write("content/cd/cd34.kfn", "content");
        Write("keys/epoch-0/ef56.kfw", "key");
        Write("kofrenot-vault.json", "{}");

        var first = await Sync().RunAsync();
        var second = await Sync().RunAsync();

        Assert.Equal(new SyncResult(0, 4), first);
        Assert.Equal(new SyncResult(0, 0), second);
        Assert.Equal(["kofrenot-vault.json", "keys/epoch-0/ef56.kfw", "content/cd/cd34.kfn", "changes/ab/ab12.kfc"], _store.Uploads);
    }

    [Fact]
    public async Task FilesBeingWritten_OrNotOfAVault_StayInTheMirror()
    {
        Write("changes/ab/ab12.kfc.tmp", "half");
        Write("desktop ini", "x");

        Assert.Equal(new SyncResult(0, 0), await Sync().RunAsync());
        Assert.Equal(0, ServerSync.Waiting(Mirror, State));
    }

    [Fact]
    public async Task AFileTheStoreHasAlready_IsNotCounted()
    {
        Write("a", "a");
        _store.Refuse("a");

        Assert.Equal(new SyncResult(0, 0), await Sync().RunAsync());
        Assert.Equal(0, ServerSync.Waiting(Mirror, State));
    }

    [Fact]
    public async Task AFileInBoth_IsNeitherDownloadedNorUploaded()
    {
        _store.Add("a", "a");
        Write("a", "a");

        Assert.Equal(new SyncResult(0, 0), await Sync().RunAsync());
        Assert.Empty(_store.Uploads);
    }

    [Fact]
    public async Task APathOutsideTheMirror_IsSkipped()
    {
        _store.Add("../outside", "x");
        _store.Add("a", "a");

        var result = await Sync().RunAsync();

        Assert.Equal(1, result.Downloaded);
        Assert.False(File.Exists(_temp.Sub("outside")));
        Assert.Contains(_log.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("skipped"));
    }

    [Fact]
    public async Task AFailedDownload_LeavesNoFile_AndIsTriedAgain()
    {
        _store.Add("a", "a");
        _store.FailDownloads = true;

        await Assert.ThrowsAsync<ServerException>(() => Sync().RunAsync());
        Assert.Empty(Directory.GetFiles(Mirror, "*", SearchOption.AllDirectories));

        _store.FailDownloads = false;
        Assert.Equal(1, (await Sync().RunAsync()).Downloaded);
    }

    [Fact]
    public async Task FilesWrittenOffline_Wait_AndAreUploadedLater()
    {
        Write("a", "a");
        Write("b", "b");
        _store.FailUploadsAfter = 1;

        await Assert.ThrowsAsync<ServerException>(() => Sync().RunAsync());
        Assert.Equal(1, ServerSync.Waiting(Mirror, State));

        _store.FailUploadsAfter = null;
        Assert.Equal(new SyncResult(0, 1), await Sync().RunAsync());
        Assert.Equal(["a", "b"], _store.Uploads);
    }

    [Fact]
    public async Task ManyFiles_AreAllUploaded()
    {
        for (var index = 0; index < 120; index++)
        {
            Write($"f{index:000}", "x");
        }

        Assert.Equal(120, (await Sync().RunAsync()).Uploaded);
        Assert.Equal(0, ServerSync.Waiting(Mirror, State));
    }

    [Fact]
    public async Task ADamagedState_CostsAFullCheck_NotACopy()
    {
        _store.Add("a", "a");
        await Sync().RunAsync();
        File.WriteAllText(State, "{damaged");

        Assert.Equal(new SyncResult(0, 0), await Sync().RunAsync());
        Assert.Empty(_store.Uploads);
    }

    [Fact]
    public void Waiting_WithoutAMirror_IsNothing() => Assert.Equal(0, ServerSync.Waiting(Mirror, State));

    [Fact]
    public void ASync_NamesItsStoreAndMirror()
    {
        var sync = Sync();

        Assert.Equal((FakeStore.Id, Mirror), (sync.Store, sync.Mirror));
    }

    [Fact]
    public async Task AnUpload_NamesTheEpochOfItsFile_OrTheMirrorsLatest()
    {
        Directory.CreateDirectory(Path.Combine(Mirror, "keys", "epoch-2"));
        File.WriteAllBytes(Path.Combine(Mirror, "a.kfc"), [.. "KFC2"u8, 0, 0, 0, 1, 9, 9]);
        Write("kofrenot-vault.json", "{}");

        await Sync().RunAsync();

        Assert.Equal(1, _store.Fences["a.kfc"]);
        Assert.Equal(2, _store.Fences["kofrenot-vault.json"]);
    }

    [Fact]
    public async Task AFileBelowTheFence_WaitsToBeSealedAgain_ThenIsUploaded()
    {
        _store.Fence = 2;
        File.WriteAllBytes(Path.Combine(Directory.CreateDirectory(Mirror).FullName, "a.kfc"), [.. "KFC2"u8, 0, 0, 0, 1, 9, 9]);

        var refused = await Sync().RunAsync();

        Assert.Equal(new SyncResult(0, 0, 1), refused);
        Assert.Equal(["a.kfc"], ServerSync.Fenced(State));
        Assert.Equal(1, ServerSync.Waiting(Mirror, State));
        Assert.Contains(_log.Entries, entry => entry.Message.Contains("to seal again"));

        File.WriteAllBytes(Path.Combine(Mirror, "a.kfc"), [.. "KFC2"u8, 0, 0, 0, 2, 9, 9]);
        Assert.Equal(new SyncResult(0, 1, 0), await Sync().RunAsync());
        Assert.Empty(ServerSync.Fenced(State));
    }

    private ServerSync Sync() => new(Mirror, State, FakeStore.Id, _store, _log);

    private void Write(string path, string text)
    {
        var file = Path.Combine([Mirror, .. path.Split('/')]);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, text);
    }

    /// <summary>A store in memory: its feed, and what was uploaded.</summary>
    private sealed class FakeStore : IStoreFiles
    {
        public static readonly Guid Id = Guid.NewGuid();

        private readonly List<(string Path, byte[] Content)> _files = [];
        private readonly HashSet<string> _refused = [];

        public int PageSize { get; set; } = 1000;

        public bool FailDownloads { get; set; }

        public int? FailUploadsAfter { get; set; }

        public List<long> FeedCalls { get; } = [];

        public List<string> Uploads { get; } = [];

        public void Add(string path, string text) => _files.Add((path, Encoding.UTF8.GetBytes(text)));

        public void Refuse(string path) => _refused.Add(path);

        public Task<FeedPage> FeedAsync(Guid store, long since, CancellationToken cancel = default)
        {
            Assert.Equal(Id, store);
            FeedCalls.Add(since);
            var after = _files.Select((file, index) => new FeedEntry(index + 1, file.Path, file.Content.Length)).Where(entry => entry.Seq > since).ToList();
            return Task.FromResult(new FeedPage(after.Take(PageSize).ToList(), after.Count > PageSize));
        }

        public async Task DownloadAsync(Guid store, string path, Stream destination, CancellationToken cancel = default)
        {
            await destination.WriteAsync(Encoding.UTF8.GetBytes("partial"), cancel);
            if (FailDownloads)
            {
                throw new ServerException(ServerFailure.Offline);
            }

            destination.SetLength(0);
            await destination.WriteAsync(_files.Single(file => file.Path == path).Content, cancel);
        }

        public Dictionary<string, long> Fences { get; } = [];

        public long? Fence { get; set; }

        public Task<UploadResult> UploadAsync(Guid store, string path, Func<Stream> open, long fence, CancellationToken cancel = default)
        {
            if (Uploads.Count == FailUploadsAfter)
            {
                throw new ServerException(ServerFailure.Offline);
            }

            Fences[path] = fence;
            if (fence < Fence)
            {
                return Task.FromResult(UploadResult.Fenced);
            }

            if (_refused.Contains(path))
            {
                return Task.FromResult(UploadResult.Exists);
            }

            using var content = open();
            using var copy = new MemoryStream();
            content.CopyTo(copy);
            _files.Add((path, copy.ToArray()));
            Uploads.Add(path);
            return Task.FromResult(UploadResult.Uploaded);
        }
    }
}
