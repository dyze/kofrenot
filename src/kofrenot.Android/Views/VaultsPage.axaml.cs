using Avalonia.Controls;
using Avalonia.Interactivity;
using Kofrenot.Core.Datasets;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Session;
using Kofrenot.Phone.Platform;

namespace Kofrenot.Phone.Views;

/// <summary>A vault of the server not on the phone yet: by the name its account keeps (else its id), size and role.</summary>
public sealed record StoreRow(StoreChoice Choice, string Name, string Detail);

/// <summary>
/// The first screen while no vault is open: the server vaults on the phone (<see cref="VaultList"/>), and the
/// others of the account, added from the server. Settings > Vault shows it to add one only (<c>adding</c>).
/// </summary>
public sealed partial class VaultsPage : UserControl, IPage
{
    private readonly Shell _shell;
    private readonly bool _onlyAdding;
    private bool _choosing;

    /// <param name="adding">Only to add a vault: the account's vaults listed at once, Back leaving the screen.</param>
    public VaultsPage(Shell shell, bool adding = false)
    {
        _shell = shell;
        _onlyAdding = adding;
        InitializeComponent();
        Vaults.Removed += _ => Refresh();
        Refresh();
        if (adding)
        {
            _shell.Run(ChooseAsync);
        }
    }

    public bool GoBack()
    {
        if (!_choosing || _onlyAdding)
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

        Vaults.Show(_shell);
        Bar.Title = "Vaults";
        Empty.Text = "No vault on this phone yet. Add your server vaults: their notes and files are read and edited here, the rest is changed in kofrenot on Windows.";
        Empty.IsVisible = Vaults.Count == 0;
        AddButton.IsVisible = true;
        SettingsButton.IsVisible = true;
        VaultsScroller.IsVisible = true;
        Stores.IsVisible = false;
    }

    public void SyncChanged() => Vaults.SyncChanged();

    private void Add_Click(object? sender, RoutedEventArgs e) => _shell.Run(ChooseAsync);

    // The account's vaults not on the phone yet, to choose from.
    private async Task ChooseAsync()
    {
        _shell.Services.Log.Info("Action", "Vaults to add listed");
        var answers = new PreparedAnswers();
        SetBusy(true);
        IReadOnlyList<StoreChoice>? stores;
        try
        {
            stores = await _shell.Services.Session.StoresToAdd(answers);
        }
        finally
        {
            SetBusy(false);
        }

        _shell.Tell(answers.Told);
        if (stores is null)
        {
            return;
        }

        _choosing = true;
        Bar.Title = "Add a vault";
        Empty.Text = "A vault shows by its id until it is opened once on a device of yours. Back to cancel.";
        Empty.IsVisible = true;
        AddButton.IsVisible = false;
        SettingsButton.IsVisible = false;
        VaultsScroller.IsVisible = false;
        Stores.IsVisible = true;
        Stores.ItemsSource = stores.Select(choice => new StoreRow(choice, choice.Title, $"{ByteSize.Format(choice.Store.Bytes)}, {choice.Store.Files} files · {choice.Store.Role}")).ToList();
    }

    private void Stores_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Stores.SelectedItem is StoreRow row)
        {
            Stores.SelectedItem = null;
            _shell.Run(() => AddAsync(row.Choice));
        }
    }

    // Downloads it, then opens it: its pass phrase asked when needed.
    private async Task AddAsync(StoreChoice choice)
    {
        var answers = new PreparedAnswers();
        SetBusy(true);
        try
        {
            await _shell.Services.Session.AddFromServer(answers, choice);
        }
        finally
        {
            SetBusy(false);
        }

        _choosing = false;
        if (_shell.Services.Session.Vaults.FirstOrDefault(vault => vault.Location.Store?.Id == choice.Store.Id) is { } added)
        {
            _shell.AfterOpening(answers, added.Location);
        }
        else
        {
            _shell.Tell(answers.Told);
            Refresh();
        }
    }

    private void More_Click(object? sender, RoutedEventArgs e) => _shell.ShowSheet(null, [new("Settings", () => _shell.Push(new SettingsPage(_shell)), Icon: "settings")]);

    private void SetBusy(bool busy)
    {
        Busy.IsVisible = busy;
        AddButton.IsEnabled = !busy;
        Vaults.IsEnabled = Stores.IsEnabled = !busy;
    }
}
