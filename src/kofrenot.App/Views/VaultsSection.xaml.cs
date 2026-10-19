using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kofrenot.Core.Input;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Settings;
using Kofrenot.Core.Storage;

namespace Kofrenot.UI.Views;

/// <summary>A row of the list: a registered vault, its name (marked when open) and its folder.</summary>
public sealed record VaultRow(RegisteredVault Vault, string Title, string Folder);

/// <summary>
/// Settings > Vault: the registered vaults, added, removed and ordered here, and for the one chosen
/// in the list its name, folder, state and remembered pass phrase.
/// </summary>
public partial class VaultsSection : UserControl
{
    private VaultSession? _session;
    private bool _filling;
    private int _measuringSize;

    public VaultsSection() => InitializeComponent();

    public ILog Log { private get; set; } = NullLog.Instance;

    /// <summary>Why the open vault cannot be closed now (a file being imported, a note not saved), or null.</summary>
    public Func<string?> WhyNotSwitch { private get; set; } = () => null;

    /// <summary>The vaults shown; set once by the Settings window.</summary>
    public VaultSession Session
    {
        set
        {
            _session = value;
            Show();
        }
    }

    private Window Owner => Window.GetWindow(this);

    private RegisteredVault? Chosen => (VaultList.SelectedItem as VaultRow)?.Vault;

    /// <summary>Shows the list again, keeping the vault chosen (at first, the selected one).</summary>
    public void Show()
    {
        if (_session is not { } session)
        {
            return;
        }

        var keep = Chosen?.Location ?? session.Selected?.Location;
        var rows = session.Vaults
            .Select(vault => new VaultRow(vault, IsOpen(vault) ? $"{session.NameOf(vault)} · open" : session.NameOf(vault), vault.Location.Folder))
            .ToList();
        _filling = true;
        try
        {
            VaultList.ItemsSource = rows;
            VaultList.SelectedItem = rows.FirstOrDefault(row => row.Vault.Location == keep) ?? rows.FirstOrDefault();
        }
        finally
        {
            _filling = false;
        }

        HintText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ShowChosen();
    }

    private bool IsOpen(RegisteredVault vault) => vault.Id is { } id && _session?.Current?.Id == id;

    /// <summary>The buttons, name, state and pass phrase of the vault chosen in the list.</summary>
    private void ShowChosen()
    {
        var vault = Chosen;
        var index = VaultList.SelectedIndex;
        RemoveButton.IsEnabled = vault is not null;
        UpButton.IsEnabled = vault is not null && index > 0;
        DownButton.IsEnabled = vault is not null && index < VaultList.Items.Count - 1;
        DetailsPanel.Visibility = vault is null ? Visibility.Collapsed : Visibility.Visible;
        if (_session is not { } session || vault is null)
        {
            return;
        }

        var open = IsOpen(vault);
        NameBox.IsEnabled = open;
        NameBox.Text = session.NameOf(vault);
        ShowNamePlan();
        ShowStatus(session, vault, open);

        var remembered = session.IsRemembered(vault);
        RememberedText.Text = remembered ? "The pass phrase is remembered on this computer." : "The pass phrase is not remembered.";
        ForgetButton.IsEnabled = remembered;
    }

    private void ShowStatus(VaultSession session, RegisteredVault vault, bool open)
    {
        var selected = vault.Location == session.Selected?.Location;
        StatusText.Text = (selected, session.Current) switch
        {
            (_, { } current) when open => $"{OpenText(current)}, measuring its size…",
            (false, _) => "Not open. Choose it in the main window to open it.",
            (_, _) when session.NewerFormat is { } format => $"Closed: written by a newer kofrenot (vault format {format}). Install the latest kofrenot to open it.",
            (_, _) when session.OlderFormat is { } older => $"Not used: in format {older}, it needs an upgrade to format {VaultFormat.Current}. Upgrade it from the main window.",
            _ => "Locked: the pass phrase was not given.",
        };
        if (open && session.Current is { } measured)
        {
            _ = ShowSize(measured, vault.Location.Folder);
        }
    }

    private static string OpenText(Vault vault) => $"Open: {vault.Graph.Nodes.Count} nodes, {vault.Graph.Links.Count} links";

