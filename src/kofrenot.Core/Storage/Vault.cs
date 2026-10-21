using Kofrenot.Core.Datasets;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Views;

namespace Kofrenot.Core.Storage;

/// <summary>
/// An open vault: its graph in memory, the change files read so far, and the writing of new
/// ones. Files are only added, so several apps can use the same folder at once; each one sees the
/// others' changes at its next <see cref="Refresh"/>. Not thread-safe: use it from one thread.
/// </summary>
public sealed partial class Vault
{
    private const string Category = "Vault";

    private readonly string _folder;
    private readonly ChangeCodec _codec;
    private readonly SnapshotStore? _snapshot;
    private readonly ILog _log;
    private readonly Dictionary<Guid, Change> _read = [];
    private readonly HashSet<Guid> _reportedUnreadable = [];

    private Vault(string folder, VaultHeader header, KeyRing keys, string? cacheFolder, ILog? log)
    {
        _folder = folder;
        Id = header.Id;
        Format = header.Format;
        _log = log ?? NullLog.Instance;
        var box = new KeyedBox(keys);
        _codec = new ChangeCodec(box);
        Notes = new NoteFiles(folder, box, _log);
        Datasets = new DatasetFiles(folder, box);
        Thumbnails = new ThumbnailFiles(folder, box, _log);
        _snapshot = cacheFolder is null ? null : new SnapshotStore(SnapshotOf(cacheFolder, Id), box, Id, _log);
    }

    public Guid Id { get; }

    public KnowledgeGraph Graph { get; } = new();

    /// <summary>The content of note versions, read on demand.</summary>
    public NoteFiles Notes { get; }

    /// <summary>The files of dataset versions, read on demand.</summary>
    public DatasetFiles Datasets { get; }

    /// <summary>The thumbnails of dataset versions (images), read on demand.</summary>
    public ThumbnailFiles Thumbnails { get; }

    /// <summary>Whether the file of a note's or dataset's version is in the vault (it may arrive after its change).</summary>
    public bool HasContent(ContentEdited version) => version is DatasetEdited ? Datasets.Exists(version.Id) : Notes.Exists(version.Id);

    /// <summary>Opens a vault with its keys: the snapshot, then the change files it lacks.</summary>
    /// <param name="cacheFolder">Where the local snapshot is kept; null for none.</param>
    /// <exception cref="NewerVaultFormatException">A change file was written by a newer kofrenot.</exception>
    public static Vault Open(string folder, VaultHeader header, KeyRing keys, string? cacheFolder = null, ILog? log = null)
    {
        var vault = new Vault(folder, header, keys, cacheFolder, log);
        var fromSnapshot = vault.LoadSnapshot();
        var read = vault.Refresh();
        vault._log.Info(Category, $"Vault {vault.Id} opened: {vault.Graph.Nodes.Count} nodes, "
            + $"{vault.Graph.Links.Count} links ({fromSnapshot} changes from the snapshot, {read} read), format {vault.Format}");
        return vault;
    }

    /// <summary>The snapshot file of a vault in <paramref name="cacheFolder"/>.</summary>
    public static string SnapshotOf(string cacheFolder, Guid vault) => Path.Combine(cacheFolder, vault.ToString("N") + ".kfs");

    /// <summary>Writes a change as a new file, in <see cref="VaultFormat.Current"/>, and applies it.</summary>
    /// <exception cref="IOException">The file cannot be written.</exception>
    /// <exception cref="InvalidOperationException">The upgrade the write makes was not agreed (<see cref="AgreeToUpgrade"/>).</exception>
    public void Append(Change change)
    {
        EnsureCanWrite();
        var path = VaultFolder.ChangeFileOf(_folder, change.Id);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        AtomicFile.Write(path, _codec.Encode(change), overwrite: false);
        Written(VaultFormat.Current);
        _read[change.Id] = change;
        Graph.Apply(change);
        _log.Debug(Category, $"Change {change.Id} written");
        SaveSnapshot();
    }

    /// <summary>
    /// Saves a note version: its content first, then its change, so a device that sees the change
    /// finds the content (unless the sync tool copies them in another order).
    /// </summary>
    /// <exception cref="IOException">A file cannot be written.</exception>
    public void Save(NoteEdited version, string content)
    {
        EnsureCanWrite();
        Notes.Write(version.Id, content);
        Append(version);
    }

    /// <summary>
    /// Saves a dataset version: its files (<see cref="WriteDataset"/>), then its change. For a large
    /// file, call <see cref="WriteDataset"/> off this thread, then <see cref="Append"/> the version.
    /// </summary>
    /// <exception cref="IOException">A file cannot be written, or the content is not the one described.</exception>
    public void Save(DatasetEdited version, Func<Stream> open, IThumbnailMaker? thumbnails = null)
    {
        WriteDataset(version, open, thumbnails);
        Append(version);
    }

