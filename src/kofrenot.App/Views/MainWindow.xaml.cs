using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kofrenot.Core;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Session;
using Kofrenot.Core.Settings;
using Kofrenot.Core.Storage;
using Kofrenot.Core.Updates;
using Kofrenot.Core.Views;
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
    private readonly ViewTabs _tabs;
    private readonly TabList _tabList;
    private readonly Func<SettingsWindow> _createSettings;
    private readonly Func<ImageWindow> _createImageWindow;
    private ImageWindow? _imageWindow;
    private readonly InputPresenter _input = new();
    private SettingsWindow? _settingsDialog;

    /// <param name="updates">Checks once a day for a newer release; the Settings button shows a mark when there is one.</param>
    /// <param name="session">The vault the input box works on.</param>
    /// <param name="recent">The nodes last opened on this device, listed while the input box is empty.</param>
    /// <param name="tabs">The views open as tabs on this device.</param>
    /// <param name="createSettings">Creates the Settings window, shown as a dialog over this one.</param>
    public MainWindow(SettingsStore settings, ILog log, UpdateChecker updates, VaultSession session, RecentNodes recent, ViewTabs tabs, ViewStates viewStates, Func<SettingsWindow> createSettings, Func<ImageWindow> createImageWindow)
    {
        _settings = settings;
        _log = log;
        _updates = updates;
        _session = session;
        _recent = recent;
        _tabs = tabs;
        _tabList = new TabList(tabs);
        _createSettings = createSettings;
        _createImageWindow = createImageWindow;

        InitializeComponent();
        StateChanged += (_, _) => FitMaximized();
        FitMaximized();
        DetailsPanel.Log = log;
        DetailsPanel.Rename = rename => _session.Rename(Ask, rename);
        DetailsPanel.Write = (change, action) => _session.Write(Ask, change, action);
        DetailsPanel.Visited = node => Visited(node);
        DetailsPanel.ReadNote = version => _session.Current?.ReadNote(version);
        DetailsPanel.ContentStateOf = ContentStateOf;
        DetailsPanel.SaveNote = (version, content) => _session.SaveNote(Ask, version, content);
        DetailsPanel.OpenVault = () => _session.Current;
        DetailsPanel.SaveDataset = (version, open, progress) => _session.SaveDataset(Ask, version, open, progress);
        DetailsPanel.ExportDataset = (version, target) => _session.ExportDataset(Ask, version, target);
        DetailsPanel.IsImporting = _session.IsImporting;
        DetailsPanel.ShowImage = ShowImage;
        DetailsPanel.EditView = view => _session.EditView(Ask, view);
        DetailsPanel.Retype = retype => _session.Retype(Ask, retype);
        DetailsPanel.WriteChanges = (changes, action) => _session.Write(Ask, changes, action);
        DetailsPanel.AddLink = (request, choices) => RecordRecent(_session.Create(Ask, request, choices)) is not null;
        ViewContent.Log = log;
        ViewContent.States = viewStates;
        ViewContent.CreateNode = (type, under, file) => RecordRecent(_session.CreateNode(Ask, type, under, file));
        ViewContent.DatasetFiles = () => _session.Current?.Datasets;
        ViewContent.Rename = rename => _session.Rename(Ask, rename);
        ViewContent.Write = (changes, action) => _session.Write(Ask, changes, action);
        ViewContent.Copy = plan => _session.Copy(Ask, plan);
        ViewContent.ContentExists = version => _session.Current is { } vault && (vault.HasContent(version) || vault.FetchesContent);
        ViewContent.ReadNote = version => _session.Current?.ReadNote(version);
        ViewContent.ContentStateOf = ContentStateOf;
        VersionText.Text = $"kofrenot {AppVersion.Of(typeof(MainWindow).Assembly)}";
        ShowUpdateMark();
        _session.Changed += ShowVault;
        _session.Changed += ShowVaults;
        _session.Sync.Changed += () =>
        {
            if (_session.Current is { } open)
            {
                VaultText.Text = FooterOf(open);
            }

            ShowSync();
        };
        ShowVault();
        ShowVaults();
    }

    private ISessionQuestions Ask => new WpfQuestions(this);

    private bool DetailsShown => DetailsPanel.Browser.IsOpen;

    /// <summary>
    /// The search, or a node's details, when a vault is open; otherwise what to do to get one. The
    /// details close with the vault they show.
    /// </summary>
    /// <summary>Shows a dataset's image in the image window, opened once and reused; it closes with its vault.</summary>
    private void ShowImage(DatasetEdited version)
    {
        if (_session.Current is not { } vault)
        {
            return;
        }

        if (_imageWindow is null)
        {
            _imageWindow = _createImageWindow();
            _imageWindow.Closed += (_, _) => _imageWindow = null;
        }

        _imageWindow.Open(vault, version);
    }

    private ContentState ContentStateOf(Guid version) => _session.Current?.StateOf(version) ?? ContentState.NotHere;

    /// <summary>"Personal · 120 nodes · 80 links", and a server vault's sync ("· offline, 2 files to upload").</summary>
    private string FooterOf(Vault vault) => $"{SelectedName} · {vault.Graph.Nodes.Count} nodes · {vault.Graph.Links.Count} links";

    private void ShowVault()
    {
        var vault = _session.Current;
        ThumbnailCache.Shared.Use(vault);
        if (DetailsPanel.Browser.Graph != vault?.Graph)
        {
            DetailsPanel.Browser.Close();
            _imageWindow?.Close();
            DetailsPanel.Show();
        }

        var open = vault is not null;
        var view = vault is null ? null : SelectedView(vault);
        foreach (var element in new UIElement[] { InputArea, PreviewText, ResultsArea })
        {
            element.Visibility = open && !DetailsShown && view is null ? Visibility.Visible : Visibility.Collapsed;
        }

        ViewContent.Visibility = open && !DetailsShown && view is not null ? Visibility.Visible : Visibility.Collapsed;
        DetailsPanel.Visibility = open && DetailsShown ? Visibility.Visible : Visibility.Collapsed;
        StatePanel.Visibility = open ? Visibility.Collapsed : Visibility.Visible;
        TabStrip.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        LockButton.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        var readOnly = open && _session.IsReadOnly;
        ReadOnlyTag.Visibility = readOnly ? Visibility.Visible : Visibility.Collapsed;
        ShowTitle();
        ShowSync();
        Title = (SelectedName.Length > 0 ? SelectedName : "kofrenot") + (readOnly ? " (read only)" : string.Empty);
        if (vault is not null)
        {
            VaultText.Text = FooterOf(vault);
            VaultText.ToolTip = _session.Folder;
            ShowTabs(vault, view);
            ShowInput();
            DetailsPanel.Show();
            return;
        }

        InputPlaceholder.Visibility = Visibility.Collapsed;
        VaultText.Text = SelectedName;
        VaultText.ToolTip = _session.Folder;
        if (_session.Folder is null)
        {
            StateText.Text = "No vault yet. Add one in Settings > Vault: an empty folder becomes a new vault, encrypted with a pass phrase.";
            StateButton.Content = "Open settings";
        }
        else if (_session.NewerFormat is { } format)
        {
            StateText.Text = $"This vault was written by a newer kofrenot (vault format {format}; this one reads up to format {VaultFormat.Current}). Install the latest kofrenot to open it.";
            StateButton.Content = "Get the latest kofrenot";
        }
        else if (_session.OlderFormat is { } older)
        {
            StateText.Text = $"This vault is in format {older}: this kofrenot uses it once upgraded to format {VaultFormat.Current}, which kofrenot older than {VaultFormat.Since(VaultFormat.Current)} no longer opens.";
            StateButton.Content = "Upgrade and open…";
        }
        else
        {
            StateText.Text = _session.IsLocked ? "The vault is locked: Unlock asks its pass phrase." : "The vault is locked.";
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

        InputPlaceholder.Visibility = _chips.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
        _input.Recent = _recent.Of(vault.Id);
        _input.Update(vault.Graph, _chips.Text);
        ShowPresented();
    }

    private void ShowPresented()
    {
        PreviewText.Text = _input.Preview;
        ResultsList.ItemsSource = _input.Rows;
        if (_session.Current is { } vault)
        {
            ShowChips(vault.Graph);
        }
    }

    /// <summary>
    /// Enter opens the selected node's details, or else adds; Up/Down choose among homonyms
    /// while a link is typed, or else select a node; Tab accepts the proposal, or else switches the
    /// side of a link; Right at the end accepts the proposal; Backspace at the start unlabels.
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
            Key.Tab => AcceptProposal() || Presented(_input.SwitchSide()),
            Key.Right => AcceptProposal(),
            Key.Back => UnlabelLast(),
            _ => false,
        };
    }

    /// <summary>A click selects a row, as Up/Down would (or chooses a homonym); a double-click opens its details.</summary>
    private void Result_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ResultRow { Node: { } node } row })
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            OpenDetails(node, row.From);
        }
        else
        {
            Presented(_input.Select(row));
        }

        e.Handled = true;
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

    /// <summary>Back from the details: the search or the view they were opened from, as it was left.</summary>
    private void DetailsPanel_BackToSearch()
    {
        ShowVault();
        FocusTab();
    }

    private bool Presented(bool changed)
    {
        ShowPresented();
        return changed;
    }

    /// <summary>
    /// Enter: turns what is typed into labels, then opens the creation screen for what the preview says. Once it is added, its nodes (new
    /// or existing) go first among the recent ones, the first typed on top, and the input box is cleared.
    /// </summary>
    private bool AddInput()
    {
        if (_input is { Plan.CanAdd: true, Request: not null })
        {
            LabelAll();
        }

        if (_input is { Plan.CanAdd: true, Request: { } request }
            && RecordRecent(_session.Create(Ask, request, _input.Choices)) is { } added)
        {
            ClearInput();

            // A new view opens in a tab.
            if (added.NewNodes.LastOrDefault(node => node.Type == TypeCatalog.View.Id) is { } view)
            {
                OpenTab(view);
                return true;
            }
        }

        InputBox.Focus();
        return true;
    }

    /// <summary>Puts the nodes of a creation, new or existing, first among the recent ones, the first typed on top. Returns the creation.</summary>
    private AddPlan? RecordRecent(AddPlan? added)
    {
        if (added is not null && _session.Current is { } vault)
        {
            foreach (var node in added.Nodes.Reverse())
            {
                _recent.Opened(vault.Id, node.Id);
            }
        }

        return added;
    }

    /// <summary>In the details, their keys (Esc goes back). In the search, Esc hides the proposal, clears the input box, then hides the window.</summary>
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Tab && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            e.Handled = SwitchTab(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1);
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.K && DetailsShown && DetailsPanel.IsVisible)
        {
            DetailsPanel.StartLink();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && HandleVaultKey(e.Key))
        {
            e.Handled = true;
            return;
        }

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

        if (ViewContent.IsVisible && ViewContent.HandleEscape())
        {
            e.Handled = true;
            return;
        }

        if (InputBox.IsKeyboardFocusWithin && DismissProposal())
        {
            e.Handled = true;
            return;
        }

        if (!_chips.IsEmpty)
        {
            ClearInput();
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
        else if (_session.OlderFormat is not null)
        {
            _log.Info("Action", "Upgrade the vault");
            _session.Upgrade(Ask);
        }
        else
        {
            _log.Info("Action", "Unlock");
            _session.Unlock(Ask);
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
            WindowPlacer.PlaceMainWindow(this, _settings.MainWindowPosition, _log);
            _log.Debug("Window", "Shown");
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
        _session.Refresh();
        _session.Sync.Soon();
        if (_session.Current is not null && DetailsShown)
        {
            DetailsPanel.Focus();
        }
        else if (ViewContent.IsVisible)
        {
            ViewContent.FocusContent();
        }
        else if (_session.Current is not null)
        {
            // All of it selected, so typing replaces it: labels included.
            _chips.Unlabel();
            SetInputText(_chips.Tail);
            ShowInput();
            InputBox.Focus();
            InputBox.SelectAll();
        }
    }

    /// <summary>
    /// Shows this window, then Settings as a dialog over it (gear button, tray menu), at
    /// <paramref name="tab"/> when given. Returns at once when Settings is already open.
    /// </summary>
    public void ShowSettings(string? tab = null)
    {
        if (_settingsDialog is not null)
        {
            _settingsDialog.Activate();
            if (tab is not null)
            {
                _settingsDialog.SelectTab(tab);
            }

            return;
        }

        ShowFromOutside();

        var dialog = _createSettings();
        dialog.Owner = this;
        if (tab is not null)
        {
            dialog.SelectTab(tab);
        }
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

    /// <summary>Why kofrenot must not quit now (an unsaved note, an import running), or null.</summary>
    public string? WhyNotQuit() => QuitRefusal.Reason(DetailsPanel.UnsavedNote, _session.ImportsRunning);

    /// <summary>Closing hides the window: kofrenot keeps running in the notification area until Quit.</summary>
    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        e.Cancel = true;
        DetailsPanel.LeaveNote();
        Hide();
        _log.Debug("Window", "Hidden");
    }
}
