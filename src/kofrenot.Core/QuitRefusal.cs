namespace Kofrenot.Core;

/// <summary>
/// Why kofrenot must not quit now, from the tray or when another copy replaces it, nor close the
/// vault for another: it would lose a note's unsaved changes, or cut an import while its file is
/// written into the vault.
/// </summary>
public static class QuitRefusal
{
    public const string Quit = "quit";
    public const string SwitchVaults = "switch vaults";

    /// <summary>What the user is told, or null when kofrenot may quit (or <paramref name="action"/>).</summary>
    /// <param name="unsavedNote">The name of the note shown with unsaved changes, or null.</param>
    /// <param name="importsRunning">How many files are being imported into the vault.</param>
    /// <param name="action">What is refused: <see cref="Quit"/> or <see cref="SwitchVaults"/>.</param>
    public static string? Reason(string? unsavedNote, int importsRunning, string action = Quit)
    {
        var reasons = new List<string>();
        if (unsavedNote is not null)
        {
            reasons.Add($"The note \"{unsavedNote}\" is not saved: save it (Ctrl+S).");
        }

        if (importsRunning > 0)
        {
            reasons.Add(importsRunning == 1 ? "A file is being imported: wait until it ends." : $"{importsRunning} files are being imported: wait until they end.");
        }

        return reasons.Count == 0
            ? null
            : $"kofrenot cannot {action} now.\n\n" + string.Join("\n", reasons) + $"\n\nThen {action} again.";
    }
}
