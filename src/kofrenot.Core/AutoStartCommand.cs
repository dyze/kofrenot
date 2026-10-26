namespace Kofrenot.Core;

/// <summary>What the Windows auto start entry does, seen from this copy of kofrenot.</summary>
public enum AutoStartStatus
{
    /// <summary>No entry: nothing starts at sign-in.</summary>
    Off,

    /// <summary>The entry starts this copy.</summary>
    ThisCopy,

    /// <summary>The entry starts another copy (for example the installed one, while running a build).</summary>
    OtherCopy,
}

/// <summary>
/// The command line registered to start kofrenot at sign-in, and the argument that starts it
/// hidden. Kept free of any Windows API so the rules can be unit-tested.
/// </summary>
public static class AutoStartCommand
{
    /// <summary>
    /// Starts kofrenot in the notification area only. Without it (started by the user or the
    /// installer), the main window is shown at once.
    /// </summary>
    public const string HiddenArgument = "--hidden";

    /// <summary>The quoted executable path (paths may contain spaces), then <see cref="HiddenArgument"/>.</summary>
    public static string Build(string executablePath) => $"\"{executablePath}\" {HiddenArgument}";

    /// <summary>True when the command-line arguments ask to start hidden.</summary>
    public static bool StartsHidden(IEnumerable<string> arguments) =>
        arguments.Any(argument => string.Equals(argument.Trim(), HiddenArgument, StringComparison.OrdinalIgnoreCase));

    /// <summary>Compares the program of the stored entry with this copy's executable, ignoring case.</summary>
    public static AutoStartStatus Status(string? storedCommand, string executablePath) =>
        ProgramOf(storedCommand) switch
        {
            null => AutoStartStatus.Off,
            var program when string.Equals(program, executablePath, StringComparison.OrdinalIgnoreCase) => AutoStartStatus.ThisCopy,
            _ => AutoStartStatus.OtherCopy,
        };

    /// <summary>
    /// The program the stored entry starts, without its arguments: the quoted part, or else the
    /// text up to the first space. Null when there is none.
    /// </summary>
    public static string? ProgramOf(string? storedCommand)
    {
        var command = storedCommand?.Trim();
        if (string.IsNullOrEmpty(command))
        {
            return null;
        }

        if (command[0] == '"')
        {
            var closing = command.IndexOf('"', 1);
            return closing < 0 ? command[1..] : command[1..closing];
        }

        var space = command.IndexOf(' ');
        return space < 0 ? command : command[..space];
    }
}
