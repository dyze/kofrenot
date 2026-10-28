using System.Windows;
using Kofrenot.Core.History;
using Kofrenot.Core.Logging;

namespace Kofrenot.UI.Views;

/// <summary>
/// History, beside the pin: the node's versions (<see cref="HistoryDialog"/>); one chosen is
/// restored, after a confirmation naming what changes, as a new version.
/// </summary>
public partial class NodeDetailsPanel
{
    /// <summary>Restores a version; false when it could not (the user is told).</summary>
    public Func<RestorePlan, Task<bool>>? Restore { get; set; }

    private async void HistoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (Browser is not { Graph: { } graph, Details: { } details } || !LeaveFields())
        {
            return;
        }

        LeaveNote();
        Log.Info("Action", "Node history shown");
        var owner = Window.GetWindow(this);
        if (HistoryDialog.Ask(owner, details.Name, NodeHistory.Of(graph, details.Node)) is not { } entry
            || new RestorePlanner(graph).Plan(details.Node, entry) is not { } plan)
        {
            return;
        }

        var row = new HistoryRow(entry);
        var label = entry.Label.Length > 0 ? $": “{entry.Label}”" : string.Empty;
        if (!MessageDialog.Confirm(owner, $"Restore the {row.What.ToLowerInvariant()} of {row.When}{label}?\n\nIt becomes the current one; the others stay in the history.", "Restore"))
        {
            Log.Info("Action", "Restore cancelled");
            return;
        }

        if (Restore is { } restore && await restore(plan))
        {
            Show();
        }
    }
}
