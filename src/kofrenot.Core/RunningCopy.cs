namespace Kofrenot.Core;

/// <summary>A copy of kofrenot: where its exe is, and its version as shown to the user.</summary>
public sealed record AppCopy(string Path, string Version)
{
    /// <summary>The same exe and version: starting it again only shows the running one.</summary>
    public bool IsSameAs(AppCopy other) =>
        string.Equals(System.IO.Path.GetFullPath(Path), System.IO.Path.GetFullPath(other.Path), StringComparison.OrdinalIgnoreCase)
        && Version == other.Version;
}

/// <summary>
/// What a starting copy says when another copy already runs: only one copy runs at a time (they
/// would write the same files), and starting an installed copy while a portable or development
/// one runs would otherwise just show the other, silently.
/// </summary>
public static class RunningCopy
{
    public static string Warning(AppCopy starting, AppCopy running) =>
        $"Another kofrenot is already running: version {running.Version}, from {running.Path}."
        + $"\n\nThis one is version {starting.Version}, from {starting.Path}."
        + "\n\nTo use this one, quit the running kofrenot (tray icon > Quit), then start this one again.";
}
