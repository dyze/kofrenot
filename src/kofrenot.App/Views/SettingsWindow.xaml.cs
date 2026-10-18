using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kofrenot.Core;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Settings;
using Kofrenot.Core.Updates;
using Kofrenot.UI.Logging;
using Kofrenot.UI.Platform;

namespace Kofrenot.UI.Views;

/// <summary>
/// The settings, grouped in tabs. Each change is saved at once. Shown as a dialog over the
/// main window.
/// </summary>
public partial class SettingsWindow : Window
{
    /// <summary>A choice of a combo box; the box shows <see cref="ToString"/>.</summary>
    private sealed record Option<T>(T Value, string Label)
    {
        public override string ToString() => Label;
    }

    /// <summary>True while the About tab shows a dot: a newer release exists (and the check is on), seen or not.</summary>
    public static readonly DependencyProperty AboutMarkedProperty =
        DependencyProperty.Register(nameof(AboutMarked), typeof(bool), typeof(SettingsWindow));

    private static readonly Option<MainWindowPosition>[] PositionOptions =
    [
        new(MainWindowPosition.Centered, "Centered on the mouse's monitor"),
        new(MainWindowPosition.AtMouse, "At the mouse"),
        new(MainWindowPosition.LastPosition, "Where I left it"),
    ];

    private static readonly Option<ThemeChoice>[] ThemeOptions =
    [
        new(ThemeChoice.System, "Follow Windows"),
        new(ThemeChoice.Light, "Light"),
        new(ThemeChoice.Dark, "Dark"),
    ];

    private readonly SettingsStore _settings;
    private readonly LogHub _logHub;
    private readonly ILog _log;
    private readonly Action<Window> _openLog;
    private readonly UpdateChecker _updates;
    private readonly VaultSession _session;
    private readonly Func<Hotkey, bool> _applyHotkey;
    private string _changelogPath = string.Empty;
    private bool _loading;

    /// <param name="session">The vault: its folder can be changed here, and its remembered pass phrase forgotten.</param>
    /// <param name="applyHotkey">Registers a new hotkey; false when another program uses it (the old one then stays).</param>
    /// <param name="openLog">Opens the log window in front of this dialog.</param>
    public SettingsWindow(
        SettingsStore settings,
        LogHub logHub,
        AppFiles files,
        UpdateChecker updates,
        VaultSession session,
        Func<Hotkey, bool> applyHotkey,
        Action<Window> openLog)
    {
        _settings = settings;
        _logHub = logHub;
        _log = logHub.Filter;
        _openLog = openLog;
        _updates = updates;
        _session = session;
        _applyHotkey = applyHotkey;
        InitializeComponent();
        ShowVault();

        ThemeComboBox.ItemsSource = ThemeOptions;
        PositionComboBox.ItemsSource = PositionOptions;
        ShowAbout(files);

        LoadValues();
        TabList.ItemsSource = SettingsCatalog.Tabs;
        TabList.SelectedIndex = 0;

        Loaded += (_, _) => FindBox.Focus();
    }

    /// <summary>The About tab: version, links, changelog, and a row per file with its path and a Show in folder button.</summary>
    private void ShowAbout(AppFiles files)
    {
        AboutVersionText.Text = $"kofrenot {AppVersion.Of(typeof(SettingsWindow).Assembly)}";
        ShowUpdateStatus();
        _changelogPath = files.Changelog;

        foreach (var file in files.All)
        {
            var row = FilesGrid.RowDefinitions.Count;
            FilesGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = new TextBlock { Text = file.Label, Margin = new Thickness(0, 4, 12, 4), VerticalAlignment = VerticalAlignment.Center };

            // Read only, but selectable: the path can be copied.
            var path = new TextBox
            {
                Text = file.Path,
                IsReadOnly = true,
                BorderThickness = new Thickness(0),
                Background = System.Windows.Media.Brushes.Transparent,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = file.Path,
            };

            var show = new Button
            {
                Content = "Show in folder",
                Margin = new Thickness(8, 4, 0, 4),
                Padding = new Thickness(10, 2, 10, 2),
            };
            System.Windows.Automation.AutomationProperties.SetName(show, $"Show {file.Label} in folder");
            show.Click += (_, _) => ShowInFolder(file);

            Grid.SetRow(label, row);
            Grid.SetRow(path, row);
            Grid.SetColumn(path, 1);
            Grid.SetRow(show, row);
            Grid.SetColumn(show, 2);
            FilesGrid.Children.Add(label);
            FilesGrid.Children.Add(path);
            FilesGrid.Children.Add(show);
        }
    }

