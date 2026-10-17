using System.Text.Json;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Logging;

namespace Kofrenot.Core.Storage;

/// <summary>A vault's graph as loaded on this device, and the changes it holds.</summary>
public sealed record Snapshot(IReadOnlyList<Guid> Changes, IReadOnlyList<Node> Nodes, IReadOnlyList<Link> Links);

/// <summary>
/// Keeps a vault's graph in a local file, so each change file is read once per device. Sealed
/// with the master key ("KFS1"), bound to the vault id. Only a cache: when it is missing or
/// unreadable, every change is read again.
/// </summary>
public sealed class SnapshotStore(string path, SecretBox box, Guid vault, ILog? log = null)
{
    public const int Format = 1;
    private static ReadOnlySpan<byte> Magic => "KFS1"u8;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly ILog _log = log ?? NullLog.Instance;

    /// <summary>The snapshot, or null when there is none or it cannot be used.</summary>
    public Snapshot? Load()
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = box.Open(Magic, File.ReadAllBytes(path), vault.ToByteArray());
            if (JsonSerializer.Deserialize<SnapshotData>(json, Json) is { Format: Format, Changes: { } changes, Nodes: { } nodes, Links: { } links })
            {
                return new Snapshot(changes, nodes, links);
            }

            _log.Info("Vault", "The local snapshot has another format: every change is read again");
        }
        catch (Exception ex) when (ex is InvalidDataException or JsonException or IOException or UnauthorizedAccessException)
        {
            _log.Warn("Vault", $"The local snapshot cannot be read ({ex.Message}): every change is read again");
        }

        return null;
    }

    /// <summary>Saves the graph. A failure is logged only: the snapshot is a cache.</summary>
    public void Save(KnowledgeGraph graph, IEnumerable<Guid> changes)
    {
        var data = new SnapshotData(Format, [.. changes], [.. graph.Nodes], [.. graph.Links]);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            AtomicFile.Write(path, box.Seal(Magic, JsonSerializer.SerializeToUtf8Bytes(data, Json), vault.ToByteArray()), overwrite: true);
            _log.Debug("Vault", $"Snapshot saved: {data.Changes!.Count} changes");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Warn("Vault", $"The local snapshot cannot be saved: {ex.Message}");
        }
    }

    internal sealed record SnapshotData(int Format, List<Guid>? Changes, List<Node>? Nodes, List<Link>? Links);
}
