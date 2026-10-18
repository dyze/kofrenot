using Kofrenot.Core.Datasets;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Logging;

namespace Kofrenot.Core.Storage;

/// <summary>
/// An open vault: its graph in memory, the change files read so far, and the writing of new
/// ones. Files are only added, so several apps can use the same folder at once; each one sees the
/// others' changes at its next <see cref="Refresh"/>. Not thread-safe: use it from one thread.
/// </summary>
public sealed class Vault
{
    private const string Category = "Vault";

    private readonly string _folder;
    private readonly ChangeCodec _codec;
    private readonly SnapshotStore? _snapshot;
    private readonly ILog _log;
    private readonly Dictionary<Guid, Change> _read = [];
    private readonly HashSet<Guid> _reportedUnreadable = [];

    private Vault(string folder, VaultHeader header, byte[] masterKey, string? cacheFolder, ILog? log)
    {
        _folder = folder;
        Id = header.Id;
        _log = log ?? NullLog.Instance;
        var box = new SecretBox(masterKey);
        _codec = new ChangeCodec(box);
        Notes = new NoteFiles(folder, box, _log);
        Datasets = new DatasetFiles(folder, new ChunkedBox(masterKey));
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

    /// <summary>Opens a vault with its master key: the snapshot, then the change files it lacks.</summary>
    /// <param name="cacheFolder">Where the local snapshot is kept; null for none.</param>
    /// <exception cref="NewerVaultFormatException">A change file was written by a newer kofrenot.</exception>
    public static Vault Open(string folder, VaultHeader header, byte[] masterKey, string? cacheFolder = null, ILog? log = null)
    {
        var vault = new Vault(folder, header, masterKey, cacheFolder, log);
        var fromSnapshot = vault.LoadSnapshot();
        var read = vault.Refresh();
        vault._log.Info(Category, $"Vault {vault.Id} opened: {vault.Graph.Nodes.Count} nodes, "
            + $"{vault.Graph.Links.Count} links ({fromSnapshot} changes from the snapshot, {read} read)");
        return vault;
    }

    /// <summary>The snapshot file of a vault in <paramref name="cacheFolder"/>.</summary>
    public static string SnapshotOf(string cacheFolder, Guid vault) => Path.Combine(cacheFolder, vault.ToString("N") + ".kfs");

    /// <summary>Writes a change as a new file and applies it.</summary>
    /// <exception cref="IOException">The file cannot be written.</exception>
    public void Append(Change change)
    {
        var path = VaultFolder.ChangeFileOf(_folder, change.Id);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        AtomicFile.Write(path, _codec.Encode(change), overwrite: false);
        _read[change.Id] = change;
        Graph.Apply(change);
        _log.Debug(Category, $"Change {change.Id} written");
        _snapshot?.Save(_read.Values);
    }

    /// <summary>
    /// Saves a note version: its content first, then its change, so a device that sees the change
    /// finds the content (unless the sync tool copies them in another order).
    /// </summary>
    /// <exception cref="IOException">A file cannot be written.</exception>
    public void Save(NoteEdited version, string content)
    {
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
    /// (opened once for each) and checked against <see cref="DatasetEdited.File"/>. It changes
    /// nothing in memory, so it can run off the thread using the vault.
    /// </summary>
    /// <exception cref="IOException">A file cannot be written, or the content is not the one described.</exception>
    public void WriteDataset(DatasetEdited version, Func<Stream> open, IThumbnailMaker? thumbnails = null)
    {
        if (thumbnails is not null && DatasetType.HasThumbnail(version.File))
        {
            using var image = open();
            if (thumbnails.Make(image) is { } png)
            {
                Thumbnails.Write(version.Id, png);
            }
        }

        using var content = open();
        Datasets.Write(version, content);
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
        _snapshot?.Save(_read.Values);
        return changes.Count;
    }

    private int LoadSnapshot()
    {
        if (_snapshot?.Load() is not { } snapshot)
        {
            return 0;
        }

        Apply(snapshot);
        return snapshot.Count;
    }

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
            return _codec.Decode(id, File.ReadAllBytes(path));
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
