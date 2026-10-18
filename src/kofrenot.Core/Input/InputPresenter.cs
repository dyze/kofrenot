using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

/// <summary>
/// A line under the input box: a node, or (indented) one of its links, coming <see cref="From"/>
/// a node. <see cref="Node"/> is the node its details open on: the node, or the link's other end.
/// <see cref="IsChosen"/>: the homonym chosen, or the row selected. A header (<see cref="IsHeader"/>)
/// names the rows under it, while the input box is empty. A node row has its <see cref="Visual"/>.
/// </summary>
public sealed record ResultRow(string Text, bool IsLink, bool IsChosen, Node? Node = null, Node? From = null, bool IsHeader = false, NodeVisual? Visual = null)
{
    internal RowKey? Key => Node is null ? null : new RowKey(From?.Id, Node.Id);
}

/// <summary>Tells a row apart from the others: a node row has no <see cref="From"/>.</summary>
internal readonly record struct RowKey(Guid? From, Guid Node);

/// <summary>
/// What the main window shows for the text of the input box: the request and the plan Enter opens
/// in the creation screen, its preview, and the nodes found. For a link, the side being typed (Tab
/// switches) is the one searched, and Up/Down choose among its homonyms. Otherwise Up/Down select
/// a node or one of its links, whose details Enter opens instead.
/// </summary>
public sealed class InputPresenter
{
    public const string Hint = "Type a name to find or add it, or “Dune relates to Frank Herbert” to link two nodes.";

    private KnowledgeGraph? _graph;
    private string _text = string.Empty;
    private string? _leftName;
    private string? _rightName;
    private int _leftChoice;
    private int _rightChoice;
    private bool _rightActive;
    private RowKey? _selected;

    /// <summary>What the text asks for, or null when it is empty.</summary>
    public InputRequest? Request { get; private set; }

    /// <summary>The homonyms chosen on each side, for the creation screen.</summary>
    public AddChoices Choices => new(_leftChoice, _rightChoice);

    public AddPlan? Plan { get; private set; }

    /// <summary>The nodes last opened on this device, the latest first (<see cref="Browse.RecentNodes"/>): listed while the text is empty.</summary>
    public IReadOnlyList<Guid> Recent { get; set; } = [];

    public string Preview { get; private set; } = Hint;

    public IReadOnlyList<ResultRow> Rows { get; private set; } = [];

    /// <summary>The row selected with Up/Down (Enter opens the details of its <see cref="ResultRow.Node"/>), or null.</summary>
    public ResultRow? SelectedRow => _selected is null ? null : Rows.FirstOrDefault(row => row.Key == _selected);

    /// <summary>
    /// Recomputes everything for this graph and text. The choices are kept while their side keeps
    /// its name, the selection while the text stays the same.
    /// </summary>
    public void Update(KnowledgeGraph graph, string text)
    {
        if (text != _text)
        {
            _selected = null;
        }

        _graph = graph;
        _text = text;
        Request = InputParser.Parse(text, graph.Types);
        if (Request is LinkRequest link)
        {
            KeepChoices(link);
        }
        else
        {
            _leftName = _rightName = null;
            _leftChoice = _rightChoice = 0;
            _rightActive = false;
        }

        Plan = Request is null ? null : new AddPlanner(graph).Plan(Request, Choices);
        Rows = Plan is null ? StartRows(graph) : RowsOf(graph, Plan);
        if (SelectedRow is null)
        {
            _selected = null;
        }

        Preview = PreviewOf(new PlanText(graph), Plan);
    }

    /// <summary>
    /// Up / Down outside a link: selects the next or previous row that opens a node (a node, or one
    /// of its links). Up from the first one
    /// clears the selection. False when it changed nothing.
    /// </summary>
    public bool MoveSelection(int step)
    {
        if (_graph is null || Plan is LinkPlan)
        {
            return false;
        }

        var keys = Rows.Select(row => row.Key).OfType<RowKey>().ToList();
        if (keys.Count == 0)
        {
            return false;
        }

        var index = _selected is { } selected ? keys.IndexOf(selected) + step : step > 0 ? 0 : -1;
        RowKey? next = index < 0 ? null : keys[Math.Min(index, keys.Count - 1)];
        if (next == _selected)
        {
            return false;
        }

        _selected = next;
        Update(_graph, _text);
        return true;
    }

    /// <summary>Up / Down: the previous or next homonym of the side being typed. False when there is no choice.</summary>
    public bool MoveChoice(int step)
    {
        if (_graph is null || Plan is not LinkPlan { } link || ActiveSide(link) is not { Candidates.Count: > 1 } side)
        {
            return false;
        }

        var choice = (side.Choice + step + side.Candidates.Count) % side.Candidates.Count;
        if (_rightActive && link.Right is not null)
        {
            _rightChoice = choice;
        }
        else
        {
            _leftChoice = choice;
        }

        Update(_graph, _text);
        return true;
    }

