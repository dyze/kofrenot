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

    /// <param name="dataDirectory">The user data folder (<c>%LOCALAPPDATA%\kofrenot</c>).</param>
    /// <param name="programDirectory">The folder of kofrenot.exe.</param>
    /// <param name="isPortable">True when the data is kept beside the program.</param>
    public AppFiles(string dataDirectory, string programDirectory, bool isPortable = false)
    {
        DataDirectory = dataDirectory;
        ProgramDirectory = programDirectory;
        IsPortable = isPortable;
    }

    /// <summary>
    /// The files of the copy in <paramref name="programDirectory"/>: with the
    /// <see cref="PortableMarker"/> beside it, the data goes to its <c>data</c> folder (a USB stick,
    /// say), else to <c>kofrenot</c> in <paramref name="localAppData"/>.
    /// </summary>
    public static AppFiles For(string programDirectory, string localAppData, Func<string, bool>? fileExists = null)
    {
        fileExists ??= File.Exists;
        return fileExists(Path.Combine(programDirectory, PortableMarker))
            ? new AppFiles(Path.Combine(programDirectory, "data"), programDirectory, isPortable: true)
            : new AppFiles(Path.Combine(localAppData, "kofrenot"), programDirectory);
    }

    public string DataDirectory { get; }

    public bool IsPortable { get; }

    public string ProgramDirectory { get; }

    public string Settings => Path.Combine(DataDirectory, "settings.json");

    public string WindowPositions => Path.Combine(DataDirectory, "windows.json");

    public string UpdateCheck => Path.Combine(DataDirectory, "updates.json");

    /// <summary>The master keys remembered by Windows (DPAPI), by vault.</summary>
    public string RememberedKeys => Path.Combine(DataDirectory, "keys.json");

    /// <summary>The local snapshot of each vault opened on this device.</summary>
    public string Cache => Path.Combine(DataDirectory, "cache");

    public string Logs => Path.Combine(DataDirectory, "logs");

    /// <summary>The changelog, shipped next to kofrenot.exe.</summary>
    public string Changelog => Path.Combine(ProgramDirectory, "CHANGELOG.md");

    /// <summary>What Settings > About lists, in this order.</summary>
    public IReadOnlyList<AppFile> All =>
    [
        new("Settings", Settings),
        new("Window positions", WindowPositions),
        new("Update check", UpdateCheck),
        new("Remembered pass phrases", RememberedKeys),
        new("Vault snapshots", Cache),
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
