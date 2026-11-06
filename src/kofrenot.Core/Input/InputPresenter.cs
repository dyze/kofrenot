using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

/// <summary>
/// A line under the input box: a node, or (indented) one of its links, coming <see cref="From"/>
/// a node. <see cref="Node"/> is the node its details open on: the node, or the link's other end.
/// <see cref="IsChosen"/>: the homonym chosen, or the row selected. A header (<see cref="IsHeader"/>)
/// names the rows under it, while the input box is empty. A node row has its <see cref="Visual"/>.
/// <see cref="LinkOption"/>: with qualifiers typed for nodes linked already, a link they may update, or
/// (past the last) a new one beside them (<see cref="LinkPlan.LinkChoice"/>).
/// </summary>
public sealed record ResultRow(string Text, bool IsLink, bool IsChosen, Node? Node = null, Node? From = null, bool IsHeader = false, NodeVisual? Visual = null, string? Detail = null, int? LinkOption = null)
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
    private int _linkChoice;
    private bool _rightActive;
    private RowKey? _selected;
    private IReadOnlyList<LinkHit>? _linkHits;

    /// <summary>What the text asks for, or null when it is empty.</summary>
    public InputRequest? Request { get; private set; }

    /// <summary>The homonyms chosen on each side, for the creation screen.</summary>
    public AddChoices Choices => new(_leftChoice, _rightChoice, Link: _linkChoice);

    public AddPlan? Plan { get; private set; }

    /// <summary>The nodes last opened on this device, the latest first (<see cref="Browse.RecentNodes"/>): listed while the text is empty.</summary>
    public IReadOnlyList<OpenedNode> Recent { get; set; } = [];

    /// <summary>The clock the recent nodes' activity is measured against; the system's, but in tests.</summary>
    public TimeProvider Clock { get; set; } = TimeProvider.System;

    /// <summary>
    /// The room the rows have on screen: while the text is empty, as many recent nodes as fit, in columns
    /// (<see cref="ColumnStarts"/>). Null: <see cref="StartNodes.MaxRecent"/> of them, in one column.
    /// </summary>
    public HomeRoom? Room { get; set; }

    public string Preview { get; private set; } = Hint;

    public IReadOnlyList<ResultRow> Rows { get; private set; } = [];

    /// <summary>The first row of each column (<see cref="HomeColumns"/>): only the empty box's rows have more than one.</summary>
    public IReadOnlyList<int> ColumnStarts { get; private set; } = [0];

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
        _linkHits = LinkSearch.Find(graph, text);
        Request = _linkHits is null ? InputParser.Parse(text, graph) : null;
        if (Request is LinkRequest link)
        {
            KeepChoices(link);
        }
        else
        {
            _leftName = _rightName = null;
            _leftChoice = _rightChoice = _linkChoice = 0;
            _rightActive = false;
        }

        Plan = Request is null ? null : new AddPlanner(graph).Plan(Request, Choices);
        ColumnStarts = [0];
        Rows = _linkHits is { } hits ? HitRows(graph, hits) : Plan is null ? StartRows(graph) : RowsOf(graph, Plan);
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

    /// <summary>
    /// Left / Right, with the rows in columns: selects the row level with the selected one in the
    /// column beside it (<see cref="HomeColumns.Beside"/>). False when it changed nothing.
    /// </summary>
    public bool MoveColumn(int step)
    {
        if (_graph is null || SelectedRow is not { } selected)
        {
            return false;
        }

        var keys = Rows.Select(row => row.Key).ToList();
        if (HomeColumns.Beside(ColumnStarts, keys.Select(key => key is not null).ToList(), keys.IndexOf(selected.Key), step) is not { } next)
        {
            return false;
        }

        _selected = keys[next];
        Update(_graph, _text);
        return true;
    }

    /// <summary>
    /// Up / Down: the previous or next homonym of the side being typed; or, with qualifiers typed for nodes
    /// linked already, the link they update, or a new one. False when there is no choice.
    /// </summary>
    public bool MoveChoice(int step)
    {
        if (_graph is null || Plan is not LinkPlan { } link)
        {
            return false;
        }

        if (ActiveSide(link) is { Candidates.Count: > 1 } side)
        {
            Choose(link, (side.Choice + step + side.Candidates.Count) % side.Candidates.Count);
            return true;
        }

        if (!link.HasLinkChoice)
        {
            return false;
        }

        var options = link.Between.Count + 1;
        _linkChoice = (link.LinkChoice + step + options) % options;
        Update(_graph, _text);
        return true;
    }

    /// <summary>
    /// A click on a row: selects it, as Up/Down would. While a link is typed, a click on a homonym
    /// of the side being typed chooses it instead. False when it changed nothing, or the row
    /// cannot be selected (a header, a link row then, or a row no longer listed).
    /// </summary>
    public bool Select(ResultRow row)
    {
        if (_graph is not null && row.LinkOption is { } option && Plan is LinkPlan { HasLinkChoice: true } typed && option != typed.LinkChoice && Rows.Contains(row))
        {
            _linkChoice = option;
            Update(_graph, _text);
            return true;
        }

        if (_graph is null || row.Key is not { } key || !Rows.Any(listed => listed.Key == key))
        {
            return false;
        }

        if (Plan is LinkPlan link)
        {
            var candidates = ActiveSide(link)!.Candidates;
            var index = row.From is null ? candidates.ToList().FindIndex(candidate => candidate.Id == key.Node) : -1;
            if (candidates.Count < 2 || index < 0 || index == ActiveSide(link)!.Choice)
            {
                return false;
            }

            Choose(link, index);
            return true;
        }

        if (key == _selected)
        {
            return false;
        }

        _selected = key;
        Update(_graph, _text);
        return true;
    }

    private void Choose(LinkPlan link, int choice)
    {
        if (_rightActive && link.Right is not null)
        {
            _rightChoice = choice;
        }
        else
        {
            _leftChoice = choice;
        }

        Update(_graph!, _text);
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

        if (NameKey.Of(link.Left) != (_leftName is null ? null : NameKey.Of(_leftName))
            || NameKey.Of(link.Right) != (_rightName is null ? null : NameKey.Of(_rightName)))
        {
            _linkChoice = 0;
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

        if (_linkHits is { } hits)
        {
            return hits.Count switch
            {
                0 => "No link has this qualifier.",
                1 => "1 link found. Down, then Enter to see its details.",
                _ => $"{hits.Count} links found. Up/Down, then Enter to see the details of one.",
            };
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

        var preview = $"Enter to {text.Action(plan)}.";
        return plan is LinkPlan typed && ActiveSide(typed) is { Candidates.Count: > 1 } side
            ? $"{preview} Up/Down: which “{side.Name}”. Tab: the other side."
            : plan is LinkPlan { HasLinkChoice: true } ? $"{preview} Up/Down: which link."
            : preview;
    }

    /// <summary>
    /// While the input box is empty: the pinned nodes, then the recent ones (opened here or changed on
    /// any device, with their latest activity), each under a header (<see cref="StartNodes"/>). With a
    /// <see cref="Room"/>, every pinned node, and the recent ones that fit, in columns.
    /// </summary>
    private List<ResultRow> StartRows(KnowledgeGraph graph)
    {
        var text = new PlanText(graph);
        var rows = new List<ResultRow>();
        var start = StartNodes.Of(graph, Recent, Clock.GetUtcNow(), Room?.MaxRows ?? StartNodes.MaxRecent);
        AddSection(rows, "Pinned", start.Pinned.Select(node => NodeRow(graph, text, node, chosen: false)));
        var pinned = rows.Count;
        AddSection(rows, "Recent", start.Recent.Select(recent => NodeRow(graph, text, recent.Node, chosen: false) with { Detail = recent.Detail }));
        if (Room is not { } room)
        {
            return rows;
        }

        var columns = HomeColumns.Of(rows.Select(row => row.IsHeader).ToList(), room, kept: pinned);
        ColumnStarts = columns.Starts;
        return rows.Take(columns.Shown).ToList();
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

        if (plan is LinkPlan { HasLinkChoice: true } linked)
        {
            return LinkOptions(graph, text, linked);
        }

        var name = plan is LinkPlan typed ? ActiveSide(typed)!.Name : ((NodePlan)plan).Name;
        foreach (var hit in GraphSearch.Find(graph, name))
        {
            AddNode(graph, text, rows, hit.Node, chosen: new RowKey(null, hit.Node.Id) == _selected, withLinks: hit.IsExact, hit.Field);
        }

        return rows;
    }

    // The links found by a qualifier, as read, each opening the node it leads to.
    private List<ResultRow> HitRows(KnowledgeGraph graph, IReadOnlyList<LinkHit> hits)
    {
        var text = new PlanText(graph);
        return hits
            .Select(hit => new ResultRow(
                $"{text.NodeLabel(hit.From)} {hit.Verb?.Name ?? hit.Link.Type} {text.NodeLabel(hit.To)}",
                IsLink: false,
                IsChosen: false,
                hit.Found,
                hit.Found == hit.From ? hit.To : hit.From,
                Visual: NodeVisual.Of(graph, hit.Found),
                Detail: $"{hit.Field.Label}: {hit.Field.Display}"))
            .Select(row => row with { IsChosen = row.Key == _selected })
            .ToList();
    }

    // The links the qualifiers typed may update, each with its qualifier shown, then a new one.
    private static List<ResultRow> LinkOptions(KnowledgeGraph graph, PlanText text, LinkPlan link)
    {
        var rows = link.Between
            .Select((existing, index) => new ResultRow(
                $"{link.Verb.Name} {text.Label(link.Right!)}{(NodeFields.ShownOf(graph, existing) is { } shown ? $" · {shown.Display}" : string.Empty)}",
                IsLink: true,
                IsChosen: index == link.LinkChoice,
                LinkOption: index))
            .ToList();
        rows.Add(new ResultRow("+ a new link", IsLink: true, IsChosen: link.LinkChoice == link.Between.Count, LinkOption: link.Between.Count));
        return rows;
    }

    private static ResultRow NodeRow(KnowledgeGraph graph, PlanText text, Node node, bool chosen) =>
        new(text.NodeLabel(node), IsLink: false, chosen, node, Visual: NodeVisual.Of(graph, node));

    // A node found by a field's value has that field as its detail ("Born: 1920-10-08").
    private void AddNode(KnowledgeGraph graph, PlanText text, List<ResultRow> rows, Node node, bool chosen, bool withLinks, string? field = null)
    {
        rows.Add(NodeRow(graph, text, node, chosen) with { Detail = field });
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
