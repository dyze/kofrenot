namespace Kofrenot.Core;

/// <summary>A copy of kofrenot: where its exe is, and its full build ("0.4+&lt;commit&gt;[.dirty]", see <see cref="AppVersion"/>).</summary>
public sealed record AppCopy(string Path, string Build)
{
    /// <summary>
    /// The same exe and build: starting it again only shows the running one. The path matters: a
    /// portable copy keeps its data in its own folder, so it is not the installed copy.
    /// </summary>
    public bool IsSameAs(AppCopy other) =>
        string.Equals(System.IO.Path.GetFullPath(Path), System.IO.Path.GetFullPath(other.Path), StringComparison.OrdinalIgnoreCase)
        && string.Equals(Build, other.Build, StringComparison.Ordinal);

    /// <summary>"kofrenot 0.4 (4e9c313), from C:\...\kofrenot.exe".</summary>
    public string Describe() => $"kofrenot {AppVersion.FormatWithCommit(Build)}, from {Path}";
}

/// <summary>
/// What a starting copy says when another copy already runs: only one copy runs at a time (they
/// would write the same files), and starting an installed copy while a portable or development
/// one runs would otherwise just show the other, silently.
/// </summary>
public static class RunningCopy
{
    private const string QuitByHand = "Quit the running kofrenot (tray icon > Quit), then start this one again.";

    /// <summary>The running copy cannot be asked to quit (0.3 and older).</summary>
    public static string Warning(AppCopy starting, AppCopy running) =>
        $"{running.Describe()}, is already running."
        + $"\n\nThis one is {starting.Describe()}."
        + $"\n\nTo use this one: {QuitByHand}";

    /// <summary>The running copy can be asked to quit, for this one to start.</summary>
    public static string ReplaceQuestion(AppCopy starting, AppCopy running) =>
        $"{running.Describe()}, is already running."
        + $"\n\nThis one is {starting.Describe()}."
        + "\n\nClose the running one and start this one?";

    /// <summary>The running copy refused to quit: its window says why.</summary>
    public const string Refused =
        "The running kofrenot did not close: a note is not saved or a file is being imported. Its window says what to do."
        + "\n\nThen start this one again.";

    /// <summary>The running copy did not quit in time.</summary>
    public const string NotClosed = $"The running kofrenot did not close.\n\n{QuitByHand}";
}
