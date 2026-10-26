using Avalonia.Controls;
using Avalonia.Interactivity;
using Kofrenot.Core.Settings;
using Kofrenot.Phone.Platform;

namespace Kofrenot.Phone.Views;

/// <summary>
/// Asks the pass phrase the session needs to open a vault, checks it in the background, then opens
/// the vault. The key is remembered in the Android Keystore only when asked.
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
    }

    public bool GoBack() => false;

    public void Refresh()
    {
    }

    public void SyncChanged()
    {
    }

    private void Unlock_Click(object? sender, RoutedEventArgs e) => _shell.Run(UnlockAsync);

    // Deriving the key takes a moment: off the UI thread, the progress bar showing.
    private async Task UnlockAsync()
    {
        _shell.Services.Log.Info("Action", "Unlock a vault");
        SetBusy(true);
        string? refusal;
        try
        {
            refusal = await _shell.Services.Session.UnlockAheadAsync(_location, PassPhraseBox.Text ?? string.Empty, RememberBox.IsChecked == true);
        }
        finally
        {
            SetBusy(false);
        }

        if (refusal is not null)
        {
            Error.Text = refusal;
            Error.IsVisible = true;
            return;
        }

        PassPhraseBox.Text = string.Empty;
        _shell.Open(_location);
    }

    private void SetBusy(bool busy)
    {
        Busy.IsVisible = busy;
        UnlockButton.IsEnabled = !busy;
        Error.IsVisible = Error.IsVisible && !busy;
    }

    private void Vaults_Click(object? sender, RoutedEventArgs e) => _shell.Show(new VaultsPage(_shell));
}
