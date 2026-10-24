namespace Kofrenot.Core;

/// <summary>One of kofrenot's files or folders, as listed in Settings > About.</summary>
public sealed record AppFile(string Label, string Path);

/// <summary>
/// Where kofrenot keeps its files: the user data (settings, window positions, update check,
/// remembered keys, vault snapshots, logs) in one folder, and the program. Vaults are
/// elsewhere, in the folders the user chooses. The single place their names are written.
/// </summary>
public sealed class AppFiles
{
    /// <summary>Next to kofrenot.exe, it makes a portable copy: the data is kept beside the program.</summary>
    public const string PortableMarker = "portable.txt";

    /// <summary>The start option that picks the data folder: <c>--data D:\test</c> or <c>--data=D:\test</c>.</summary>
    public const string DataOption = "--data";

    /// <param name="dataDirectory">The user data folder (<c>%LOCALAPPDATA%\kofrenot</c>).</param>
    /// <param name="programDirectory">The folder of kofrenot.exe.</param>
    /// <param name="isPortable">True when the data is kept beside the program.</param>
    /// <param name="isChosen">True when the data folder was given by <see cref="DataOption"/>.</param>
    public AppFiles(string dataDirectory, string programDirectory, bool isPortable = false, bool isChosen = false)
    {
        DataDirectory = dataDirectory;
        ProgramDirectory = programDirectory;
        IsPortable = isPortable;
        IsChosen = isChosen;
    }

    /// <summary>
    /// The files of the copy in <paramref name="programDirectory"/>: in the folder given by
    /// <see cref="DataOption"/> among <paramref name="arguments"/> (another set of data, to test
    /// with another account, say); else, with the <see cref="PortableMarker"/> beside it, in its
    /// <c>data</c> folder (a USB stick, say); else in <c>kofrenot</c> in <paramref name="localAppData"/>.
    /// </summary>
    public static AppFiles For(string programDirectory, string localAppData, Func<string, bool>? fileExists = null, IReadOnlyList<string>? arguments = null)
    {
        if (DataFolderIn(arguments ?? []) is { } chosen)
        {
            return new AppFiles(Path.GetFullPath(chosen), programDirectory, isChosen: true);
        }

        fileExists ??= File.Exists;
        return fileExists(Path.Combine(programDirectory, PortableMarker))
            ? new AppFiles(Path.Combine(programDirectory, "data"), programDirectory, isPortable: true)
            : new AppFiles(Path.Combine(localAppData, "kofrenot"), programDirectory);
    }

    // The folder after --data, or after --data=; null when not given, or given empty.
    private static string? DataFolderIn(IReadOnlyList<string> arguments)
    {
        for (var i = 0; i < arguments.Count; i++)
        {
            var folder = string.Equals(arguments[i], DataOption, StringComparison.OrdinalIgnoreCase)
                ? arguments.ElementAtOrDefault(i + 1)
                : arguments[i].StartsWith(DataOption + "=", StringComparison.OrdinalIgnoreCase) ? arguments[i][(DataOption.Length + 1)..] : null;
            if (!string.IsNullOrWhiteSpace(folder))
            {
                return folder;
            }
        }

        return null;
    }

    public string DataDirectory { get; }

    public bool IsPortable { get; }

    /// <summary>The data folder was given at start (<see cref="DataOption"/>).</summary>
    public bool IsChosen { get; }

    public string ProgramDirectory { get; }

    public string Settings => Path.Combine(DataDirectory, "settings.json");

    public string WindowPositions => Path.Combine(DataDirectory, "windows.json");

    public string UpdateCheck => Path.Combine(DataDirectory, "updates.json");

    /// <summary>The nodes last opened on this device, by vault (ids only).</summary>
    public string RecentNodes => Path.Combine(DataDirectory, "recent.json");

    /// <summary>The views open as tabs on this device, by vault (ids only).</summary>
    public string ViewTabs => Path.Combine(DataDirectory, "tabs.json");

    /// <summary>The nodes expanded in each tree view on this device, by vault (ids only).</summary>
    public string ViewStates => Path.Combine(DataDirectory, "view-states.json");

    /// <summary>The vault upgrades agreed on this device, by vault (ids only).</summary>
    public string VaultUpgrades => Path.Combine(DataDirectory, "vault-upgrades.json");

    /// <summary>The master keys remembered by Windows (DPAPI), by vault.</summary>
    public string RememberedKeys => Path.Combine(DataDirectory, "keys.json");

    /// <summary>The name last read in each vault, protected by Windows (DPAPI), by vault.</summary>
    public string VaultNames => Path.Combine(DataDirectory, "vault-names.json");

    /// <summary>The local snapshot of each vault opened on this device.</summary>
    public string Cache => Path.Combine(DataDirectory, "cache");

    public string Logs => Path.Combine(DataDirectory, "logs");

    /// <summary>The server session (refresh token), protected by Windows (DPAPI).</summary>
    public string ServerSession => Path.Combine(DataDirectory, "server-session.json");

    /// <summary>The server account's identity, as on the server (sealed), so server vaults unlock offline.</summary>
    public string AccountIdentity => Path.Combine(DataDirectory, "account-identity.json");

    /// <summary>The owners of the server vaults (their public keys), to check their signatures offline.</summary>
    public string VaultOwners => Path.Combine(DataDirectory, "vault-owners.json");

    /// <summary>The local mirrors of the server vaults, one folder each, with their sync states.</summary>
    public string Mirrors => Path.Combine(DataDirectory, "stores");

    /// <summary>The mirror of a server vault's store: a folder with the vault layout.</summary>
    public string MirrorOf(Guid store) => Path.Combine(Mirrors, store.ToString("N"));

    /// <summary>What the last sync of a store's mirror knows (<see cref="Server.ServerSync"/>).</summary>
    public string SyncStateOf(Guid store) => MirrorOf(store) + ".sync.json";

    /// <summary>The changelog, shipped next to kofrenot.exe.</summary>
    public string Changelog => Path.Combine(ProgramDirectory, "CHANGELOG.md");

    /// <summary>What Settings > About lists, in this order.</summary>
    public IReadOnlyList<AppFile> All =>
    [
        new("Settings", Settings),
        new("Window positions", WindowPositions),
        new("Update check", UpdateCheck),
        new("Recent nodes", RecentNodes),
        new("Open tabs", ViewTabs),
        new("Expanded nodes in views", ViewStates),
        new("Vault upgrades agreed", VaultUpgrades),
        new("Remembered pass phrases", RememberedKeys),
        new("Vault names", VaultNames),
        new("Vault snapshots", Cache),
        new("Server session", ServerSession),
        new("Server account identity", AccountIdentity),
        new("Owners of server vaults", VaultOwners),
        new("Server vault mirrors", Mirrors),
        new("Logs", Logs),
        new("Program", ProgramDirectory),
    ];

    /// <summary>
    /// The arguments for explorer.exe that show a file or folder in its folder, selected. One not
    /// made yet (no settings saved so far, say) opens the folder it will be in.
    /// </summary>
    public static string ShowInFolderArguments(string path, Func<string, bool>? exists = null)
    {
        exists ??= target => File.Exists(target) || Directory.Exists(target);
        var target = Path.TrimEndingDirectorySeparator(path);
        return exists(target)
            ? $"/select,\"{target}\""
            : $"\"{Path.GetDirectoryName(target)}\"";
    }
}