    /// <summary>The newer release with a link to its page, or when the last check ran.</summary>
    private void ShowUpdateStatus()
    {
        CheckForUpdatesCheckBox.IsChecked = _settings.CheckForUpdates;
        AboutMarked = _settings.CheckForUpdates && _updates.Available is not null;
        UpdateStatusText.Inlines.Clear();
        UpdateStatusText.FontWeight = FontWeights.Normal;
        UpdateStatusText.ClearValue(TextBlock.ForegroundProperty);

        if (!_settings.CheckForUpdates)
        {
            UpdateStatusText.Inlines.Add(new System.Windows.Documents.Run("The update check is off."));
        }
        else if (_updates.Available is { } release)
        {
            // In the caution color of the dots that lead here, link included.
            const string caution = "SystemFillColorCautionBrush";
            UpdateStatusText.FontWeight = FontWeights.SemiBold;
            UpdateStatusText.SetResourceReference(TextBlock.ForegroundProperty, caution);
            UpdateStatusText.Inlines.Add(new System.Windows.Documents.Run($"kofrenot {release.Version} is available: "));
            var link = new System.Windows.Documents.Hyperlink(new System.Windows.Documents.Run("download it"))
            {
                NavigateUri = new Uri(Uri.TryCreate(release.Page, UriKind.Absolute, out var page) && page.Scheme == Uri.UriSchemeHttps ? release.Page : ProjectLinks.Releases),
            };
            link.SetResourceReference(System.Windows.Documents.TextElement.ForegroundProperty, caution);
            link.Click += Link_Click;
            UpdateStatusText.Inlines.Add(link);
        }
        else if (_updates.LastCheck is { } last)
        {
            // Green, as the caution color of a newer release is yellow.
            UpdateStatusText.SetResourceReference(TextBlock.ForegroundProperty, "SystemFillColorSuccessBrush");
            UpdateStatusText.Inlines.Add(new System.Windows.Documents.Run($"You have the latest version (checked {last.ToLocalTime():g})."));
        }
        else
        {
            UpdateStatusText.Inlines.Add(new System.Windows.Documents.Run("Not checked yet."));
        }
    }

    private void CheckForUpdatesCheckBox_Click(object sender, RoutedEventArgs e)
    {
        var wanted = CheckForUpdatesCheckBox.IsChecked == true;

        try
        {
            _settings.SetCheckForUpdates(wanted);
            _log.Info("Settings", $"Check for updates {(wanted ? "on" : "off")}");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            _log.Error("Settings", "Cannot change the update check", ex);
            ShowMessage(ex.Message);
        }

        ShowUpdateStatus();
    }

