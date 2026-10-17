using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;

namespace Kofrenot.Core.Browse;

/// <summary>
/// The details screen: the trail of nodes opened from the search, then from link to link, at most
/// <see cref="MaxDepth"/> levels. Each level keeps the link selected (Up/Down), so going back shows
/// it again. Levels keep node ids, and the details are read from the graph each time, so changes
/// (from elsewhere, or a rename) appear.
/// </summary>
public sealed class NodeBrowser
{
    public const int MaxDepth = 4;

    private readonly List<Level> _trail = [];

    /// <summary>The graph browsed; null while closed.</summary>
    public KnowledgeGraph? Graph { get; private set; }

    public bool IsOpen => _trail.Count > 0;

    /// <summary>How many levels are open: 0 for the search.</summary>
    public int Depth => _trail.Count;

    /// <summary>The labels of the open nodes, from level 1 to the current one.</summary>
    public IReadOnlyList<string> Trail => Graph is null ? [] : _trail.Select(level => new PlanText(Graph).NodeLabel(Graph.FindNode(level.NodeId)!)).ToList();

    /// <summary>The node of the current level, or null while closed.</summary>
    public NodeDetails? Details => Graph is null || _trail.Count == 0 ? null : DetailsOf(Graph, _trail[^1]);

    /// <summary>
    /// Opens a node found by the search, as level 1. For a link row of the search, <paramref name="from"/>
    /// is the node it hangs under: it becomes level 1, and <paramref name="node"/> level 2.
    /// </summary>
    public void Open(KnowledgeGraph graph, Node node, Node? from = null)
    {
        Graph = graph;
        _trail.Clear();
        _trail.Add(new Level((from ?? node).Id));
        if (from is not null)
        {
            OpenLinked(node);
        }
    }

    /// <summary>Up / Down among the links. False when it changed nothing.</summary>
    public bool MoveSelection(int step)
    {
        if (Details is not { Links.Count: > 0 } details)
        {
            return false;
        }

        var level = _trail[^1];
        var selected = Math.Clamp(level.Selected + step, 0, details.Links.Count - 1);
        if (selected == level.Selected)
        {
            return false;
        }

        level.Selected = selected;
        return true;
    }

    /// <summary>Enter: opens the selected link's other node one level deeper. False when it cannot.</summary>
    public bool OpenSelected() =>
        Details is { } details && details.Links.FirstOrDefault(link => link.IsSelected) is { } link && OpenLinked(link.OtherNode);

    /// <summary>Opens a node linked to the current one, one level deeper (selecting its link). False when it cannot.</summary>
    public bool OpenLinked(Node node)
    {
        var index = Details?.Links.ToList().FindIndex(link => link.OtherNode.Id == node.Id) ?? -1;
        if (index < 0 || !CanOpen(node))
        {
            return false;
        }

        _trail[^1].Selected = index;
        _trail.Add(new Level(node.Id));
        return true;
    }

    /// <summary>Esc: back one level; from level 1, back to the search.</summary>
    public void Back() => BackTo(Depth - 1);

    /// <summary>Back to this level of the trail (a breadcrumb item); 0 is the search.</summary>
    public void BackTo(int depth)
    {
        depth = Math.Clamp(depth, 0, Depth);
        _trail.RemoveRange(depth, Depth - depth);
        if (_trail.Count == 0)
        {
            Graph = null;
        }
    }

    public void Close() => BackTo(0);

    private bool CanOpen(Node node) => Depth < MaxDepth && _trail.All(level => level.NodeId != node.Id);

    private NodeDetails DetailsOf(KnowledgeGraph graph, Level level)
    {
        var text = new PlanText(graph);
        var node = graph.FindNode(level.NodeId)!;
        var links = graph.LinksOf(node.Id);
        level.Selected = Math.Clamp(level.Selected, 0, Math.Max(0, links.Count - 1));
        var rows = links.Select((link, index) =>
        {
            var other = graph.FindNode(link.Other(node.Id))!;
            return new LinkRow(graph.Types.LinkName(link, node.Id), text.NodeLabel(other), other, CanOpen(other), index == level.Selected);
        });
        var names = graph.NamesOf(node.Id);
        IReadOnlyList<string> conflict = names.Count > 1 ? names.Select(version => version.Name).ToList() : [];
        return new NodeDetails(node, text.NodeLabel(node), graph.Types.NodeTypeName(node.Type), rows.ToList(), conflict, IsPinned: graph.PinsOf(node.Id).Count > 0);
    }

    private sealed class Level(Guid nodeId)
    {
        public Guid NodeId { get; } = nodeId;

        public int Selected { get; set; }
    }
}
