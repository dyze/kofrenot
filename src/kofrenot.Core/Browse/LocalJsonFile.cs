using System.Text.Json;
using Kofrenot.Core.Logging;

namespace Kofrenot.Core.Browse;

/// <summary>
/// A JSON file of state kept on this device only. A missing or damaged file reads as new; a
/// failure to save is logged only: such state is a convenience, never needed.
/// </summary>
/// <param name="what">What the file holds, for the log ("recent nodes").</param>
internal sealed class LocalJsonFile<T>(string path, string category, string what, ILog? log = null)
    where T : new()
{
    private readonly ILog _log = log ?? NullLog.Instance;

    public T Load()
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path)) ?? new T() : new T();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _log.Warn(category, $"The {what} cannot be read: {ex.Message}");
            return new T();
        }
    }

    public void Save(T value)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(path, JsonSerializer.Serialize(value));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Warn(category, $"The {what} cannot be saved: {ex.Message}");
        }
    }
}
