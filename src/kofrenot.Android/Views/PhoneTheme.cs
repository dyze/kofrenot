using Avalonia;
using Avalonia.Styling;
using Kofrenot.Core.Settings;

namespace Kofrenot.Phone.Views;

/// <summary>
/// Applies the light or dark look with Avalonia's Fluent theme (<see cref="Application.RequestedThemeVariant"/>),
/// which follows the phone's setting when asked. What Android draws itself (status bar, splash) follows the phone.
/// </summary>
public static class PhoneTheme
{
    /// <summary>Changes the look of every page, at once.</summary>
    public static void Apply(ThemeChoice choice)
    {
        if (Application.Current is { } app)
        {
            app.RequestedThemeVariant = choice switch
            {
                ThemeChoice.Light => ThemeVariant.Light,
                ThemeChoice.Dark => ThemeVariant.Dark,
                _ => ThemeVariant.Default,
            };
        }
    }
}
