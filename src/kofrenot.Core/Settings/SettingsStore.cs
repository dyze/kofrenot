using System.Text.Json;

namespace Kofrenot.Core.Settings;

/// <summary>The look of the windows. Follows the Windows setting by default.</summary>
public enum ThemeChoice
{
    System,
    Light,
    Dark,
}

/// <summary>Where the main window appears each time it is shown. Always kept fully on screen.</summary>
public enum MainWindowPosition
{
    /// <summary>In the middle of the monitor under the mouse.</summary>
    Centered,

    /// <summary>Centered on the mouse pointer.</summary>
    AtMouse,

    /// <summary>Where it was left, on the same monitor.</summary>
    LastPosition,
}

/// <summary>
/// User settings persisted as JSON: the theme, where the main window appears, whether the log
/// records debug entries, whether to check for updates, the registered vaults and the one selected,
/// and the hotkey. A missing, corrupt or partial file means defaults for whatever cannot be read.
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
        MainWindowPosition = Enum.TryParse<MainWindowPosition>(data?.MainWindowPosition, ignoreCase: true, out var position) && Enum.IsDefined(position)
            ? position
            : MainWindowPosition.Centered;
        (Vaults, SelectedVault) = VaultsOf(data);
        Hotkey = Hotkey.TryParse(data?.Hotkey, out var hotkey) ? hotkey : Hotkey.Default;
    }

    /// <summary>When on, the log also records detailed (debug) entries. Off by default.</summary>
    public bool DebugLogging { get; private set; }

    public ThemeChoice Theme { get; private set; }

    /// <summary>Where the main window appears when it is shown. In the middle of the mouse's monitor by default.</summary>
    public MainWindowPosition MainWindowPosition { get; private set; }

    /// <summary>When on, kofrenot asks GitHub once a day for a newer release. On by default.</summary>
    public bool CheckForUpdates { get; private set; }

    /// <summary>The registered vaults, in the user's order.</summary>
    public VaultList Vaults { get; private set; }

    /// <summary>The vault to open, one of <see cref="Vaults"/>; null when none is registered.</summary>
    public VaultLocation? SelectedVault { get; private set; }

    /// <summary>The global shortcut that shows kofrenot. Ctrl+Alt+K by default.</summary>
    public Hotkey Hotkey { get; private set; }

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

    public void SetMainWindowPosition(MainWindowPosition position)
    {
        MainWindowPosition = position;
        Save();
    }

    public void SetCheckForUpdates(bool check)
    {
        CheckForUpdates = check;
        Save();
    }

    /// <summary>Keeps the selected vault while it is still listed; otherwise none is selected.</summary>
    public void SetVaults(VaultList vaults)
    {
        Vaults = vaults;
        if (SelectedVault is { } selected && vaults.Find(selected) is null)
        {
            SelectedVault = null;
        }

        Save();
    }

    /// <exception cref="ArgumentException"><paramref name="location"/> is not registered.</exception>
    public void SelectVault(VaultLocation? location)
    {
        if (location is not null && Vaults.Find(location) is null)
        {
            throw new ArgumentException("Not a registered vault", nameof(location));
        }

        SelectedVault = location;
        Save();
    }

    /// <summary>
    /// Whether <paramref name="hotkey"/> is kofrenot's own already: the hotkey that shows it, or
    /// another vault's. <paramref name="forVault"/> is the vault it is wanted for; null for the
    /// hotkey that shows kofrenot.
    /// </summary>
    public bool IsHotkeyTaken(Hotkey hotkey, VaultLocation? forVault) =>
        (forVault is not null && hotkey == Hotkey) || Vaults.UsesHotkey(hotkey, forVault);

    public void SetHotkey(Hotkey hotkey)
    {
        Hotkey = hotkey;
        Save();
    }

    private void Save()
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // VaultFolder still names the selected vault, for a kofrenot 0.4 started again.
        var data = new SettingsData(DebugLogging, Theme.ToString(), CheckForUpdates, SelectedVault?.Folder, Hotkey.ToString(), MainWindowPosition.ToString())
        {
            Vaults = [.. Vaults.Vaults.Select(vault => new VaultData(vault.Location.Folder, vault.Id, vault.Hotkey?.ToString()))],
            SelectedVault = SelectedVault?.Folder,
        };
        File.WriteAllText(_path, JsonSerializer.Serialize(data));
    }

    /// <summary>
    /// The vaults stored, entries without a folder skipped. A file of kofrenot 0.4 or older has a
    /// single vault folder instead: it becomes the only vault, selected.
    /// </summary>
    private static (VaultList Vaults, VaultLocation? Selected) VaultsOf(SettingsData? data)
    {
        if (data?.Vaults is null)
        {
            return data?.VaultFolder is { } folder && !string.IsNullOrWhiteSpace(folder)
                ? (new VaultList([new RegisteredVault(new VaultLocation(folder))]), new VaultLocation(folder))
                : (VaultList.Empty, null);
        }

        var vaults = new VaultList(data.Vaults
            .OfType<VaultData>()
            .Where(vault => !string.IsNullOrWhiteSpace(vault.Folder))
            .Select(vault => new RegisteredVault(
                new VaultLocation(vault.Folder!),
                vault.Id is { } id && id != Guid.Empty ? id : null,
                Hotkey.TryParse(vault.Hotkey, out var hotkey) ? hotkey : null)));
        var selected = data.SelectedVault is { } chosen && vaults.Find(new VaultLocation(chosen)) is { } found
            ? found.Location
            : vaults.Vaults.FirstOrDefault()?.Location;
        return (vaults, selected);
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
        bool? CheckForUpdates = null,
        string? VaultFolder = null,
        string? Hotkey = null,
        string? MainWindowPosition = null)
    {
        public List<VaultData?>? Vaults { get; init; }

        public string? SelectedVault { get; init; }
    }

    /// <param name="Folder">Missing in a damaged file: the entry is skipped.</param>
    internal sealed record VaultData(string? Folder, Guid? Id, string? Hotkey);
}
