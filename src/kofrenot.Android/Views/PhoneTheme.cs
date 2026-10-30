using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
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

    /// <summary>A colour of the tokens (App.axaml), for what a screen builds in code.</summary>
    public static IBrush Brush(string key) => (IBrush)Token(key);

    /// <summary>An icon of the tokens (App.axaml), for what a screen builds in code.</summary>
    public static Geometry Icon(string key) => (Geometry)Token(key);

    /// <summary>A size of the tokens (App.axaml), for what a screen builds in code.</summary>
    public static double Size(string key) => (double)Token(key);

    private static object Token(string key) =>
        Application.Current?.FindResource(key) ?? throw new KeyNotFoundException($"No token {key} in App.axaml.");
}
