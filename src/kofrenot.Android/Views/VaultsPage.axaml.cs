using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Kofrenot.Core.Datasets;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Server;
using Kofrenot.Core.Settings;
using Kofrenot.Phone.Platform;

namespace Kofrenot.Phone.Views;

/// <summary>A server vault on the phone: its name and where its sync stands.</summary>
public sealed record VaultRow(RegisteredVault Vault, string Name, string Detail, IBrush Dot);

/// <summary>A vault of the server not on the phone yet: its name is in the vault, so shown by its id, size and role.</summary>
public sealed record StoreRow(ServerStore Store, string Name, string Detail);

/// <summary>
/// The server vaults on the phone: a tap opens one, ✕ removes it from the phone (its copy here, not
/// the server's); the others of the account are added from the server.
/// </summary>
public sealed partial class VaultsPage : UserControl, IPage
{
    private readonly Shell _shell;
    private bool _choosing;

    public VaultsPage(Shell shell)
    {
        _shell = shell;
        InitializeComponent();
        Refresh();
    }

    public bool GoBack()
    {
        if (!_choosing)
        {
            return false;
        }

        _choosing = false;
        Refresh();
        return true;
    }

    public void Refresh()
    {
        if (_choosing)
        {
            return;
        }

        var session = _shell.Services.Session;
        var rows = session.Vaults
            .Where(vault => vault.Location.Store is not null)
            .Select(vault => RowOf(vault, _shell.Services.Sync.StatusOf(vault.Location.Store!)))
            .ToList();
        Title.Text = "Vaults";
        List.ItemsSource = rows;
        Empty.Text = "No vault on this phone yet. Add your server vaults: they are read here, and changed in kofrenot on Windows.";
        Empty.IsVisible = rows.Count == 0;
        AddButton.IsVisible = true;
        SettingsButton.IsVisible = true;
    }

    private VaultRow RowOf(RegisteredVault vault, SyncStatus status) =>
        new(vault, _shell.Services.Session.NameOf(vault), status.Summary, DotOf(status.Level));

    /// <summary>The dot of the main window: green synced, grey waiting or offline, red when the user is needed.</summary>
    public static IBrush DotOf(SyncLevel level) => level switch
    {
        SyncLevel.Synced => Brushes.MediumSeaGreen,
        SyncLevel.Problem => Brushes.IndianRed,
        _ => Brushes.Gray,
    };

    private async void Add_Click(object? sender, RoutedEventArgs e)
    {
        var answers = new PreparedAnswers();
        SetBusy(true);
        var stores = await _shell.Services.Session.StoresToAdd(answers);
        SetBusy(false);
        _shell.Tell(answers.Told);
        if (stores is null)
        {
            return;
        }

        _choosing = true;
        Title.Text = "Add a vault";
        Empty.Text = "Vaults are named inside them: a vault not added yet shows its size and your role. Back to cancel.";
        Empty.IsVisible = true;
        AddButton.IsVisible = false;
        SettingsButton.IsVisible = false;
        List.ItemsSource = stores.Select(store => new StoreRow(store, $"Vault {store.Id.ToString()[..8]}", $"{ByteSize.Format(store.Bytes)}, {store.Files} files · {store.Role}")).ToList();
    }

    private async void List_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var selected = List.SelectedItem;
        List.SelectedItem = null;
        switch (selected)
        {
            case VaultRow row:
                _shell.Open(row.Vault.Location);
                break;
            case StoreRow row:
                await AddAsync(row.Store);
                break;
        }
    }

    // Downloads it, then opens it: its pass phrase asked when needed.
    private async Task AddAsync(ServerStore store)
    {
        var answers = new PreparedAnswers();
        SetBusy(true);
        await _shell.Services.Session.AddFromServer(answers, store);
        SetBusy(false);
        _choosing = false;
        if (_shell.Services.Session.Vaults.FirstOrDefault(vault => vault.Location.Store?.Id == store.Id) is { } added)
        {
            _shell.AfterOpening(answers, added.Location);
        }
        else
        {
            _shell.Tell(answers.Told);
            Refresh();
        }
    }

    private async void Remove_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.Tag is not VaultRow row
            || !await _shell.Confirm($"Remove {row.Name} from this phone?\n\nThe vault stays on the server. Its copy here is kept: added again, it syncs from where it was.", "Remove"))
        {
            return;
        }

        var answers = new PreparedAnswers();
        _shell.Services.Session.Unregister(answers, row.Vault.Location);
        _shell.Tell(answers.Told);
        Refresh();
    }

    private void Settings_Click(object? sender, RoutedEventArgs e) => _shell.Push(new SettingsPage(_shell));

    private void SetBusy(bool busy)
    {
        Busy.IsVisible = busy;
        AddButton.IsEnabled = !busy;
        List.IsEnabled = !busy;
    }
}
