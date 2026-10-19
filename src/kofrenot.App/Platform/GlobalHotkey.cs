using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Kofrenot.Core.Settings;

namespace Kofrenot.UI.Platform;

/// <summary>
/// Shortcuts that work from any program (Windows' <c>RegisterHotKey</c>), received by a window of
/// kofrenot, each by an id: registering an id again releases its previous shortcut.
/// </summary>
public sealed class GlobalHotkey : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint ModNoRepeat = 0x4000;

    private readonly IntPtr _window;
    private readonly HwndSource _source;
    private readonly Action<int> _pressed;
    private readonly HashSet<int> _registered = [];

    /// <param name="pressed">Called on the window's thread with the id of the hotkey pressed.</param>
    public GlobalHotkey(Window window, Action<int> pressed)
    {
        _window = new WindowInteropHelper(window).EnsureHandle();
        _source = HwndSource.FromHwnd(_window);
        _source.AddHook(Hook);
        _pressed = pressed;
    }

    /// <summary>Registers the hotkey of <paramref name="id"/>. False when another program already uses it: the previous one is then released.</summary>
    public bool Register(int id, Hotkey hotkey)
    {
        Unregister(id);
        if (!RegisterHotKey(_window, id, (uint)hotkey.Modifiers | ModNoRepeat, VirtualKeyOf(hotkey.Key)))
        {
            return false;
        }

        _registered.Add(id);
        return true;
    }

    public void Unregister(int id)
    {
        if (_registered.Remove(id))
        {
            UnregisterHotKey(_window, id);
        }
    }

    public void Dispose()
    {
        foreach (var id in _registered.ToList())
        {
            Unregister(id);
        }

        _source.RemoveHook(Hook);
    }

    /// <summary>A letter or digit is its own virtual-key code; F1 to F24 follow each other from 0x70.</summary>
    private static uint VirtualKeyOf(string key) =>
        key.Length == 1 ? key[0] : 0x70u + uint.Parse(key[1..], System.Globalization.CultureInfo.InvariantCulture) - 1;

    private IntPtr Hook(IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmHotkey && _registered.Contains(wParam.ToInt32()))
        {
            _pressed(wParam.ToInt32());
            handled = true;
        }

        return IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr window, int id);
}
