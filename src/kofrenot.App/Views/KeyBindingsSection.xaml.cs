using System.Windows;
using System.Windows.Controls;
using Kofrenot.Core.Settings;

namespace Kofrenot.UI.Views;

/// <summary>An area of Settings > Key bindings, with its shortcuts.</summary>
public sealed record KeyBindingArea(string Area, IReadOnlyList<KeyBinding> Bindings);

/// <summary>Settings > Key bindings: every shortcut, by area (<see cref="KeyBindings"/>), read only.</summary>
public partial class KeyBindingsSection : UserControl
{
    public KeyBindingsSection() => InitializeComponent();

    public SettingsStore? Settings { private get; set; }

    /// <summary>How a vault is named in its hotkey's line.</summary>
    public Func<RegisteredVault, string> NameOf { private get; set; } = vault => vault.Location.FolderName;

    /// <summary>Lists the shortcuts again: the hotkeys may have changed in another tab.</summary>
    public void Show()
    {
        if (Settings is not { } settings)
        {
            return;
        }

        AreaList.ItemsSource = KeyBindings.All(settings, NameOf)
            .GroupBy(binding => binding.Area)
            .Select(area => new KeyBindingArea(area.Key, area.ToList()))
            .ToList();
    }

    private void Section_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            Show();
        }
    }
}
