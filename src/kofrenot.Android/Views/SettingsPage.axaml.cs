using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Settings;

namespace Kofrenot.Phone.Views;

/// <summary>
/// The phone's settings, as Android's own: a list of topics, each with where it stands, named as the
/// tabs of the Settings window on Windows (General, Vault, Server, Appearance, Logging, About); a
/// topic opens on a screen of its own, the same page showing that topic's panel only.
/// </summary>
public sealed partial class SettingsPage : UserControl, IPage
{
    private readonly Shell _shell;
    private readonly string? _topic;
    private readonly Dictionary<string, Control> _panels;

    /// <param name="topic">A topic (<see cref="SettingsCatalog"/>'s tab name); null for the list of topics.</param>
    public SettingsPage(Shell shell, string? topic = null)
    {
        _shell = shell;
        _topic = topic;
        InitializeComponent();
        _panels = new()
        {
            [SettingsCatalog.GeneralTab] = GeneralPanel,
            [SettingsCatalog.VaultTab] = VaultPanel,
            [SettingsCatalog.ServerTab] = ServerPanel,
            [SettingsCatalog.AppearanceTab] = AppearancePanel,
            [SettingsCatalog.LoggingTab] = LoggingPanel,
            [SettingsCatalog.AboutTab] = AboutPanel,
        };
        Bar.Title = topic ?? "Settings";
        Topics.IsVisible = topic is null;
        foreach (var (name, panel) in _panels)
        {
            panel.IsVisible = name == topic;
        }

        ThemeBox.SelectedIndex = (int)shell.Services.Settings.Theme;
        PrivacyBox.IsChecked = shell.Services.Settings.PrivacyScreen;
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
        if (_topic is null)
        {
            ShowTopics();
        }
    }

    // Each topic with where it stands, read again when coming back to the list.
    private void ShowTopics()
    {
        var settings = _shell.Services.Settings;
        string Summary(string topic) => topic switch
        {
            SettingsCatalog.GeneralTab => settings.PrivacyScreen ? "Out of screenshots and the recent apps list" : "Screenshots allowed",
            SettingsCatalog.VaultTab => Vault.Text ?? string.Empty,
            SettingsCatalog.ServerTab => Account.Text ?? string.Empty,
            SettingsCatalog.AppearanceTab => (ThemeBox.SelectedItem as ComboBoxItem)?.Content as string ?? string.Empty,
            SettingsCatalog.LoggingTab => settings.DebugLogging ? "Debug details on" : "Debug details off",
            _ => $"kofrenot {AppServices.Version}",
        };

        Topics.Children.Clear();
        foreach (var topic in _panels.Keys)
        {
            var button = new Button
            {
                Classes = { "choice" },
                Content = new StackPanel
                {
                    Margin = new Avalonia.Thickness(0, 6),
                    Children =
                    {
                        new TextBlock { Text = topic, Classes = { "body" } },
                        new TextBlock { Text = Summary(topic), Classes = { "caption" }, TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis },
                    },
                },
            };
            button.Click += (_, _) =>
            {
                _shell.Services.Log.Info("Action", $"Settings > {topic} shown");
                _shell.Push(new SettingsPage(_shell, topic));
            };
            Topics.Children.Add(button);
        }
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

    private void Theme_Changed(object? sender, SelectionChangedEventArgs e)
    {
        var theme = (ThemeChoice)ThemeBox.SelectedIndex;
        if (!Enum.IsDefined(theme) || theme == _shell.Services.Settings.Theme)
        {
            return;
        }

        _shell.Services.Settings.SetTheme(theme);
        PhoneTheme.Apply(theme);
        _shell.Services.Log.Info("Settings", $"Theme: {theme}");
    }

    private void Privacy_Changed(object? sender, RoutedEventArgs e)
    {
        var privacy = PrivacyBox.IsChecked == true;
        if (privacy == _shell.Services.Settings.PrivacyScreen)
        {
            return;
        }

        _shell.Services.Settings.SetPrivacyScreen(privacy);
        App.Current?.ApplyPrivacyScreen();
        _shell.Services.Log.Info("Settings", $"Privacy screen {(privacy ? "on" : "off")}");
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
}
