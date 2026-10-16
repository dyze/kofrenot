using System.Text.Json;

namespace Kofrenot.Core.Settings;

/// <summary>The look of the windows. Follows the Windows setting by default.</summary>
public enum ThemeChoice
{
    System,
    Light,
    Dark,
}

/// <summary>
/// User settings persisted as JSON: the theme, whether the log records debug entries, and
/// whether to check for updates. A missing, corrupt or partial file means defaults for whatever
/// cannot be read.
/// </summary>
public sealed class SettingsStore
{
    private readonly string _path;

    public SettingsStore(string path)
    {
        _path = path;

        var data = Load(path);
        DebugLogging = data?.DebugLogging ?? false;
        Theme = Enum.TryParse<ThemeChoice>(data?.Theme, ignoreCase: true, out var theme) && Enum.IsDefined(theme)
            ? theme
            : ThemeChoice.System;
        CheckForUpdates = data?.CheckForUpdates ?? true;
    }

    /// <summary>When on, the log also records detailed (debug) entries. Off by default.</summary>
    public bool DebugLogging { get; private set; }

    public ThemeChoice Theme { get; private set; }

    /// <summary>When on, kofrenot asks GitHub once a day for a newer release. On by default.</summary>
    public bool CheckForUpdates { get; private set; }

    public void SetDebugLogging(bool enabled)
    {
        DebugLogging = enabled;
        Save();
    }

    public void SetTheme(ThemeChoice theme)
    {
        Theme = theme;
        Save();
    }

    public void SetCheckForUpdates(bool check)
    {
        CheckForUpdates = check;
        Save();
    }

    private void Save()
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var data = new SettingsData(DebugLogging, Theme.ToString(), CheckForUpdates);
        File.WriteAllText(_path, JsonSerializer.Serialize(data));
    }

    private static SettingsData? Load(string path)
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<SettingsData>(File.ReadAllText(path))
                : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    internal sealed record SettingsData(
        bool? DebugLogging = null,
        string? Theme = null,
        bool? CheckForUpdates = null);
}
