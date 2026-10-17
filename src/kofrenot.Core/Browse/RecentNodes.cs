using System.Text.Json;
using Kofrenot.Core.Logging;

namespace Kofrenot.Core.Browse;

/// <summary>
/// The nodes last opened on this device, per vault, the latest first: what the empty input box
/// lists after the pinned ones. Kept on this device only, as JSON holding ids, never names. A
/// missing or damaged file means none.
/// </summary>
public sealed class RecentNodes(string path, ILog? log = null)
{
    public const int Max = 10;

    private readonly ILog _log = log ?? NullLog.Instance;
    private Dictionary<Guid, List<Guid>>? _byVault;

    private Dictionary<Guid, List<Guid>> ByVault => _byVault ??= Load();

    /// <summary>The nodes opened in this vault, the latest first.</summary>
    public IReadOnlyList<Guid> Of(Guid vault) => ByVault.TryGetValue(vault, out var nodes) ? nodes : [];

    /// <summary>Puts a node first, keeping at most <see cref="Max"/>, and saves. A failure to save is logged only.</summary>
    public void Opened(Guid vault, Guid node)
    {
        if (!ByVault.TryGetValue(vault, out var nodes))
        {
            ByVault[vault] = nodes = [];
        }

        nodes.Remove(node);
        nodes.Insert(0, node);
        if (nodes.Count > Max)
        {
            nodes.RemoveRange(Max, nodes.Count - Max);
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(path, JsonSerializer.Serialize(ByVault));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Warn("Recent", $"The recent nodes cannot be saved: {ex.Message}");
        }
    }

    private Dictionary<Guid, List<Guid>> Load()
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<Dictionary<Guid, List<Guid>>>(File.ReadAllText(path)) ?? []
                : [];
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _log.Warn("Recent", $"The recent nodes cannot be read: {ex.Message}");
            return [];
        }
    }
}
