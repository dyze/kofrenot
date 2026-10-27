using System.Text.Json;
using System.Text.Json.Serialization;
using Kofrenot.Core.Graph;

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
/// records debug entries, whether to check for updates, whether a hidden window is covered, the registered vaults and the one selected,
/// the hotkey, and the Settings tab shown last. A missing, corrupt or partial file means defaults for whatever cannot be read.
/// </summary>
public sealed class SettingsStore
{
    private readonly string _path;

    /// <param name="mirrorOf">The local mirror of a server vault's store; null keeps the folder saved.</param>
    public SettingsStore(string path, Func<Guid, string>? mirrorOf = null)
    {
        _path = path;

        var data = Load(path);
        DebugLogging = data?.DebugLogging ?? false;
        Theme = Enum.TryParse<ThemeChoice>(data?.Theme, ignoreCase: true, out var theme) && Enum.IsDefined(theme)
            ? theme
            : ThemeChoice.System;
        CheckForUpdates = data?.CheckForUpdates ?? true;
        PrivacyScreen = data?.PrivacyScreen ?? true;
        MainWindowPosition = Enum.TryParse<MainWindowPosition>(data?.MainWindowPosition, ignoreCase: true, out var position) && Enum.IsDefined(position)
            ? position
            : MainWindowPosition.Centered;
        (Vaults, SelectedVault) = VaultsOf(data, mirrorOf);
        Server = data?.Server is { Url: { } url, Email: { } email } && Uri.TryCreate(url, UriKind.Absolute, out var server)
            ? new ServerAccount(server, email)
            : null;
        Hotkey = Hotkey.TryParse(data?.Hotkey, out var hotkey) ? hotkey : Hotkey.Default;
        SettingsTab = data?.SettingsTab is { } tab && SettingsCatalog.Tabs.Contains(tab) ? tab : SettingsCatalog.GeneralTab;
    }

    /// <summary>When on, the log also records detailed (debug) entries. Off by default.</summary>
    public bool DebugLogging { get; private set; }

    public ThemeChoice Theme { get; private set; }

    /// <summary>Where the main window appears when it is shown. In the middle of the mouse's monitor by default.</summary>
    public MainWindowPosition MainWindowPosition { get; private set; }

    /// <summary>When on, kofrenot asks GitHub once a day for a newer release. On by default.</summary>
    public bool CheckForUpdates { get; private set; }

    /// <summary>When on, the main window is covered while hidden or minimized, and shown again on a plain screen with a Show button. On by default.</summary>
    public bool PrivacyScreen { get; private set; }

    /// <summary>The registered vaults, in the user's order.</summary>
    public VaultList Vaults { get; private set; }

    /// <summary>The vault to open, one of <see cref="Vaults"/>; null when none is registered.</summary>
    public VaultLocation? SelectedVault { get; private set; }

    /// <summary>The global shortcut that shows kofrenot. Ctrl+Alt+K by default.</summary>
    public Hotkey Hotkey { get; private set; }

    /// <summary>The server account; null when none was set.</summary>
    public ServerAccount? Server { get; private set; }

    /// <summary>The Settings tab shown last, where Settings opens again. General by default, or for a tab no longer there.</summary>
    public string SettingsTab { get; private set; }

    public void SetServer(ServerAccount? server)
    {
        Server = server;
        Save();
    }

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

    public void SetPrivacyScreen(bool enabled)
    {
        PrivacyScreen = enabled;
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

    /// <exception cref="ArgumentException"><paramref name="tab"/> is not one of <see cref="SettingsCatalog.Tabs"/>.</exception>
    public void SetSettingsTab(string tab)
    {
        if (!SettingsCatalog.Tabs.Contains(tab))
        {
            throw new ArgumentException("Not a Settings tab", nameof(tab));
        }

        if (tab != SettingsTab)
        {
            SettingsTab = tab;
            Save();
        }
    }

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
            Vaults = [.. Vaults.Vaults.Select(vault => new VaultData(vault.Location.Folder, vault.Id, vault.Hotkey?.ToString(), vault.IsLocked ? true : null)
            {
                Server = vault.Location.Store?.Server.ToString(),
                Store = vault.Location.Store?.Id,
                Role = vault.Role?.ToString(),
            })],
            SelectedVault = SelectedVault?.Folder,
            Server = Server is { } server ? new ServerData(server.Server.ToString(), server.Email) : null,
            SettingsTab = SettingsTab,
            PrivacyScreen = PrivacyScreen,
        };
        File.WriteAllText(_path, JsonSerializer.Serialize(data));
    }

    /// <summary>
    /// The vaults stored, entries without a folder skipped. A file of kofrenot 0.4 or older has a
    /// single vault folder instead: it becomes the only vault, selected.
    /// </summary>
    private static (VaultList Vaults, VaultLocation? Selected) VaultsOf(SettingsData? data, Func<Guid, string>? mirrorOf)
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
                LocationOf(vault, mirrorOf),
                vault.Id is { } id && id != Guid.Empty ? id : null,
                Hotkey.TryParse(vault.Hotkey, out var hotkey) ? hotkey : null,
                vault.Locked == true,
                Enum.TryParse<MemberRole>(vault.Role, out var role) && Enum.IsDefined(role) ? role : null)));
        var selected = data.SelectedVault is { } chosen && vaults.Find(new VaultLocation(chosen)) is { } found
            ? found.Location
            : vaults.Vaults.FirstOrDefault()?.Location;
        return (vaults, selected);
    }

    /// <summary>
    /// A folder vault's folder, or a server vault's store and mirror. The mirror's folder is saved
    /// too, for a kofrenot 0.5 started again (it opens the mirror as a folder vault), but found
    /// again from the store: the data folder of a portable copy moves with it.
    /// </summary>
    private static VaultLocation LocationOf(VaultData vault, Func<Guid, string>? mirrorOf) =>
        vault is { Server: { } url, Store: { } store } && Uri.TryCreate(url, UriKind.Absolute, out var server)
            ? new VaultLocation(mirrorOf?.Invoke(store) ?? vault.Folder!, new StoreAddress(server, store))
            : new VaultLocation(vault.Folder!);

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

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ServerData? Server { get; init; }

        public string? SettingsTab { get; init; }

        public bool? PrivacyScreen { get; init; }
    }

    /// <param name="Folder">Missing in a damaged file: the entry is skipped.</param>
    /// <param name="Locked">Written only when locked.</param>
    internal sealed record VaultData(
        string? Folder,
        Guid? Id,
        string? Hotkey,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Locked = null)
    {
        /// <summary>A server vault's server; written only for one.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Server { get; init; }

        /// <summary>A server vault's store; written only for one.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Guid? Store { get; init; }

        /// <summary>The role in a vault shared with this account; written only for one.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Role { get; init; }
    }

    internal sealed record ServerData(string? Url, string? Email);
}
