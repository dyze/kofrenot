using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Kofrenot.Core.Settings;

namespace Kofrenot.UI.Platform;

/// <summary>
/// A shortcut that works from any program (Windows' <c>RegisterHotKey</c>), received by a window
/// of kofrenot. One at a time: registering another releases the previous one.
/// </summary>
public sealed class GlobalHotkey : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint ModNoRepeat = 0x4000;
    private const int Id = 1;

    private readonly IntPtr _window;
    private readonly HwndSource _source;
    private readonly Action _pressed;
    private bool _registered;

    /// <param name="pressed">Called on the window's thread.</param>
    public GlobalHotkey(Window window, Action pressed)
    {
        _window = new WindowInteropHelper(window).EnsureHandle();
        _source = HwndSource.FromHwnd(_window);
        _source.AddHook(Hook);
        _pressed = pressed;
    }

    /// <summary>Registers the hotkey. False when another program already uses it: the previous one is then released.</summary>
    public bool Register(Hotkey hotkey)
    {
        Unregister();
        _registered = RegisterHotKey(_window, Id, (uint)hotkey.Modifiers | ModNoRepeat, VirtualKeyOf(hotkey.Key));
        return _registered;
    }

    public void Dispose()
    {
        Unregister();
        _source.RemoveHook(Hook);
    }

    /// <summary>A letter or digit is its own virtual-key code; F1 to F24 follow each other from 0x70.</summary>
    private static uint VirtualKeyOf(string key) =>
        key.Length == 1 ? key[0] : 0x70u + uint.Parse(key[1..], System.Globalization.CultureInfo.InvariantCulture) - 1;

    private void Unregister()
    {
        if (_registered)
        {
            UnregisterHotKey(_window, Id);
            _registered = false;
        }
    }

    private IntPtr Hook(IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmHotkey && wParam.ToInt32() == Id)
        {
            _pressed();
            handled = true;
        }

        return IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr window, int id);
}
