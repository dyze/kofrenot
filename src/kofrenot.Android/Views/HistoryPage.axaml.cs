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
/// A node's versions on a screen of their own (⋮ > History… on its page), or a link's qualifiers' (its sheet),
/// as kofrenot on Windows lists them; Restore makes one the current one again, after a confirmation.
/// </summary>
public sealed partial class HistoryPage : UserControl, IPage
{
    private readonly Shell _shell;
    private readonly FieldHolder _holder;
    private readonly string? _linkTitle;

    public HistoryPage(Shell shell, Node node)
    {
        _shell = shell;
        _holder = node;
        InitializeComponent();
        Refresh();
    }

    /// <param name="title">The link as read from the node shown: "written by Frank Herbert".</param>
    public HistoryPage(Shell shell, Link link, string title)
    {
        _shell = shell;
        _holder = link;
        _linkTitle = title;
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

        var history = _holder.IsLink
            ? graph.FindLink(_holder.Id) is { } link ? NodeHistory.OfLink(graph, link) : null
            : graph.FindNode(_holder.Id) is { } node ? NodeHistory.Of(graph, node) : null;
        if (history is null)
        {
            if (_shell.IsShown(this))
            {
                _shell.Back();
            }

            return;
        }

        Bar.Title = $"History of {_linkTitle ?? graph.FindNode(_holder.Id)!.Name}";
        var canWrite = !_shell.Services.Session.IsReadOnly;
        History.ItemsSource = history.Select(entry => HistoryLine.Of(entry, canWrite)).ToList();
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
        if (Vault is not { } vault || PlanOf(vault.Graph, line.Entry) is not { } plan)
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

    private RestorePlan? PlanOf(KnowledgeGraph graph, HistoryEntry entry) => _holder.IsLink
        ? graph.FindLink(_holder.Id) is { } link ? new RestorePlanner(graph).Plan(link, entry) : null
        : graph.FindNode(_holder.Id) is { } node ? new RestorePlanner(graph).Plan(node, entry) : null;
}
