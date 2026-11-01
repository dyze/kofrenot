using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Kofrenot.Core.Browse;

namespace Kofrenot.UI.Views;

/// <summary>
/// The pin (synced: listed while the input box is empty, on every device) and, in a server vault,
/// Keep offline (this computer only: the content downloaded ahead). Each is filled, in the
/// accent colour, when on.
/// </summary>
public partial class NodeDetailsPanel
{
    /// <summary>The open vault's nodes can be kept offline (a server vault).</summary>
    public Func<bool> CanKeepOffline { get; set; } = () => false;

    /// <summary>The node is kept offline on this computer.</summary>
    public Func<Guid, bool> IsKeptOffline { get; set; } = _ => false;

    /// <summary>Keeps the node offline on this computer, or no longer.</summary>
    public Action<Guid, bool>? KeepOffline { get; set; }

    private void ShowPinAndOffline(NodeDetails details)
    {
        ShowOn(PinIcon, "pin", details.IsPinned);
        PinButton.ToolTip = details.IsPinned ? "Pinned: click to unpin" : "Pin: listed while the input box is empty, on every device";

        var canKeep = CanKeepOffline();
        OfflineButton.Visibility = canKeep ? Visibility.Visible : Visibility.Collapsed;
        var kept = canKeep && IsKeptOffline(details.Node.Id);
        ShowOn(OfflineIcon, "offline", kept);
        OfflineButton.ToolTip = kept
            ? $"{OpenVault?.Invoke()?.OfflineLabel(details.Node.Id) ?? "Kept offline"} on this computer: click to stop"
            : "Keep offline: its content downloaded ahead, on this computer only";
    }

    private static void ShowOn(IconView icon, string kind, bool isOn)
    {
        icon.Kind = isOn ? $"{kind}-filled" : kind;
        if (isOn)
        {
            icon.SetResourceReference(IconView.ForegroundProperty, "AccentTextFillColorPrimaryBrush");
        }
        else
        {
            icon.ClearValue(IconView.ForegroundProperty);
        }
    }

    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        if (Browser is { Graph: { } graph, Details: { } details })
        {
            Write?.Invoke(new PinPlanner(graph).Toggle(details.Node), details.IsPinned ? "Node unpinned" : "Node pinned");
            Show();
        }
    }

    private void OfflineButton_Click(object sender, RoutedEventArgs e)
    {
        if (Browser.Details is { } details)
        {
            KeepOffline?.Invoke(details.Node.Id, !IsKeptOffline(details.Node.Id));
            Show();
        }
    }
}
