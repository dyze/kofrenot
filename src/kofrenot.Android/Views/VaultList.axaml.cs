using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Kofrenot.Core.Server;
using Kofrenot.Core.Session;
using Kofrenot.Core.Settings;
using Kofrenot.Phone.Platform;

namespace Kofrenot.Phone.Views;

/// <summary>A server vault on the phone: its name (bold when open), where its sync stands (shown again after each sync) and, shared with this account, its role.</summary>
public sealed class VaultRow(RegisteredVault vault, string name, bool isOpen) : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public RegisteredVault Vault { get; } = vault;

    public string Name { get; } = name;

    public bool IsOpen { get; } = isOpen;

    public FontWeight Weight => IsOpen ? FontWeight.SemiBold : FontWeight.Normal;

    public string Detail { get; private set; } = string.Empty;

    public IBrush Dot { get; private set; } = VaultList.DotOf(SyncLevel.Waiting);

    public void Show(SyncStatus status)
    {
        Detail = Vault.Role is { } role ? $"{status.Summary} · shared, {role.ToString().ToLowerInvariant()}" : status.Summary;
        Dot = VaultList.DotOf(status.Level);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Detail)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Dot)));
    }
}

/// <summary>
/// The server vaults on the phone, on the first screen (<see cref="VaultsPage"/>) and in Settings > Vault:
/// a tap opens one, ✕ removes it from the phone (its copy here, not the server's), asked first.
/// </summary>
public sealed partial class VaultList : UserControl
{
    private Shell? _shell;

    public VaultList()
    {
        InitializeComponent();
    }

    /// <summary>A vault removed from the phone: true when it was the one open.</summary>
    public event Action<bool>? Removed;

    /// <summary>How many vaults it shows.</summary>
    public int Count => Rows.ItemCount;

    /// <summary>The dot of the main window: green synced, grey waiting or offline, red when the user is needed.</summary>
    public static IBrush DotOf(SyncLevel level) => level switch
    {
        SyncLevel.Synced => PhoneTheme.Brush("Synced"),
        SyncLevel.Problem => PhoneTheme.Brush("Problem"),
        _ => PhoneTheme.Brush("Idle"),
    };

    /// <summary>The vaults read again.</summary>
    public void Show(Shell shell)
    {
        _shell = shell;
        var session = shell.Services.Session;
        Rows.ItemsSource = session.Vaults
            .Where(vault => vault.Location.Store is not null)
            .Select(vault => new VaultRow(vault, session.NameOf(vault), isOpen: vault == session.Selected && session.Current is not null))
            .ToList();
        SyncChanged();
    }

    /// <summary>The rows kept, so the list keeps its place.</summary>
    public void SyncChanged()
    {
        foreach (var row in Rows.ItemsSource?.OfType<VaultRow>() ?? [])
        {
            row.Show(_shell!.Services.Sync.StatusOf(row.Vault.Location.Store!));
        }
    }

    private void Open_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.Tag is VaultRow row)
        {
            _shell?.Open(row.Vault.Location);
        }
    }

    private void Remove_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.Tag is VaultRow row && _shell is { } shell)
        {
            shell.Run(() => RemoveAsync(shell, row));
        }
    }

    private async Task RemoveAsync(Shell shell, VaultRow row)
    {
        if (!await shell.Confirm($"Remove {row.Name} from this phone?\n\nThe vault stays on the server. Its copy here is kept: added again, it syncs from where it was.", "Remove"))
        {
            return;
        }

        var answers = new PreparedAnswers();
        shell.Services.Session.Unregister(answers, row.Vault.Location);
        shell.Tell(answers.Told);
        Removed?.Invoke(row.IsOpen);
    }
}