    /// <summary>
    /// Writes the files of a dataset version, not its change: the thumbnail of an image (when
    /// <paramref name="thumbnails"/> makes one), then the file, read from <paramref name="open"/>
    /// (opened once for each) and checked against <see cref="DatasetEdited.File"/>;
    /// <paramref name="progress"/> is told the bytes of the file sealed so far. It changes nothing
    /// in memory, so it can run off the thread using the vault.
    /// </summary>
    /// <exception cref="IOException">A file cannot be written, or the content is not the one described.</exception>
    public void WriteDataset(DatasetEdited version, Func<Stream> open, IThumbnailMaker? thumbnails = null, IProgress<long>? progress = null)
    {
        EnsureCanWrite();
        if (thumbnails is not null && DatasetType.HasThumbnail(version.File))
        {
            using var image = open();
            if (thumbnails.Make(image) is { } png)
            {
                Thumbnails.Write(version.Id, png);
            }
        }

        using var content = open();
        Datasets.Write(version, content, progress);
    }

    /// <summary>
    /// Writes the files of copied content (<see cref="CopyPlan.Contents"/>), not their changes: a
    /// note's text, or a dataset's file sealed again (with its thumbnail) under the new version.
    /// It changes nothing in memory, so it can run off the thread using the vault; then append
    /// <see cref="CopyPlan.Changes"/>.
    /// </summary>
    /// <exception cref="IOException">A file cannot be read or written.</exception>
    public void WriteCopies(IEnumerable<ContentCopy> copies, IThumbnailMaker? thumbnails = null)
    {
        EnsureCanWrite();
        foreach (var copy in copies)
        {
            switch (copy.To)
            {
                case DatasetEdited dataset:
                    WriteDataset(dataset, () => Datasets.OpenRead(copy.From.Id), thumbnails);
                    break;
                default:
                    Notes.Write(copy.To.Id, Notes.Read(copy.From.Id) ?? throw new IOException($"Note version {copy.From.Id} is not in the vault"));
                    break;
            }
        }
    }

    /// <summary>Reads the change files not read yet (written by another app, or copied by the sync tool).</summary>
    /// <returns>How many were read.</returns>
    /// <exception cref="NewerVaultFormatException">
    /// A change file was written by a newer kofrenot: the vault must not be used further, as this
    /// version would miss what it means.
    /// </exception>
    public int Refresh()
    {
        var changes = ReadNewChanges();
        if (changes.Count == 0)
        {
            return 0;
        }

        Apply(changes);

        _log.Debug(Category, $"{changes.Count} new changes read");
        SaveSnapshot();
        return changes.Count;
    }

    private int LoadSnapshot()
    {
        if (_snapshot?.Load() is not { } snapshot)
        {
            return 0;
        }

        Written(snapshot.HighestFormat);
        Apply(snapshot.Changes);
        return snapshot.Changes.Count;
    }

    private void SaveSnapshot() => _snapshot?.Save(_read.Values, Format);

    private void Apply(IEnumerable<Change> changes)
    {
        foreach (var change in changes.OrderBy(change => change.At).ThenBy(change => change.Id))
        {
            _read[change.Id] = change;
            Graph.Apply(change);
        }
    }

    private List<Change> ReadNewChanges()
    {
        var changes = new List<Change>();
        var folder = VaultFolder.ChangesOf(_folder);
        if (!Directory.Exists(folder))
        {
            return changes;
        }

        foreach (var path in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
        {
            // Skips files being written (.tmp) and anything else put there.
            if (Path.GetExtension(path) == VaultFolder.ChangeExtension
                && Guid.TryParseExact(Path.GetFileNameWithoutExtension(path), "N", out var id)
                && !_read.ContainsKey(id)
                && TryRead(id, path) is { } change)
            {
                changes.Add(change);
            }
        }

        return changes;
    }

    // An unreadable file is tried again at each refresh (the sync tool may still be copying it), and reported once.
    // A newer format is not unreadable: NewerVaultFormatException goes up.
    private Change? TryRead(Guid id, string path)
    {
        try
        {
            var (change, format) = _codec.DecodeWithFormat(id, File.ReadAllBytes(path));
            Written(format);
            return change;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            if (_reportedUnreadable.Add(id))
            {
                _log.Warn(Category, $"Change {id} skipped: {ex.Message}");
            }

            return null;
        }
    }
}
