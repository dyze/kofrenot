using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;

namespace Kofrenot.UI.Views;

/// <summary>A line under the input box: a node, or (indented) one of its links.</summary>
public sealed record ResultRow(string Text, bool IsLink, bool IsChosen);

/// <summary>
/// What the main window shows for the text of the input box: the plan of Enter, its preview, and
/// the nodes found. For a link, the side being typed (Tab switches) is the one searched, and
/// Up/Down choose among its homonyms.
/// </summary>
public sealed class InputPresenter
{
    private const string Hint = "Type a name to find or add it, or “Dune relates to Frank Herbert” to link two nodes.";

    private readonly TypeCatalog _types = TypeCatalog.BuiltIn;
    private KnowledgeGraph? _graph;
    private string _text = string.Empty;
    private string? _leftName;
    private string? _rightName;
    private int _leftChoice;
    private int _rightChoice;
    private bool _rightActive;

    public AddPlan? Plan { get; private set; }

    public string Preview { get; private set; } = Hint;

    public IReadOnlyList<ResultRow> Rows { get; private set; } = [];

    /// <summary>Recomputes everything for this graph and text. The choices are kept while their side keeps its name.</summary>
    public void Update(KnowledgeGraph graph, string text)
    {
        _graph = graph;
        _text = text;
        var request = InputParser.Parse(text, _types);
        if (request is LinkRequest link)
        {
            KeepChoices(link);
        }
        else
        {
            _leftName = _rightName = null;
            _leftChoice = _rightChoice = 0;
            _rightActive = false;
        }

        Plan = request is null ? null : new AddPlanner(graph).Plan(request, _leftChoice, _rightChoice);
        Preview = Plan is null ? Hint : PreviewOf(Plan);
        Rows = Plan is null ? [] : RowsOf(graph, Plan);
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

    private string PreviewOf(AddPlan plan) => plan switch
    {
        NodePlan { Existing.Count: 0 } node => $"Enter adds the node “{node.Name}”.",
        NodePlan node => $"“{node.Name}” exists already{(node.Existing.Count > 1 ? $" ({node.Existing.Count} times)" : string.Empty)}. Enter adds another one, after confirmation.",
        LinkPlan { Problem: LinkProblem.SecondNodeMissing } link => $"Type the node to link “{Label(link.Left)}” to.",
        LinkPlan { Problem: LinkProblem.SameNode } => "A node cannot be linked to itself.",
        LinkPlan { Problem: LinkProblem.AlreadyLinked } link => $"“{Label(link.Left)}” {link.Type.Name} “{Label(link.Right!)}” already.",
        LinkPlan link => LinkPreview(link),
        _ => string.Empty,
    };

    private string LinkPreview(LinkPlan link)
    {
        var parts = new[] { link.Left, link.Right! }
            .Where(side => side.IsNew)
            .Select(side => $"the node “{side.Name}”")
            .Append($"the link “{Label(link.Left)} {link.Type.Name} {Label(link.Right!)}”");
        var preview = $"Enter adds {string.Join(", ", parts)}.";
        return ActiveSide(link) is { Candidates.Count: > 1 } side
            ? $"{preview} Up/Down: which “{side.Name}”. Tab: the other side."
            : preview;
    }

    private string Label(PlannedSide side) => side.Chosen is { } node ? node.Name + MarkOf(node) : side.Name;

    private IReadOnlyList<ResultRow> RowsOf(KnowledgeGraph graph, AddPlan plan)
    {
        var rows = new List<ResultRow>();
        if (plan is LinkPlan link && ActiveSide(link) is { Candidates.Count: > 1 } side)
        {
            // Homonyms to choose from, each with its links to tell them apart.
            for (var index = 0; index < side.Candidates.Count; index++)
            {
                AddNode(rows, side.Candidates[index], chosen: index == side.Choice, withLinks: true);
            }

            return rows;
        }

        var name = plan switch
        {
            NodePlan node => node.Name,
            LinkPlan linkPlan => ActiveSide(linkPlan)!.Name,
            _ => string.Empty,
        };
        foreach (var hit in GraphSearch.Find(graph, name))
        {
            AddNode(rows, hit.Node, chosen: false, withLinks: hit.IsExact);
        }

        return rows;
    }

    private void AddNode(List<ResultRow> rows, Node node, bool chosen, bool withLinks)
    {
        rows.Add(new ResultRow(node.Name + MarkOf(node), IsLink: false, chosen));
        if (!withLinks || _graph is null)
        {
            return;
        }

        foreach (var link in _graph.LinksOf(node.Id))
        {
            var other = _graph.FindNode(link.Other(node.Id))!;
            var type = _types.FindLinkType(link.Type)?.Name ?? link.Type;
            rows.Add(new ResultRow($"{type} {other.Name}{MarkOf(other)}", IsLink: true, IsChosen: false));
        }
    }

    // "²" when several nodes have this name: its place among them, oldest first.
    private string MarkOf(Node node)
    {
        var homonyms = _graph?.NodesNamed(node.Name) ?? [];
        return homonyms.Count > 1 ? SearchHit.Superscript(homonyms.ToList().IndexOf(node) + 1) : string.Empty;
    }
}
