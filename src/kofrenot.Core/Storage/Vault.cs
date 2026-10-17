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
    private readonly HashSet<Guid> _read = [];
    private readonly HashSet<Guid> _reportedUnreadable = [];

    private Vault(string folder, VaultHeader header, byte[] masterKey, string? cacheFolder, ILog? log)
    {
        _folder = folder;
        Id = header.Id;
        _log = log ?? NullLog.Instance;
        var box = new SecretBox(masterKey);
        _codec = new ChangeCodec(box);
        _snapshot = cacheFolder is null ? null : new SnapshotStore(SnapshotOf(cacheFolder, Id), box, Id, _log);
    }

    public Guid Id { get; }

    public KnowledgeGraph Graph { get; } = new();

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
        _read.Add(change.Id);
        Graph.Apply(change);
        _log.Debug(Category, $"Change {change.Id} written");
        _snapshot?.Save(Graph, _read);
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

        foreach (var change in changes.OrderBy(change => change.At).ThenBy(change => change.Id))
        {
            Graph.Apply(change);
        }

        _log.Debug(Category, $"{changes.Count} new changes read");
        _snapshot?.Save(Graph, _read);
        return changes.Count;
    }

    private int LoadSnapshot()
    {
        if (_snapshot?.Load() is not { } snapshot)
        {
            return 0;
        }

        foreach (var node in snapshot.Nodes)
        {
            Graph.Add(node);
        }

        foreach (var link in snapshot.Links)
        {
            Graph.Add(link);
        }

        _read.UnionWith(snapshot.Changes);
        return snapshot.Changes.Count;
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
                && !_read.Contains(id)
                && TryRead(id, path) is { } change)
            {
                changes.Add(change);
                _read.Add(id);
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
