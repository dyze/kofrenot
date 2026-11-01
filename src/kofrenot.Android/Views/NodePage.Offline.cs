using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Kofrenot.Core.Graph;

namespace Kofrenot.Phone.Views;

/// <summary>
/// Keep offline: a node's content downloaded ahead on this phone only, never written to the vault,
/// so readers keep nodes offline too. An action of the top bar (its icon filled, in the accent, while kept)
/// and of ⋮. The pin (synced, set on Windows) is only shown.
/// </summary>
public sealed partial class NodePage
{
    private void ShowOffline(Node node)
    {
        var session = _shell.Services.Session;
        OfflineButton.IsVisible = session.CanKeepOffline;
        // Not kept: the bar's own colour (a null brush would draw nothing).
        var kept = session.IsKeptOffline(node.Id);
        OfflineIcon.Data = PhoneTheme.Icon(kept ? "offline-filled" : "offline");
        if (kept)
        {
            OfflineIcon.Foreground = PhoneTheme.Brush("Accent");
        }
        else
        {
            OfflineIcon.ClearValue(TemplatedControl.ForegroundProperty);
        }
    }

    // ⋮'s choice, saying what it does now; none where nothing is fetched on demand.
    private SheetChoice? OfflineChoice(Node node) => _shell.Services.Session.CanKeepOffline
        ? new SheetChoice(_shell.Services.Session.IsKeptOffline(node.Id) ? "Don't keep offline" : "Keep offline", ToggleOffline, Icon: "offline")
        : null;

    private void Offline_Click(object? sender, RoutedEventArgs e) => ToggleOffline();

    private void ToggleOffline()
    {
        if (_browser.Details?.Node is { } node)
        {
            var session = _shell.Services.Session;
            session.KeepOffline(node.Id, !session.IsKeptOffline(node.Id));
            Refresh();
        }
    }
}
