using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Kofrenot.Core.Graph;
using Kofrenot.Core.History;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Storage;
using Kofrenot.Phone.Platform;

namespace Kofrenot.Phone.Views;

/// <summary>A version of a node: when and what (<see cref="Heading"/>), what it held, and whether it can be restored.</summary>
public sealed record HistoryLine(HistoryEntry Entry, string Heading, string Label, bool CanRestore)
{
    public bool HasLabel => Label.Length > 0;

    public static HistoryLine Of(HistoryEntry entry, bool canWrite) =>
        new(
            entry,
            $"{entry.At.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture)} · {entry.What}{(entry.IsCurrent ? " · current" : string.Empty)}",
            entry.Label,
            canWrite && entry.CanRestore);
}

/// <summary>
/// A node's versions on a screen of their own (⋮ > History… on its page), as kofrenot on Windows
/// lists them; Restore makes one the current one again, after a confirmation.
/// </summary>
public sealed partial class HistoryPage : UserControl, IPage
{
    private readonly Shell _shell;
    private readonly Guid _nodeId;

    public HistoryPage(Shell shell, Node node)
    {
        _shell = shell;
        _nodeId = node.Id;
        InitializeComponent();
        Refresh();
    }

    private Vault? Vault => _shell.Services.Session.Current;

    public bool GoBack() => false;

    public void SyncChanged()
    {
    }

    // Read again: a sync may have added a version, or removed the node (then the screen before).
    public void Refresh()
    {
        if (Vault?.Graph is not { } graph)
        {
            return;
        }

        if (graph.FindNode(_nodeId) is not { } node)
        {
            if (_shell.IsShown(this))
            {
                _shell.Back();
            }

            return;
        }

        Bar.Title = $"History of {node.Name}";
        var canWrite = !_shell.Services.Session.IsReadOnly;
        History.ItemsSource = NodeHistory.Of(graph, node).Select(entry => HistoryLine.Of(entry, canWrite)).ToList();
    }

    private void Restore_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.Tag is HistoryLine line)
        {
            _shell.Run(() => RestoreAsync(line));
        }
    }

    private async Task RestoreAsync(HistoryLine line)
    {
        if (Vault is not { } vault || vault.Graph.FindNode(_nodeId) is not { } node
            || new RestorePlanner(vault.Graph).Plan(node, line.Entry) is not { } plan)
        {
            return;
        }

        var label = line.HasLabel ? $": “{line.Label}”" : string.Empty;
        if (!await _shell.Confirm($"Restore the {line.Entry.What.ToLowerInvariant()} of {line.Heading.Split(" · ")[0]}{label}?\n\nIt becomes the current one; the others stay in the history.", "Restore"))
        {
            _shell.Services.Log.Info("Action", "Restore cancelled");
            return;
        }

        var answers = new PreparedAnswers();
        Busy.IsVisible = true;
        try
        {
            await _shell.Services.Session.Restore(answers, plan);
        }
        finally
        {
            Busy.IsVisible = false;
            _shell.Tell(answers.Told);
            Refresh();
        }
    }
}