    /// <summary>Measures the vault folder off the UI thread (it may be a slow file share), then adds it to the status.</summary>
    private async Task ShowSize(Vault vault, string folder)
    {
        var measuring = ++_measuringSize;
        string size;
        try
        {
            size = (await Task.Run(() => VaultFolder.SizeOf(folder))).Describe();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warn("Settings", $"The vault folder cannot be measured ({ex.GetType().Name})");
            size = "its size cannot be measured";
        }

        // The list may show another vault, or a newer measure, by now.
        if (measuring == _measuringSize && _session?.Current == vault && Chosen is { } chosen && IsOpen(chosen))
        {
            StatusText.Text = $"{OpenText(vault)} · {size}.";
        }
    }

    /// <summary>What Rename does with the name typed: enabled when it names the open vault anew, the conflict shown.</summary>
    private void ShowNamePlan()
    {
        if (_session?.Current?.Graph is not { } graph || Chosen is not { } chosen || !IsOpen(chosen))
        {
            RenameButton.IsEnabled = false;
            NameStatusText.Text = "Open this vault to rename it. Its name is stored in the vault, so every device shows it.";
            return;
        }

        var plan = new VaultNamePlanner(graph).Plan(NameBox.Text);
        RenameButton.IsEnabled = plan.Change is not null;
        NameStatusText.Text = graph.VaultNames.Count > 1
            ? $"Named differently on two devices: {string.Join(", ", graph.VaultNames.Select(name => name.Name))}. Rename to keep one."
            : "Stored in the vault, so every device shows it.";
    }

    private void VaultList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_filling)
        {
            ShowChosen();
        }
    }

    private void NameBox_TextChanged(object sender, TextChangedEventArgs e) => ShowNamePlan();

    private void NameBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Rename();
            e.Handled = true;
        }
    }

    private void RenameButton_Click(object sender, RoutedEventArgs e) => Rename();

    private void Rename()
    {
        if (_session is { Current.Graph: { } graph } session && new VaultNamePlanner(graph).Plan(NameBox.Text) is { Change: { } change })
        {
            session.Name(Owner, change);
            Show();
        }
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        if (_session is not { } session)
        {
            return;
        }

        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Vault folder" };
        if (dialog.ShowDialog(Owner) != true)
        {
            return;
        }

        if (WhyNotSwitch() is { } reason)
        {
            MessageDialog.Inform(Owner, reason);
            return;
        }

        if (session.Register(Owner, dialog.FolderName) is { } problem)
        {
            Log.Info("Settings", "Vault not added: not a vault folder, or listed already");
            MessageDialog.Inform(Owner, problem);
        }

        VaultList.SelectedItem = null;
        Show();
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_session is not { } session || Chosen is not { } vault)
        {
            return;
        }

        var question = $"Remove “{session.NameOf(vault)}” from the list?\n\nIts folder, and everything in it, stays as it is: add it again to use it. Its remembered pass phrase is forgotten on this computer.";
        if (!MessageDialog.Confirm(Owner, question, "Remove", "Cancel"))
        {
            return;
        }

        if (vault.Location == session.Selected?.Location && WhyNotSwitch() is { } reason)
        {
            MessageDialog.Inform(Owner, reason);
            return;
        }

        session.Unregister(Owner, vault.Location);
        VaultList.SelectedItem = null;
        Show();
    }

    private void UpButton_Click(object sender, RoutedEventArgs e) => Move(-1);

    private void DownButton_Click(object sender, RoutedEventArgs e) => Move(1);

    private void Move(int offset)
    {
        if (_session is { } session && Chosen is { } vault)
        {
            session.Move(vault.Location, offset);
            Show();
        }
    }

    private void ExploreButton_Click(object sender, RoutedEventArgs e)
    {
        if (Chosen is not { Location.Folder: var folder })
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\""));
            Log.Info("Action", $"Explore to the vault folder: {folder}");
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException)
        {
            Log.Error("Action", $"Cannot open {folder}", ex);
            MessageDialog.Inform(Owner, ex.Message);
        }
    }

    private void ForgetButton_Click(object sender, RoutedEventArgs e)
    {
        if (_session is not { } session || Chosen is not { } vault)
        {
            return;
        }

        try
        {
            session.Forget(vault);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            Log.Error("Settings", "Cannot forget the pass phrase", ex);
            MessageDialog.Inform(Owner, ex.Message);
        }

        ShowChosen();
    }
}
