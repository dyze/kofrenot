using System.Text.Json;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Logging;

namespace Kofrenot.Core.Storage;

/// <summary>
/// Keeps the changes of a vault read on this device in a local file, so each change file is read
/// once per device. Sealed with the master key ("KFS1"), bound to the vault id. Only a cache:
/// when it is missing or unreadable, every change is read again.
/// </summary>
public sealed class SnapshotStore(string path, SecretBox box, Guid vault, ILog? log = null)
{
    public const int Format = 3;
    private static ReadOnlySpan<byte> Magic => "KFS1"u8;

    private readonly ILog _log = log ?? NullLog.Instance;

    /// <summary>The changes, or null when there is no snapshot or it cannot be used.</summary>
    public IReadOnlyList<Change>? Load()
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = box.Open(Magic, File.ReadAllBytes(path), vault.ToByteArray());
            if (JsonSerializer.Deserialize<SnapshotData>(json, ChangeCodec.Json) is { Format: Format, Changes: { } changes })
            {
                return changes.Select(stored => stored.Change?.ToChange(stored.Id) ?? throw new InvalidDataException("A change is empty")).ToList();
            }

            _log.Info("Vault", "The local snapshot has another format: every change is read again");
        }
        catch (NewerVaultFormatException ex)
        {
            _log.Info("Vault", $"The local snapshot holds a newer format ({ex.Format}): every change is read again");
        }
        catch (Exception ex) when (ex is InvalidDataException or JsonException or IOException or UnauthorizedAccessException)
        {
            _log.Warn("Vault", $"The local snapshot cannot be read ({ex.Message}): every change is read again");
        }

        return null;
    }

    /// <summary>Saves the changes. A failure is logged only: the snapshot is a cache.</summary>
    public void Save(IReadOnlyCollection<Change> changes)
    {
        var data = new SnapshotData(Format, changes.Select(change => new StoredChange(change.Id, ChangeData.From(change))).ToList());
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            AtomicFile.Write(path, box.Seal(Magic, JsonSerializer.SerializeToUtf8Bytes(data, ChangeCodec.Json), vault.ToByteArray()), overwrite: true);
            _log.Debug("Vault", $"Snapshot saved: {changes.Count} changes");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Warn("Vault", $"The local snapshot cannot be saved: {ex.Message}");
        }
    }

    internal sealed record SnapshotData(int Format, List<StoredChange>? Changes);

    internal sealed record StoredChange(Guid Id, ChangeData? Change);
}
