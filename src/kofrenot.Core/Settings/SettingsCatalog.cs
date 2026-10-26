namespace Kofrenot.Core.Settings;

/// <summary>One entry of the Settings window, as far as finding it is concerned.</summary>
/// <param name="Id">Ties the entry to its row in the window.</param>
/// <param name="Tab">The tab that shows it.</param>
/// <param name="Label">The text shown next to it.</param>
/// <param name="Keywords">Other words that find it (for example "dark" for the theme).</param>
public sealed record SettingInfo(string Id, string Tab, string Label, IReadOnlyList<string> Keywords);

/// <summary>
/// Every entry of the Settings window, grouped in tabs, and the search that finds them.
/// The window builds its rows from the same ids, so a new setting is added here first.
/// </summary>
public static class SettingsCatalog
{
    public const string GeneralTab = "General";
    public const string KeyBindingsTab = "Key bindings";
    public const string VaultTab = "Vault";
    public const string ServerTab = "Server";
    public const string LinkTypesTab = "Link types";
    public const string AppearanceTab = "Appearance";
    public const string LoggingTab = "Logging";
    public const string AboutTab = "About";

    public const string HotkeyId = "Hotkey";
    public const string MainWindowPositionId = "MainWindowPosition";
    public const string AutoStartId = "AutoStart";
    public const string KeyBindingsId = "KeyBindings";
    public const string VaultsId = "Vaults";
    public const string ServerId = "Server";
    public const string LinkAliasesId = "LinkAliases";
    public const string ThemeId = "Theme";
    public const string DebugLoggingId = "DebugLogging";
    public const string LogWindowId = "LogWindow";
    public const string AboutId = "About";
    public const string UpdatesId = "Updates";
    public const string FilesId = "Files";

    /// <summary>The tabs, in the order they are shown.</summary>
    public static IReadOnlyList<string> Tabs { get; } = [GeneralTab, KeyBindingsTab, VaultTab, ServerTab, LinkTypesTab, AppearanceTab, LoggingTab, AboutTab];

    public static IReadOnlyList<SettingInfo> All { get; } =
    [
        new(HotkeyId, GeneralTab, "Hotkey that shows kofrenot", ["shortcut", "keyboard", "summon", "ctrl", "alt", "global"]),
        new(MainWindowPositionId, GeneralTab, "Main window position", ["place", "where", "mouse", "center", "centre", "monitor", "screen", "window"]),
        new(AutoStartId, GeneralTab, "Start kofrenot with Windows", ["auto", "startup", "boot", "launch", "session", "tray"]),
        new(KeyBindingsId, KeyBindingsTab, "Key bindings: every shortcut", ["keys", "keyboard", "hotkey", "ctrl", "alt", "esc", "enter"]),
        new(VaultsId, VaultTab, "Vaults: name, folder, remembered pass phrase", [
            "database", "folder", "sync", "dropbox", "share", "nodes", "open", "create", "add", "remove", "several", "order", "name", "rename",
            "password", "passphrase", "forget", "remember", "unlock", "secret"]),
        new(ServerId, ServerTab, "Server account: sign up, sign in, sign out", ["kofre", "web", "url", "account", "email", "signup", "online", "offline", "cloud"]),
        new(LinkAliasesId, LinkTypesTab, "Link types and their aliases", ["alias", "phrase", "relates", "contains", "instance", "reverse", "input", "custom"]),
        new(ThemeId, AppearanceTab, "Theme", ["dark", "light", "mode", "color", "colour"]),
        new(DebugLoggingId, LoggingTab, "Detailed (debug) logging", ["log", "verbose", "trace"]),
        new(LogWindowId, LoggingTab, "Open log window", ["log", "view", "errors"]),
        new(AboutId, AboutTab, "Version, website, issues and changelog", ["github", "web", "link", "bug", "report", "help", "feedback", "release notes", "changes", "what's new"]),
        new(UpdatesId, AboutTab, "Check for updates once a day", ["update", "release", "download", "newer", "network", "internet"]),
        new(FilesId, AboutTab, "kofrenot's files", ["path", "location", "data", "json", "explorer", "program", "show"]),
    ];

    /// <summary>
    /// The entries matching every word of <paramref name="query"/>, in their usual order. A word
    /// matches when the label, a keyword or the tab name contains it, ignoring case. An empty
    /// query matches nothing: the window then shows its tabs instead.
    /// </summary>
    public static IReadOnlyList<SettingInfo> Find(string? query)
    {
        var words = (query ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0)
        {
            return [];
        }

        return All.Where(entry => words.All(word => Matches(entry, word))).ToList();
    }

    private static bool Matches(SettingInfo entry, string word) =>
        entry.Label.Contains(word, StringComparison.OrdinalIgnoreCase)
        || entry.Tab.Contains(word, StringComparison.OrdinalIgnoreCase)
        || entry.Keywords.Any(keyword => keyword.Contains(word, StringComparison.OrdinalIgnoreCase));
}
