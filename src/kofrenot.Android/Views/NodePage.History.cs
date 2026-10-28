using System.Globalization;
using Kofrenot.Core.Graph;
using Kofrenot.Core.History;

namespace Kofrenot.Phone.Views;

/// <summary>A version on a node's page: when and what (<see cref="Heading"/>), and what it held.</summary>
public sealed record HistoryLine(string Heading, string Label)
{
    public bool HasLabel => Label.Length > 0;

    public static HistoryLine Of(HistoryEntry entry) =>
        new($"{entry.At.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture)} · {entry.What}{(entry.IsCurrent ? " · current" : string.Empty)}", entry.Label);
}

/// <summary>A node's versions, read only, as kofrenot on Windows lists them: restoring one is a change this kofrenot does not make.</summary>
public sealed partial class NodePage
{
    private void ShowHistory(KnowledgeGraph graph, Node node) =>
        History.ItemsSource = NodeHistory.Of(graph, node).Select(HistoryLine.Of).ToList();
}