    private void ShowInFolder(AppFile file)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", AppFiles.ShowInFolderArguments(file.Path)));
            _log.Info("Action", $"Show in folder: {file.Path}");
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException)
        {
            _log.Error("Action", $"Show in folder failed for {file.Path}", ex);
            ShowMessage(ex.Message);
        }
    }

    /// <summary>The download link of a newer release.</summary>
    private void Link_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Documents.Hyperlink { NavigateUri: { } uri })
        {
            OpenLink(uri);
        }
    }

    private void RepositoryButton_Click(object sender, RoutedEventArgs e) => OpenLink(new Uri(ProjectLinks.Repository));

    private void IssuesButton_Click(object sender, RoutedEventArgs e) => OpenLink(new Uri(ProjectLinks.Issues));

    /// <summary>Opens a link of the About tab in the default browser.</summary>
    private void OpenLink(Uri uri)
    {
        if (Browser.Open(uri.AbsoluteUri, _log) is { } error)
        {
            ShowMessage(error);
        }
    }

    public bool AboutMarked
    {
        get => (bool)GetValue(AboutMarkedProperty);
        set => SetValue(AboutMarkedProperty, value);
    }

    private void LoadValues()
    {
        _loading = true;
        DebugLoggingCheckBox.IsChecked = _settings.DebugLogging;
        HotkeyBox.Text = _settings.Hotkey.ToString();
        ThemeComboBox.SelectedItem = ThemeOptions.First(option => option.Value == _settings.Theme);
        PositionComboBox.SelectedItem = PositionOptions.First(option => option.Value == _settings.MainWindowPosition);
        _loading = false;
    }

    /// <summary>The panel of each tab; its Tag is the tab name.</summary>
    private IEnumerable<StackPanel> Sections => SectionsPanel.Children.OfType<StackPanel>();

    /// <summary>
    /// Without a search, shows the panel of the selected tab only. With one, hides the tab strip
    /// and shows the matching rows of every tab, under the name of their tab.
    /// </summary>
    private void UpdateSections()
    {
        var query = FindBox.Text;
        var searching = !string.IsNullOrWhiteSpace(query);
        var matches = SettingsCatalog.Find(query).Select(entry => entry.Id).ToHashSet();
        var tab = TabList.SelectedItem as string;

        FindPlaceholder.Visibility = query.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        TabList.Visibility = searching ? Visibility.Collapsed : Visibility.Visible;

        foreach (var section in Sections)
        {
            var anyShown = false;
            foreach (UIElement child in section.Children)
            {
                if (IsHeader(child))
                {
                    // Under the tab strip, the section title would repeat the tab name.
                    child.Visibility = searching ? Visibility.Visible : Visibility.Collapsed;
                    continue;
                }

                var shown = !searching || Matches(child, matches);
                child.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
                anyShown |= shown;
            }

            var visible = searching ? anyShown : Equals(section.Tag, tab);
            section.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        NoMatchText.Visibility = searching && matches.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>A row matches when its id does, or the id of a control in it.</summary>
    private static bool Matches(UIElement row, HashSet<string> matches) =>
        row is FrameworkElement { Tag: string id } && matches.Contains(id)
        || row is Panel panel && panel.Children.OfType<FrameworkElement>().Any(child => child.Tag is string childId && matches.Contains(childId));

    /// <summary>A section title: the only child of a section without a setting id.</summary>
    private static bool IsHeader(UIElement element) => element is FrameworkElement { Tag: null };

    /// <summary>A dialog needs no minimize or maximize button; it can still be resized.</summary>
    private void Window_SourceInitialized(object? sender, EventArgs e) =>
        NativeWindows.RemoveMinimizeMaximizeButtons(new System.Windows.Interop.WindowInteropHelper(this).Handle);

    private void TabList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateSections();
        if (TabList.SelectedItem as string == SettingsCatalog.AboutTab)
        {
            AcknowledgeUpdate();
        }
    }

    /// <summary>
    /// The About tab is shown: the newer release has been seen, so the main window's Settings button
    /// loses its mark (when this window closes) until a later one comes. The About tab keeps its
    /// own mark while a newer release exists.
    /// </summary>
    private void AcknowledgeUpdate()
    {
        try
        {
            _updates.Acknowledge();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            _log.Warn(UpdateChecker.LogCategory, $"Cannot save that the release was seen: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void FindBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (IsLoaded)
        {
            UpdateSections();
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            // First Esc clears the search, the next one closes the window.
            if (FindBox.Text.Length > 0)
            {
                FindBox.Clear();
            }
            else
            {
                Close();
            }

            e.Handled = true;
        }
        else if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            FindBox.Focus();
            FindBox.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.Tab && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            SwitchTab(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Ctrl+Tab / Ctrl+Shift+Tab: the next or previous tab, wrapping around. A search in progress
    /// is cleared, so that the tab is shown. The focus goes to the tab strip, since the control
    /// that had it may now be hidden.
    /// </summary>
    private void SwitchTab(int step)
    {
        FindBox.Clear();

        var count = SettingsCatalog.Tabs.Count;
        var index = (Math.Max(TabList.SelectedIndex, 0) + step + count) % count;
        TabList.SelectedIndex = index;
        if (TabList.ItemContainerGenerator.ContainerFromIndex(index) is UIElement tab)
        {
            tab.Focus();
        }
    }

    /// <summary>Down in the Find box: to the first setting shown (of the tab, or of the search results).</summary>
    private void FindBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down && Keyboard.Modifiers == ModifierKeys.None)
        {
            SectionsPanel.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
            e.Handled = true;
        }
    }

    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || ThemeComboBox.SelectedItem is not Option<ThemeChoice> option || option.Value == _settings.Theme)
        {
            return;
        }

        try
        {
            _settings.SetTheme(option.Value);
            AppTheme.Apply(option.Value);
            _log.Info("Settings", $"Theme: {option.Value}");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            _log.Error("Settings", "Cannot change the theme", ex);
            ShowMessage(ex.Message);
        }
    }

    private void PositionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || PositionComboBox.SelectedItem is not Option<MainWindowPosition> option || option.Value == _settings.MainWindowPosition)
        {
            return;
        }

        try
        {
            _settings.SetMainWindowPosition(option.Value);
            _log.Info("Settings", $"Main window position: {option.Value}");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            _log.Error("Settings", "Cannot change the main window position", ex);
            ShowMessage(ex.Message);
        }
    }

    private void DebugLoggingCheckBox_Click(object sender, RoutedEventArgs e)
    {
        var wanted = DebugLoggingCheckBox.IsChecked == true;

        try
        {
            _settings.SetDebugLogging(wanted);
            _logHub.SetDebug(wanted);
            _log.Info("Settings", $"Detailed (debug) logging {(wanted ? "on" : "off")}");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            _log.Error("Settings", "Cannot change debug logging", ex);
            ShowMessage(ex.Message);
            DebugLoggingCheckBox.IsChecked = !wanted;
        }
    }

    /// <summary>Opens the log window in front of this dialog, which stays open (to switch debug logging on and watch, say).</summary>
    private void OpenLogButton_Click(object sender, RoutedEventArgs e) => _openLog(this);

    private void ChangelogButton_Click(object sender, RoutedEventArgs e)
    {
        _log.Info("Action", "Show changelog");
        ChangelogWindow.Show(this, Changelog.Read(_changelogPath));
    }

    /// <summary>The key pressed with its modifiers becomes the hotkey, when it can be one.</summary>
    private void HotkeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Alt combinations arrive as the System key.
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Tab || key == Key.Escape)
        {
            return;
        }

        e.Handled = true;
        if (Hotkey.TryParse($"{ModifiersOf(Keyboard.Modifiers)}+{KeyName(key)}", out var hotkey))
        {
            ApplyHotkey(hotkey);
        }
        else if (KeyName(key) is not null)
        {
            HotkeyStatusText.Text = "Use Ctrl, Alt or Win with a letter, a digit or F1 to F24.";
        }
    }

    private void HotkeyDefaultButton_Click(object sender, RoutedEventArgs e) => ApplyHotkey(Hotkey.Default);

    private void ApplyHotkey(Hotkey hotkey)
    {
        if (hotkey == _settings.Hotkey)
        {
            HotkeyStatusText.Text = string.Empty;
            return;
        }

        if (!_applyHotkey(hotkey))
        {
            _log.Warn("Settings", $"Hotkey {hotkey} is used by another program");
            HotkeyStatusText.Text = $"{hotkey} is used by another program: {_settings.Hotkey} is kept.";
            return;
        }

        try
        {
            _settings.SetHotkey(hotkey);
            _log.Info("Settings", $"Hotkey: {hotkey}");
            HotkeyBox.Text = hotkey.ToString();
            HotkeyStatusText.Text = string.Empty;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            _log.Error("Settings", "Cannot change the hotkey", ex);
            ShowMessage(ex.Message);
        }
    }

    private static string ModifiersOf(ModifierKeys modifiers) =>
        string.Join('+', new[] { (ModifierKeys.Control, "Ctrl"), (ModifierKeys.Alt, "Alt"), (ModifierKeys.Shift, "Shift"), (ModifierKeys.Windows, "Win") }
            .Where(modifier => modifiers.HasFlag(modifier.Item1))
            .Select(modifier => modifier.Item2));

    /// <summary>The name <see cref="Hotkey"/> reads for a WPF key, or null for a key it cannot use.</summary>
    private static string? KeyName(Key key) => key switch
    {
        >= Key.A and <= Key.Z => key.ToString(),
        >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
        >= Key.NumPad0 and <= Key.NumPad9 => ((char)('0' + (key - Key.NumPad0))).ToString(),
        >= Key.F1 and <= Key.F24 => key.ToString(),
        _ => null,
    };

    /// <summary>The Vault tab: the folder, whether the vault is open, and its remembered pass phrase.</summary>
    private void ShowVault()
    {
        VaultFolderBox.Text = _session.Folder ?? string.Empty;
        ExploreVaultButton.IsEnabled = _session.Folder is not null;
        VaultStatusText.Text = (_session.Folder, _session.Current) switch
        {
            (null, _) => "None chosen yet. An empty folder becomes a new vault; a folder holding a kofrenot vault opens it.",
            (_, null) when _session.NewerFormat is { } format => $"Closed: written by a newer kofrenot (vault format {format}). Install the latest kofrenot to open it.",
            (_, null) => "Locked: the pass phrase was not given.",
            (_, { } vault) => $"Open: {vault.Graph.Nodes.Count} nodes, {vault.Graph.Links.Count} links.",
        };

        var remembered = _session.IsRemembered;
        RememberedText.Text = remembered ? "The pass phrase is remembered on this computer." : "The pass phrase is not remembered.";
        ForgetButton.IsEnabled = remembered;
        LinkTypes.Session = _session;
    }

    private void ChooseVaultButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Vault folder" };
        if (_session.Folder is { } folder && Directory.Exists(folder))
        {
            dialog.InitialDirectory = folder;
        }

        if (dialog.ShowDialog(this) != true || dialog.FolderName == _session.Folder)
        {
            return;
        }

        try
        {
            _session.SwitchTo(this, dialog.FolderName);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            _log.Error("Settings", "Cannot change the vault folder", ex);
            ShowMessage(ex.Message);
        }

        ShowVault();
    }

    private void ExploreVaultButton_Click(object sender, RoutedEventArgs e)
    {
        if (_session.Folder is not { } folder)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\""));
            _log.Info("Action", $"Explore to the vault folder: {folder}");
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException)
        {
            _log.Error("Action", $"Cannot open {folder}", ex);
            ShowMessage(ex.Message);
        }
    }

    private void ForgetButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _session.Forget();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            _log.Error("Settings", "Cannot forget the pass phrase", ex);
            ShowMessage(ex.Message);
        }

        ShowVault();
    }

    private void ShowMessage(string message) => MessageDialog.Inform(this, message);
}
