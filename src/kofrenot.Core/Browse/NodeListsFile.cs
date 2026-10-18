using System.Text.Json;
using Kofrenot.Core.Logging;

namespace Kofrenot.Core.Browse;

/// <summary>
/// Lists of nodes per vault, kept on this device only, as JSON holding ids, never names. A missing
/// or damaged file means none; a failure to save is logged only.
/// </summary>
/// <param name="what">What the lists hold, for the log ("recent nodes").</param>
internal sealed class NodeListsFile(string path, string category, string what, ILog? log = null)
{
    private readonly ILog _log = log ?? NullLog.Instance;
    private Dictionary<Guid, List<Guid>>? _byVault;

    private Dictionary<Guid, List<Guid>> ByVault => _byVault ??= Load();

    public IReadOnlyList<Guid> Of(Guid vault) => ByVault.TryGetValue(vault, out var nodes) ? nodes : [];

    /// <summary>Changes a vault's list, then saves.</summary>
    public void Change(Guid vault, Action<List<Guid>> change)
    {
        if (!ByVault.TryGetValue(vault, out var nodes))
        {
            ByVault[vault] = nodes = [];
        }

        change(nodes);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(path, JsonSerializer.Serialize(ByVault));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Warn(category, $"The {what} cannot be saved: {ex.Message}");
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
            _log.Warn(category, $"The {what} cannot be read: {ex.Message}");
            return [];
        }
    }
}
