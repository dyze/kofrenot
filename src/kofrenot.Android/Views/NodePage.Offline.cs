using Avalonia.Interactivity;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Storage;

namespace Kofrenot.Phone.Views;

/// <summary>
/// Keep offline: a node's content downloaded ahead on this phone only, never written to the vault,
/// so readers keep nodes offline too. The pin (synced, set on Windows) is only shown.
/// </summary>
public sealed partial class NodePage
{
    private void ShowOffline(Vault vault, Node node)
    {
        var session = _shell.Services.Session;
        OfflineButton.IsVisible = session.CanKeepOffline;
        OfflineButton.Content = session.IsKeptOffline(node.Id) ? $"✓ {vault.OfflineLabel(node.Id)}" : "Keep offline";
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
