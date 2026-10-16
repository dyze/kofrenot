using System.ComponentModel;
using System.Windows;
using Kofrenot.Core;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Settings;
using Kofrenot.Core.Updates;

namespace Kofrenot.UI.Views;

public partial class MainWindow : Window
{
    private readonly SettingsStore _settings;
    private readonly ILog _log;
    private readonly UpdateChecker _updates;
    private readonly Func<SettingsWindow> _createSettings;
    private SettingsWindow? _settingsDialog;

    /// <param name="updates">Checks once a day for a newer release; the Settings button shows a mark when there is one.</param>
    /// <param name="createSettings">Creates the Settings window, shown as a dialog over this one.</param>
    public MainWindow(SettingsStore settings, ILog log, UpdateChecker updates, Func<SettingsWindow> createSettings)
    {
        _settings = settings;
        _log = log;
        _updates = updates;
        _createSettings = createSettings;

        InitializeComponent();
        VersionText.Text = $"kofrenot {AppVersion.Of(typeof(MainWindow).Assembly)}";
        ShowUpdateMark();
    }

    /// <summary>
    /// Asks for a newer release when the check is on and a day has passed (in the background),
    /// then shows or hides the mark on the Settings button.
    /// </summary>
    public async void CheckForUpdates()
    {
        try
        {
            if (_settings.CheckForUpdates && _updates.IsDue)
            {
                await Task.Run(() => _updates.CheckIfDueAsync());
            }
        }
        catch (Exception ex)
        {
            _log.Error(UpdateChecker.LogCategory, "Update check failed", ex);
        }

        ShowUpdateMark();
    }

    /// <summary>A dot on the Settings button while a newer release is not seen in About yet (and the check is on).</summary>
    private void ShowUpdateMark()
    {
        var available = _settings.CheckForUpdates && _updates.ShouldNotify ? _updates.Available : null;
        UpdateMark.Visibility = available is null ? Visibility.Collapsed : Visibility.Visible;
        SettingsButton.ToolTip = available is null
            ? "Settings"
            : $"Settings. kofrenot {available.Version} is available: see About";
    }

    /// <summary>Shows this window at start, from the tray, or when asked by another copy of kofrenot.</summary>
    public void ShowFromOutside()
    {
        if (_settingsDialog is not null)
        {
            _settingsDialog.Activate();
            return;
        }

        if (!IsVisible)
        {
            _log.Debug("Window", "Shown");
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
    }

    /// <summary>
    /// Shows this window, then Settings as a dialog over it (gear button, tray menu). Returns at
    /// once when Settings is already open.
    /// </summary>
    public void ShowSettings()
    {
        if (_settingsDialog is not null)
        {
            _settingsDialog.Activate();
            return;
        }

        ShowFromOutside();

        var dialog = _createSettings();
        dialog.Owner = this;
        _log.Debug("Window", "Settings shown");

        _settingsDialog = dialog;
        try
        {
            dialog.ShowDialog();
        }
        finally
        {
            _settingsDialog = null;
        }

        Activate();

        // The update check may have been switched on or off.
        CheckForUpdates();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e) => ShowSettings();

    /// <summary>Closing hides the window: kofrenot keeps running in the notification area until Quit.</summary>
    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
        _log.Debug("Window", "Hidden");
    }
}
