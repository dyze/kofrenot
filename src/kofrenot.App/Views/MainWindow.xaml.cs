using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kofrenot.Core;
using Kofrenot.Core.Input;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Settings;
using Kofrenot.Core.Updates;

namespace Kofrenot.UI.Views;

/// <summary>
/// The main window: the input box finds nodes while typing and adds what it says on Enter (see
/// <see cref="InputPresenter"/>). Without an open database, it says how to get one.
/// </summary>
public partial class MainWindow : Window
{
    private readonly SettingsStore _settings;
    private readonly ILog _log;
    private readonly UpdateChecker _updates;
    private readonly DatabaseSession _session;
    private readonly Func<SettingsWindow> _createSettings;
    private readonly InputPresenter _input = new();
    private SettingsWindow? _settingsDialog;

    /// <param name="updates">Checks once a day for a newer release; the Settings button shows a mark when there is one.</param>
    /// <param name="session">The database the input box works on.</param>
    /// <param name="createSettings">Creates the Settings window, shown as a dialog over this one.</param>
    public MainWindow(SettingsStore settings, ILog log, UpdateChecker updates, DatabaseSession session, Func<SettingsWindow> createSettings)
    {
        _settings = settings;
        _log = log;
        _updates = updates;
        _session = session;
        _createSettings = createSettings;

        InitializeComponent();
        VersionText.Text = $"kofrenot {AppVersion.Of(typeof(MainWindow).Assembly)}";
        ShowUpdateMark();
        _session.Changed += ShowDatabase;
        ShowDatabase();
    }

    /// <summary>The input box when a database is open; otherwise what to do to get one.</summary>
    private void ShowDatabase()
    {
        var database = _session.Current;
        var open = database is not null;
        foreach (var element in new UIElement[] { InputBox, PreviewText, ResultsList })
        {
            element.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        }

        StatePanel.Visibility = open ? Visibility.Collapsed : Visibility.Visible;
        if (database is not null)
        {
            DatabaseText.Text = $"{_session.Folder} · {database.Graph.Nodes.Count} nodes · {database.Graph.Links.Count} links";
            ShowInput();
            return;
        }

        InputPlaceholder.Visibility = Visibility.Collapsed;
        DatabaseText.Text = _session.Folder ?? string.Empty;
        if (_session.Folder is null)
        {
            StateText.Text = "No database yet. Choose its folder in Settings > Database: an empty folder becomes a new database, encrypted with a pass phrase.";
            StateButton.Content = "Open settings";
        }
        else
        {
            StateText.Text = "The database is locked.";
            StateButton.Content = "Unlock";
        }
    }

    /// <summary>The preview and the results for the text of the input box.</summary>
    private void ShowInput()
    {
        if (_session.Current is not { } database)
        {
            return;
        }

        InputPlaceholder.Visibility = InputBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        _input.Update(database.Graph, InputBox.Text);
        ShowPresented();
    }

    private void ShowPresented()
    {
        PreviewText.Text = _input.Preview;
        ResultsList.ItemsSource = _input.Rows;
    }

    private void InputBox_TextChanged(object sender, TextChangedEventArgs e) => ShowInput();

    /// <summary>Enter adds, Up/Down choose among homonyms, Tab switches the side of a link.</summary>
    private void InputBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.None)
        {
            return;
        }

        e.Handled = e.Key switch
        {
            Key.Enter => AddInput(),
            Key.Up => Presented(_input.MoveChoice(-1)),
            Key.Down => Presented(_input.MoveChoice(1)),
            Key.Tab => Presented(_input.SwitchSide()),
            _ => false,
        };
    }

    private bool Presented(bool changed)
    {
        ShowPresented();
        return changed;
    }

    /// <summary>Enter: adds what the preview says, after confirmation for a duplicate node.</summary>
    private bool AddInput()
    {
        if (_input.Plan is not { CanAdd: true } plan)
        {
            return true;
        }

        if (plan is NodePlan { NeedsConfirmation: true } node
            && !MessageDialog.Confirm(this, $"“{node.Name}” exists already. Add another node with this name?", "Add"))
        {
            _log.Info("Action", "Duplicate node not added");
            return true;
        }

        try
        {
            _session.Add(plan);
            InputBox.Clear();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Error("Action", "Cannot add to the database", ex);
            MessageDialog.Inform(this, $"Cannot add to the database.\n\n{ex.Message}");
        }

        return true;
    }

    /// <summary>Esc: clears the input box, then hides the window.</summary>
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || Keyboard.Modifiers != ModifierKeys.None)
        {
            return;
        }

        if (InputBox.Text.Length > 0)
        {
            InputBox.Clear();
        }
        else
        {
            Hide();
            _log.Debug("Window", "Hidden (Esc)");
        }

        e.Handled = true;
    }

    private void StateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_session.Folder is null)
        {
            ShowSettings();
        }
        else
        {
            _log.Info("Action", "Unlock");
            _session.Open(this);
        }
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

    /// <summary>
    /// Shows this window at start, from the tray, the hotkey, or when asked by another copy of
    /// kofrenot, ready to type. Reads the changes the folder watcher may have missed.
    /// </summary>
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
        _session.Refresh();
        if (_session.Current is not null)
        {
            InputBox.Focus();
            InputBox.SelectAll();
        }
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
