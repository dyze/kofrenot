using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;

namespace Kofrenot.Phone.Views;

/// <summary>
/// Edge to edge, as Android enforces it from API 35: the app draws behind the status and navigation
/// bars, and the Shell's padding keeps its screens clear of them, and of the keyboard while it is
/// shown (the field typed in scrolled into view).
/// </summary>
public sealed partial class Shell
{
    private void WatchInsets(TopLevel top)
    {
        if (top.InsetsManager is { } insets)
        {
            insets.DisplayEdgeToEdgePreference = true;
            insets.SafeAreaChanged += Insets_Changed;
        }

        if (top.InputPane is { } keyboard)
        {
            keyboard.StateChanged += Keyboard_Changed;
        }

        Pad(top);
    }

    private void UnwatchInsets(TopLevel top)
    {
        if (top.InsetsManager is { } insets)
        {
            insets.SafeAreaChanged -= Insets_Changed;
        }

        if (top.InputPane is { } keyboard)
        {
            keyboard.StateChanged -= Keyboard_Changed;
        }
    }

    private void Insets_Changed(object? sender, SafeAreaChangedArgs e) => Pad(TopLevel.GetTopLevel(this));

    private void Keyboard_Changed(object? sender, InputPaneStateEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        Pad(top);
        if (e.NewState == InputPaneState.Open && top?.FocusManager?.GetFocusedElement() is Control focused)
        {
            focused.BringIntoView();
        }
    }

    // The bars' insets; at the bottom, the keyboard's height when it covers more.
    private void Pad(TopLevel? top)
    {
        var safe = top?.InsetsManager?.SafeAreaPadding ?? default;
        var keyboard = top?.InputPane is { State: InputPaneState.Open } pane ? pane.OccludedRect.Height : 0;
        Padding = new Thickness(safe.Left, safe.Top, safe.Right, Math.Max(safe.Bottom, keyboard));
    }
}
