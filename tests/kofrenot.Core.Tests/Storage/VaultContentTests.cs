namespace Kofrenot.Core.Tests.Storage;

public sealed class VaultContentTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly TempFolder _temp = new();
    private readonly RingLog _log = new();
    private readonly VaultHeader _header;
    private readonly KeyRing _keys;
    private readonly FakeFetcher _fetcher;

    public VaultContentTests()
    {
        (_header, _keys, _) = VaultFolder.Create(Folder, "secret", iterations: 10).Access;
        _fetcher = new FakeFetcher(Remote, Folder);
    }

    private string Folder => _temp.Sub("mirror");

    private string Remote => _temp.Sub("store");

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void AFolderVault_HasNoContentToFetch()
    {
        var note = SaveNoteOnTheStore("# Ideas");
        var vault = Open(fetcher: null);

        Assert.False(vault.FetchesContent);
        Assert.Null(vault.ReadNote(note.Id));
        Assert.Equal(ContentState.NotHere, vault.StateOf(note.Id));
        Assert.False(vault.IsFetching);
    }

    [Fact]
    public async Task AFolderVault_FetchesNothing() =>
        Assert.False(await Open(fetcher: null).FetchAsync(SaveNoteOnTheStore("# Ideas")));

    [Fact]
    public async Task ANoteNotHere_IsDownloadedWhenRead_ThenReadable()
    {
        var note = SaveNoteOnTheStore("# Ideas");
        var vault = Open();
        var fetched = new TaskCompletionSource();
        vault.ContentFetched += fetched.SetResult;

        Assert.True(vault.FetchesContent);
        Assert.Null(vault.ReadNote(note.Id));
        await fetched.Task;

        Assert.Equal("# Ideas", vault.ReadNote(note.Id));
        Assert.Equal(ContentState.Here, vault.StateOf(note.Id));
        Assert.Equal(["content"], _fetcher.Paths.Select(path => path.Split('/')[0]));
    }

    [Fact]
    public async Task ContentHere_IsNotFetched()
    {
        var vault = Open();
        var note = new NoteEdited(Guid.NewGuid(), Start, Guid.NewGuid(), []);
        vault.Save(note, "# Here");

        Assert.True(await vault.FetchAsync(note));
        Assert.Equal("# Here", vault.ReadNote(note.Id));
        Assert.Empty(_fetcher.Paths);
    }

    [Fact]
    public async Task ADataset_IsDownloadedWhenAskedFor()
    {
        var source = _temp.Sub("survey.csv");
        File.WriteAllText(source, "a,b");
        var dataset = new DatasetEdited(Guid.NewGuid(), Start, Guid.NewGuid(), DatasetFiles.Describe(source), []);
        var writer = Open(fetcher: null);
        writer.Save(dataset, () => File.OpenRead(source));
        MoveContentToTheStore();

        var vault = Open();

        Assert.True(await vault.FetchAsync(dataset));
        using var reader = new StreamReader(vault.Datasets.OpenRead(dataset.Id));
        Assert.Equal("a,b", reader.ReadToEnd());
    }

    [Fact]
    public async Task TwoReadsAtOnce_DownloadOnce()
    {
        var note = SaveNoteOnTheStore("# Ideas");
        var vault = Open();
        _fetcher.Gate = new TaskCompletionSource();

        var first = vault.FetchAsync(note);
        var second = vault.FetchAsync(note);
        Assert.Equal(ContentState.Downloading, vault.StateOf(note.Id));
        Assert.True(vault.IsFetching);
        _fetcher.Gate.SetResult();

        var results = await Task.WhenAll(first, second);
        Assert.Equal([true, true], results);
        Assert.Single(_fetcher.Paths);
        Assert.False(vault.IsFetching);
    }

    [Fact]
    public async Task AFailedDownload_IsTriedAgain_OnlyWhenAsked_OrAfterASync()
    {
        var note = SaveNoteOnTheStore("# Ideas");
        var vault = Open();
        _fetcher.Failure = ServerFailure.Offline;

        Assert.False(await vault.FetchAsync(note));
        Assert.Equal(new ContentState(ContentLevel.Failed, ServerFailure.Offline), vault.StateOf(note.Id));
        Assert.False(await vault.FetchAsync(note));
        Assert.Null(vault.ReadNote(note.Id));
        Assert.Single(_fetcher.Paths);

        _fetcher.Failure = null;
        Assert.True(vault.RetryFailed());
        Assert.False(vault.RetryFailed());
        Assert.True(await vault.FetchAsync(note));
        Assert.Equal(2, _fetcher.Paths.Count);
    }

    [Fact]
    public async Task AFailedDownload_IsTriedAgain_WhenTheUserAsks()
    {
        var note = SaveNoteOnTheStore("# Ideas");
        var vault = Open();
        _fetcher.Failure = ServerFailure.SignInNeeded;
        Assert.False(await vault.FetchAsync(note));
        Assert.Contains(_log.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("not downloaded"));

        _fetcher.Failure = null;

        Assert.True(await vault.FetchAsync(note, retry: true));
    }

    [Fact]
    public async Task AnOfflineDownload_IsNotAWarning()
    {
        var note = SaveNoteOnTheStore("# Ideas");
        var vault = Open();
        _fetcher.Failure = ServerFailure.Offline;

        await vault.FetchAsync(note);

        Assert.DoesNotContain(_log.Entries, entry => entry.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task AFileThatCannotBeWritten_IsAFailedDownload()
    {
        var note = SaveNoteOnTheStore("# Ideas");
        var vault = Open();
        _fetcher.WriteFails = true;

        Assert.False(await vault.FetchAsync(note));
        Assert.Equal(new ContentState(ContentLevel.Failed, ServerFailure.Failed), vault.StateOf(note.Id));
    }

    private Vault Open() => Open(_fetcher);

    private Vault Open(IContentFetcher? fetcher) => Vault.Open(Folder, _header, _keys, log: _log, fetcher: fetcher);

    /// <summary>A note saved, then its content moved to the store: its change only stays here.</summary>
    private NoteEdited SaveNoteOnTheStore(string content)
    {
        var note = new NoteEdited(Guid.NewGuid(), Start, Guid.NewGuid(), []);
        Open(fetcher: null).Save(note, content);
        MoveContentToTheStore();
        return note;
    }

    private void MoveContentToTheStore()
    {
        Directory.CreateDirectory(Remote);
        Directory.Move(Path.Combine(Folder, VaultFolder.ContentFolder), Path.Combine(Remote, VaultFolder.ContentFolder));
    }

    /// <summary>Copies files from a folder standing for the store, as told.</summary>
    private sealed class FakeFetcher(string remote, string mirror) : IContentFetcher
    {
        public List<string> Paths { get; } = [];

        public ServerFailure? Failure { get; set; }

        public bool WriteFails { get; set; }

        public TaskCompletionSource? Gate { get; set; }

        public async Task FetchAsync(string path, CancellationToken cancel = default)
        {
            Paths.Add(path);
            if (Gate is { } gate)
            {
                await gate.Task;
            }

            if (Failure is { } failure)
            {
                throw new ServerException(failure);
            }

            if (WriteFails)
            {
                throw new IOException("Disk full");
            }

            var target = Path.Combine([mirror, .. path.Split('/')]);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(Path.Combine([remote, .. path.Split('/')]), target);
        }
    }
}
