using System.Text.Json;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Logging;

namespace Kofrenot.Core.Storage;

/// <summary>The changes of a vault kept on this device, and the highest vault format of their files (<see cref="Vault.Format"/>).</summary>
public sealed record Snapshot(IReadOnlyList<Change> Changes, int HighestFormat);

/// <summary>
/// Keeps the changes of a vault read on this device in a local file, so each change file is read
/// once per device. Sealed with the vault's key (<see cref="SealKind.Snapshot"/>), bound to the vault id. Only a cache:
/// when it is missing or unreadable, every change is read again.
/// </summary>
public sealed class SnapshotStore(string path, KeyedBox box, Guid vault, ILog? log = null)
{
    public const int Format = 4;

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
            var json = box.Open(SealKind.Snapshot, File.ReadAllBytes(path), vault.ToByteArray());
            // The format first: an older snapshot has another shape.
            if (JsonSerializer.Deserialize<FormatOnly>(json, ChangeCodec.Json) is { Format: Format }
                && JsonSerializer.Deserialize<SnapshotData>(json, ChangeCodec.Json) is { Changes: { } changes, HighestFormat: >= 1 and var highest })
            {
                VaultFormat.EnsureSupported(highest);
                return new Snapshot(changes.Select(stored => stored.Change?.ToChange(stored.Id) ?? throw new InvalidDataException("A change is empty")).ToList(), highest);
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

    /// <summary>Saves the changes, and the highest vault format of their files. A failure is logged only: the snapshot is a cache.</summary>
    public void Save(IReadOnlyCollection<Change> changes, int highestFormat)
    {
        var data = new SnapshotData(Format, changes.Select(change => new StoredChange(change.Id, ChangeData.From(change))).ToList(), highestFormat);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            AtomicFile.Write(path, box.Seal(SealKind.Snapshot, JsonSerializer.SerializeToUtf8Bytes(data, ChangeCodec.Json), vault.ToByteArray()), overwrite: true);
            _log.Debug("Vault", $"Snapshot saved: {changes.Count} changes");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Warn("Vault", $"The local snapshot cannot be saved: {ex.Message}");
        }
    }

    internal sealed record FormatOnly(int Format);

    internal sealed record SnapshotData(int Format, List<StoredChange>? Changes, int HighestFormat);

    internal sealed record StoredChange(Guid Id, ChangeData? Change);
}
