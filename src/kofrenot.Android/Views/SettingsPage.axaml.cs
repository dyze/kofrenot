using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Kofrenot.Core.Logging;

namespace Kofrenot.Phone.Views;

/// <summary>The server account, the open vault's remembered pass phrase, logging, and the version.</summary>
public sealed partial class SettingsPage : UserControl, IPage
{
    private readonly Shell _shell;

    public SettingsPage(Shell shell)
    {
        _shell = shell;
        InitializeComponent();
        DebugBox.IsChecked = shell.Services.Settings.DebugLogging;
        LogFolder.Text = $"Kept on this phone only: {shell.Services.Files.Logs}";
        About.Text = $"kofrenot {AppServices.Version} for Android. It reads and changes your server vaults, also offline; what defines a vault (your node and link types, its name, its keys) is changed in kofrenot on Windows.";
        Refresh();
    }

    public bool GoBack() => false;

    public void SyncChanged()
    {
    }

    public void Refresh()
    {
        var services = _shell.Services;
        Account.Text = services.Server.Account is { } account ? $"{account.Email} on {account.Server.Host}" : "Signed out";
        var selected = services.Session.Selected;
        Vault.Text = selected is null ? "None open" : services.Session.NameOf(selected);
        ForgetButton.IsEnabled = selected is not null && services.Session.IsRemembered(selected);
        MembersButton.IsVisible = services.Session.Current is not null && selected?.Location.IsOnServer == true;
        ExchangeDays.ItemsSource = services.Server.Exchanges.DayRows();
        ExchangeKinds.ItemsSource = services.Server.Exchanges.KindRows();
    }

    private void CopyExchanges_Click(object? sender, RoutedEventArgs e) => _shell.Run(CopyExchangesAsync);

    private async Task CopyExchangesAsync()
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(_shell.Services.Server.Exchanges.Text());
            _shell.Services.Log.Info("Action", "Exchange statistics copied");
        }
    }

    private void SignOut_Click(object? sender, RoutedEventArgs e) => _shell.Run(SignOutAsync);

    private async Task SignOutAsync()
    {
        if (!await _shell.Confirm("Sign out of the server?\n\nThe vaults stay on this phone, not synced until you sign in again.", "Sign out"))
        {
            return;
        }

        _shell.Services.Server.SignOut();
        _shell.Show(new SignInPage(_shell));
    }

    private void Forget_Click(object? sender, RoutedEventArgs e) => _shell.Run(ForgetAsync);

    private async Task ForgetAsync()
    {
        if (_shell.Services.Session.Selected is not { } selected
            || !await _shell.Confirm($"Forget the remembered pass phrase of {_shell.Services.Session.NameOf(selected)}?\n\nIt is asked again the next time the vault opens.", "Forget"))
        {
            return;
        }

        _shell.Services.Session.Forget(selected);
        Refresh();
    }

    private void Debug_Changed(object? sender, RoutedEventArgs e)
    {
        var debug = DebugBox.IsChecked == true;
        if (debug == _shell.Services.Settings.DebugLogging)
        {
            return;
        }

        _shell.Services.Settings.SetDebugLogging(debug);
        _shell.Services.Logs.SetDebug(debug);
        _shell.Services.Log.Info("Settings", $"Debug logging {(debug ? "on" : "off")}");
    }

    private void Members_Click(object? sender, RoutedEventArgs e)
    {
        _shell.Services.Log.Info("Action", "Members shown");
        _shell.Push(new MembersPage(_shell));
    }

    private void Back_Click(object? sender, RoutedEventArgs e) => _shell.Back();
}
