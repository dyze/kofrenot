using Avalonia.Interactivity;
using Avalonia.Media;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Storage;

namespace Kofrenot.Phone.Views;

/// <summary>
/// Keep offline, an action of the top bar (its icon in the accent while kept): a node's content
/// downloaded ahead on this phone only, never written to the vault, so readers keep nodes offline
/// too; where it stands under the node's type. The pin (synced, set on Windows) is only shown.
/// </summary>
public sealed partial class NodePage
{
    private void ShowOffline(Vault vault, Node node)
    {
        var session = _shell.Services.Session;
        var isKept = session.IsKeptOffline(node.Id);
        OfflineButton.IsVisible = session.CanKeepOffline;
        OfflineIcon.Foreground = isKept ? PhoneTheme.Brush("Accent") : null;
        OfflineText.IsVisible = session.CanKeepOffline && isKept;
        OfflineText.Text = isKept ? $"✓ {vault.OfflineLabel(node.Id)}" : null;
    }

    private void Offline_Click(object? sender, RoutedEventArgs e)
    {
        if (_browser.Details?.Node is { } node)
        {
            var session = _shell.Services.Session;
            session.KeepOffline(node.Id, !session.IsKeptOffline(node.Id));
            Refresh();
        }
    }
}
