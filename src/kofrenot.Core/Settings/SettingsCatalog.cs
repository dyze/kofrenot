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
    public const string VaultTab = "Vault";
    public const string AppearanceTab = "Appearance";
    public const string LoggingTab = "Logging";
    public const string AboutTab = "About";

    public const string HotkeyId = "Hotkey";
    public const string VaultFolderId = "VaultFolder";
    public const string RememberedPassPhraseId = "RememberedPassPhrase";
    public const string ThemeId = "Theme";
    public const string DebugLoggingId = "DebugLogging";
    public const string LogWindowId = "LogWindow";
    public const string AboutId = "About";
    public const string UpdatesId = "Updates";
    public const string FilesId = "Files";

    /// <summary>The tabs, in the order they are shown.</summary>
    public static IReadOnlyList<string> Tabs { get; } = [GeneralTab, VaultTab, AppearanceTab, LoggingTab, AboutTab];

    public static IReadOnlyList<SettingInfo> All { get; } =
    [
        new(HotkeyId, GeneralTab, "Hotkey that shows kofrenot", ["shortcut", "keyboard", "summon", "ctrl", "alt", "global"]),
        new(VaultFolderId, VaultTab, "Vault folder", ["database", "folder", "sync", "dropbox", "share", "nodes", "open", "create"]),
        new(RememberedPassPhraseId, VaultTab, "Remembered pass phrase", ["password", "passphrase", "forget", "remember", "unlock", "secret"]),
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
