using System.Diagnostics.CodeAnalysis;

namespace Kofrenot.Core.Settings;

/// <summary>The modifier keys of a hotkey. The values are those of Windows' <c>RegisterHotKey</c>.</summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Ctrl = 2,
    Shift = 4,
    Win = 8,
}

/// <summary>
/// The global shortcut that shows kofrenot, written as in the settings: <c>Ctrl+Alt+K</c>.
/// <see cref="Key"/> is a letter, a digit or F1 to F24. Ctrl, Alt or Win is needed, so the
/// hotkey never takes a key the user types.
/// </summary>
public sealed record Hotkey(HotkeyModifiers Modifiers, string Key)
{
    public static Hotkey Default { get; } = new(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, "K");

    public static bool TryParse(string? text, [NotNullWhen(true)] out Hotkey? hotkey)
    {
        hotkey = null;
        var parts = (text ?? string.Empty).Split('+', StringSplitOptions.TrimEntries);
        var modifiers = HotkeyModifiers.None;
        foreach (var part in parts[..^1])
        {
            if (ModifierOf(part) is not { } modifier || modifiers.HasFlag(modifier))
            {
                return false;
            }

            modifiers |= modifier;
        }

        var key = parts[^1].ToUpperInvariant();
        if (!IsKey(key) || (modifiers & (HotkeyModifiers.Ctrl | HotkeyModifiers.Alt | HotkeyModifiers.Win)) == 0)
        {
            return false;
        }

        hotkey = new Hotkey(modifiers, key);
        return true;
    }

    public override string ToString()
    {
        var parts = new[] { HotkeyModifiers.Ctrl, HotkeyModifiers.Alt, HotkeyModifiers.Shift, HotkeyModifiers.Win }
            .Where(modifier => Modifiers.HasFlag(modifier))
            .Select(modifier => modifier.ToString())
            .Append(Key);
        return string.Join('+', parts);
    }

    private static HotkeyModifiers? ModifierOf(string name) => name.ToUpperInvariant() switch
    {
        "CTRL" or "CONTROL" => HotkeyModifiers.Ctrl,
        "ALT" => HotkeyModifiers.Alt,
        "SHIFT" => HotkeyModifiers.Shift,
        "WIN" or "WINDOWS" => HotkeyModifiers.Win,
        _ => null,
    };

    private static bool IsKey(string key) =>
        (key.Length == 1 && char.IsAsciiLetterOrDigit(key[0]))
        || (key.Length is 2 or 3 && key[0] == 'F' && key[1] != '0' && int.TryParse(key[1..], out var number) && number is >= 1 and <= 24);
}
