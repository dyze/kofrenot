using System.Windows.Input;
using Kofrenot.Core.Settings;

namespace Kofrenot.UI.Views;

/// <summary>Reads the shortcut pressed in a hotkey box of Settings (click the box, then press the shortcut).</summary>
internal static class HotkeyCapture
{
    public const string Hint = "Use Ctrl, Alt or Win with a letter, a digit or F1 to F24.";

    /// <summary>
    /// The hotkey pressed, or null. Tab and Esc are left to the window; any other key is taken.
    /// <paramref name="hint"/> is told <see cref="Hint"/> when a usable key lacks Ctrl, Alt or Win.
    /// </summary>
    public static Hotkey? Read(KeyEventArgs e, Action<string> hint)
    {
        // Alt combinations arrive as the System key.
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.Tab or Key.Escape)
        {
            return null;
        }

        e.Handled = true;
        if (Hotkey.TryParse($"{ModifiersOf(Keyboard.Modifiers)}+{KeyName(key)}", out var hotkey))
        {
            return hotkey;
        }

        if (KeyName(key) is not null)
        {
            hint(Hint);
        }

        return null;
    }

    private static string ModifiersOf(ModifierKeys modifiers) =>
        string.Join('+', new[] { (ModifierKeys.Control, "Ctrl"), (ModifierKeys.Alt, "Alt"), (ModifierKeys.Shift, "Shift"), (ModifierKeys.Windows, "Win") }
            .Where(modifier => modifiers.HasFlag(modifier.Item1))
            .Select(modifier => modifier.Item2));

    /// <summary>The name <see cref="Hotkey"/> reads for a WPF key, or null for a key it cannot use.</summary>
    private static string? KeyName(Key key) => key switch
    {
        >= Key.A and <= Key.Z => key.ToString(),
        >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
        >= Key.NumPad0 and <= Key.NumPad9 => ((char)('0' + (key - Key.NumPad0))).ToString(),
        >= Key.F1 and <= Key.F24 => key.ToString(),
        _ => null,
    };
}
