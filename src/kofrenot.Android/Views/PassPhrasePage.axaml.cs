using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Kofrenot.Core.Settings;
using Kofrenot.Phone.Platform;

namespace Kofrenot.Phone.Views;

/// <summary>
/// Asks the pass phrase the session needs to open a vault, then opens it with it. The key is
/// remembered in the Android Keystore only when asked.
/// </summary>
public sealed partial class PassPhrasePage : UserControl, IPage
{
    private readonly Shell _shell;
    private readonly VaultLocation _location;

    public PassPhrasePage(Shell shell, VaultLocation location, PassPhraseNeeded needed)
    {
        _shell = shell;
        _location = location;
        InitializeComponent();
        Place.Text = needed.Place;
        Hint.Text = needed.Hint;
        Hint.IsVisible = needed.Hint is not null;
        Error.Text = needed.Refusal;
        Error.IsVisible = needed.Refusal is not null;
    }

    public bool GoBack() => false;

    public void Refresh()
    {
    }

    private void Unlock_Click(object? sender, RoutedEventArgs e)
    {
        var passPhrase = PassPhraseBox.Text ?? string.Empty;
        var remember = RememberBox.IsChecked == true;
        Busy.IsVisible = true;
        UnlockButton.IsEnabled = false;
        Error.IsVisible = false;

        // Deriving the key takes a moment: the progress bar shows first.
        Dispatcher.UIThread.Post(() => _shell.Open(_location, passPhrase, remember), DispatcherPriority.Background);
    }

    private void Vaults_Click(object? sender, RoutedEventArgs e) => _shell.Show(new VaultsPage(_shell));
}
