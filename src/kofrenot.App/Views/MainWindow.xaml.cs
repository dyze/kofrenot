using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kofrenot.Core;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Settings;
using Kofrenot.Core.Storage;
using Kofrenot.Core.Updates;
using Kofrenot.UI.Platform;

namespace Kofrenot.UI.Views;

/// <summary>
/// The main window: the input box finds nodes while typing and adds what it says on Enter (see
/// <see cref="InputPresenter"/>). Without an open vault, it says how to get one.
/// </summary>
public partial class MainWindow : Window
{
    private readonly SettingsStore _settings;
    private readonly ILog _log;
    private readonly UpdateChecker _updates;
    private readonly VaultSession _session;
    private readonly RecentNodes _recent;
    private readonly Func<SettingsWindow> _createSettings;
    private readonly InputPresenter _input = new();
    private SettingsWindow? _settingsDialog;

    /// <param name="updates">Checks once a day for a newer release; the Settings button shows a mark when there is one.</param>
    /// <param name="session">The vault the input box works on.</param>
    /// <param name="recent">The nodes last opened on this device, listed while the input box is empty.</param>
    /// <param name="createSettings">Creates the Settings window, shown as a dialog over this one.</param>
    public MainWindow(SettingsStore settings, ILog log, UpdateChecker updates, VaultSession session, RecentNodes recent, Func<SettingsWindow> createSettings)
    {
        _settings = settings;
        _log = log;
        _updates = updates;
        _session = session;
        _recent = recent;
        _createSettings = createSettings;

        InitializeComponent();
        DetailsPanel.Log = log;
        DetailsPanel.Rename = rename => _session.Rename(this, rename);
        DetailsPanel.Write = (change, action) => _session.Write(this, change, action);
        DetailsPanel.Visited = node => Visited(node);
        VersionText.Text = $"kofrenot {AppVersion.Of(typeof(MainWindow).Assembly)}";
        ShowUpdateMark();
        _session.Changed += ShowVault;
        ShowVault();
    }

    private bool DetailsShown => DetailsPanel.Browser.IsOpen;

    /// <summary>
    /// The search, or a node's details, when a vault is open; otherwise what to do to get one. The
    /// details close with the vault they show.
    /// </summary>
    private void ShowVault()
    {
        var vault = _session.Current;
        if (DetailsPanel.Browser.Graph != vault?.Graph)
        {
            DetailsPanel.Browser.Close();
        }

        var open = vault is not null;
        foreach (var element in new UIElement[] { InputArea, PreviewText, ResultsArea })
        {
            element.Visibility = open && !DetailsShown ? Visibility.Visible : Visibility.Collapsed;
        }

        DetailsPanel.Visibility = open && DetailsShown ? Visibility.Visible : Visibility.Collapsed;
        StatePanel.Visibility = open ? Visibility.Collapsed : Visibility.Visible;
        if (vault is not null)
        {
            VaultText.Text = $"{_session.Folder} · {vault.Graph.Nodes.Count} nodes · {vault.Graph.Links.Count} links";
            ShowInput();
            DetailsPanel.Show();
            return;
        }

        InputPlaceholder.Visibility = Visibility.Collapsed;
        VaultText.Text = _session.Folder ?? string.Empty;
        if (_session.Folder is null)
        {
            StateText.Text = "No vault yet. Choose its folder in Settings > Vault: an empty folder becomes a new vault, encrypted with a pass phrase.";
            StateButton.Content = "Open settings";
        }
        else if (_session.NewerFormat is { } format)
        {
            StateText.Text = $"This vault was written by a newer kofrenot (vault format {format}; this one reads up to format {VaultFormat.Current}). Install the latest kofrenot to open it.";
            StateButton.Content = "Get the latest kofrenot";
        }
        else
        {
            StateText.Text = "The vault is locked.";
            StateButton.Content = "Unlock";
        }
    }

    /// <summary>The preview and the results for the text of the input box.</summary>
    private void ShowInput()
    {
        if (_session.Current is not { } vault)
        {
            return;
        }

        InputPlaceholder.Visibility = InputBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        _input.Recent = _recent.Of(vault.Id);
        _input.Update(vault.Graph, InputBox.Text);
        ShowPresented();
    }

    private void ShowPresented()
    {
        PreviewText.Text = _input.Preview;
        ResultsList.ItemsSource = _input.Rows;
    }

    private void InputBox_TextChanged(object sender, TextChangedEventArgs e) => ShowInput();

    /// <summary>
    /// Enter opens the selected node's details, or else adds; Up/Down choose among homonyms
    /// while a link is typed, or else select a node; Tab switches the side of a link.
    /// </summary>
    private void InputBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.None)
        {
            return;
        }

        e.Handled = e.Key switch
        {
            Key.Enter when _input.SelectedRow is { Node: { } node } row => OpenDetails(node, row.From),
            Key.Enter => AddInput(),
            Key.Up => Presented(_input.MoveChoice(-1) || _input.MoveSelection(-1)),
            Key.Down => Presented(_input.MoveChoice(1) || _input.MoveSelection(1)),
            Key.Tab => Presented(_input.SwitchSide()),
            _ => false,
        };
    }

    private void OpenResult_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ResultRow { Node: { } node } row })
        {
            OpenDetails(node, row.From);
        }
    }

    /// <summary>Shows a node's details instead of the search; for a link row, under the node it comes from.</summary>
    private bool OpenDetails(Node node, Node? from)
    {
        if (_session.Current is { } vault)
        {
            DetailsPanel.Open(vault.Graph, node, from);
            ShowVault();
            DetailsPanel.Focus();
        }

        return true;
    }

    /// <summary>A node shown in the details goes first among the recent ones.</summary>
    private void Visited(Node node)
    {
        if (_session.Current is { } vault)
        {
            _recent.Opened(vault.Id, node.Id);
        }
    }

    /// <summary>Back from the details: the search, as it was left.</summary>
    private void DetailsPanel_BackToSearch()
    {
        ShowVault();
        InputBox.Focus();
    }

    private bool Presented(bool changed)
    {
        ShowPresented();
        return changed;
    }

    /// <summary>Enter: opens the creation screen for what the preview says; the input box is cleared once it is added.</summary>
    private bool AddInput()
    {
        if (_input is { Plan.CanAdd: true, Request: { } request } && _session.Create(this, request, _input.Choices))
        {
            InputBox.Clear();
        }

        InputBox.Focus();
        return true;
    }

    /// <summary>In the details, their keys (Esc goes back). In the search, Esc clears the input box, then hides the window.</summary>
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.None)
        {
            return;
        }

        if (DetailsShown && DetailsPanel.IsVisible)
        {
            e.Handled = DetailsPanel.HandleKey(e.Key);
            return;
        }

        if (e.Key != Key.Escape)
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
        else if (_session.NewerFormat is not null)
        {
            if (Browser.Open(ProjectLinks.Releases, _log) is { } error)
            {
                MessageDialog.Inform(this, $"Cannot open {ProjectLinks.Releases}.\n\n{error}");
            }
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
        if (_session.Current is not null && DetailsShown)
        {
            DetailsPanel.Focus();
        }
        else if (_session.Current is not null)
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