    /// <summary>Tab: searches and chooses on the other side of the link. False when not typing a link.</summary>
    public bool SwitchSide()
    {
        if (_graph is null || Plan is not LinkPlan { Right: not null })
        {
            return false;
        }

        _rightActive = !_rightActive;
        Update(_graph, _text);
        return true;
    }

    private void KeepChoices(LinkRequest link)
    {
        if (NameKey.Of(link.Left) != (_leftName is null ? null : NameKey.Of(_leftName)))
        {
            _leftChoice = 0;
        }

        if (NameKey.Of(link.Right) != (_rightName is null ? null : NameKey.Of(_rightName)))
        {
            _rightChoice = 0;
            // While the second node is typed, it is the one searched.
            _rightActive = link.Right.Length > 0;
        }

        _leftName = link.Left;
        _rightName = link.Right;
    }

    private PlannedSide? ActiveSide(LinkPlan link) => _rightActive && link.Right is not null ? link.Right : link.Left;

    private string PreviewOf(PlanText text, AddPlan? plan)
    {
        if (SelectedRow?.Node is { } selected)
        {
            return $"Enter to see the details of “{text.NodeLabel(selected)}”. Up/Down: another one.";
        }

        if (plan is null)
        {
            return Hint;
        }

        if (PlanText.Duplicate(plan) is { } duplicate)
        {
            return $"{duplicate} Enter to add another one.";
        }

        if (plan is LinkPlan link && text.Problem(link) is { } problem)
        {
            return problem;
        }

        var preview = $"Enter to add {text.Additions(plan)}.";
        return plan is LinkPlan typed && ActiveSide(typed) is { Candidates.Count: > 1 } side
            ? $"{preview} Up/Down: which “{side.Name}”. Tab: the other side."
            : preview;
    }

    /// <summary>While the input box is empty: the pinned nodes, then the recent ones not pinned, each under a header.</summary>
    private List<ResultRow> StartRows(KnowledgeGraph graph)
    {
        var text = new PlanText(graph);
        var rows = new List<ResultRow>();
        var pinned = graph.PinnedNodes;
        var recent = Recent.Select(graph.FindNode).OfType<Node>().Except(pinned);
        AddSection(rows, "Pinned", pinned.Select(node => NodeRow(graph, text, node, chosen: false)));
        AddSection(rows, "Recent", recent.Select(node => NodeRow(graph, text, node, chosen: false)));
        return rows;
    }

    private void AddSection(List<ResultRow> rows, string header, IEnumerable<ResultRow> section)
    {
        var start = rows.Count;
        rows.AddRange(section.Select(row => row with { IsChosen = row.Key == _selected }));
        if (rows.Count > start)
        {
            rows.Insert(start, new ResultRow(header, IsLink: false, IsChosen: false, IsHeader: true));
        }
    }

    private List<ResultRow> RowsOf(KnowledgeGraph graph, AddPlan plan)
    {
        var text = new PlanText(graph);
        var rows = new List<ResultRow>();
        if (plan is LinkPlan link && ActiveSide(link) is { Candidates.Count: > 1 } side)
        {
            // Homonyms to choose from, each with its links to tell them apart.
            for (var index = 0; index < side.Candidates.Count; index++)
            {
                AddNode(graph, text, rows, side.Candidates[index], chosen: index == side.Choice, withLinks: true);
            }

            return rows;
        }

        var name = plan is LinkPlan typed ? ActiveSide(typed)!.Name : ((NodePlan)plan).Name;
        foreach (var hit in GraphSearch.Find(graph, name))
        {
            AddNode(graph, text, rows, hit.Node, chosen: new RowKey(null, hit.Node.Id) == _selected, withLinks: hit.IsExact);
        }

        return rows;
    }

    private static ResultRow NodeRow(KnowledgeGraph graph, PlanText text, Node node, bool chosen) =>
        new(text.NodeLabel(node), IsLink: false, chosen, node, Visual: NodeVisual.Of(graph, node));

    private void AddNode(KnowledgeGraph graph, PlanText text, List<ResultRow> rows, Node node, bool chosen, bool withLinks)
    {
        rows.Add(NodeRow(graph, text, node, chosen));
        if (!withLinks)
        {
            return;
        }

        foreach (var link in graph.LinksOf(node.Id))
        {
            var other = graph.FindNode(link.Other(node.Id))!;
            var row = new ResultRow($"{graph.Types.LinkName(link, node.Id)} {text.NodeLabel(other)}", IsLink: true, IsChosen: false, other, node);
            rows.Add(row with { IsChosen = row.Key == _selected });
        }
    }
}
