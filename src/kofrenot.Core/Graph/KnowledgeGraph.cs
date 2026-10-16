namespace Kofrenot.Core.Graph;

/// <summary>
/// The nodes and links of a database, in memory, built by applying its changes. Applying a change
/// twice does nothing, so a change read again (from a snapshot, or later from a pack) is harmless.
/// </summary>
/// <remarks>
/// Nodes with the same name (<see cref="NameKey"/>) are all kept: they are duplicates the user
/// sees. A link repeating another one (same type and nodes) is counted once. A link may arrive
/// before one of its nodes (the sync tool copies files in any order): it is kept, and shown once
/// both nodes are there.
/// </remarks>
public sealed class KnowledgeGraph
{
    private readonly TypeCatalog _types;
    private readonly Dictionary<Guid, Node> _nodes = [];
    private readonly Dictionary<string, List<Node>> _nodesByName = [];
    private readonly Dictionary<Guid, Link> _links = [];
    private readonly Dictionary<LinkKey, Link> _linksByEnds = [];
    private readonly Dictionary<Guid, List<Link>> _linksByNode = [];

    public KnowledgeGraph(TypeCatalog? types = null) => _types = types ?? TypeCatalog.BuiltIn;

    public IReadOnlyCollection<Node> Nodes => _nodes.Values;

    public IReadOnlyCollection<Link> Links => _links.Values;

    /// <summary>The nodes by name key (<see cref="NameKey"/>), each list oldest first: what the search goes through.</summary>
    internal IReadOnlyDictionary<string, List<Node>> NodesByKey => _nodesByName;

    /// <summary>Applies a change. False when it changed nothing (already applied, or a repeated link).</summary>
    public bool Apply(Change change) => change switch
    {
        NodeAdded added => Add(added.Node),
        LinkAdded added => Add(added.Link),
        _ => throw new ArgumentOutOfRangeException(nameof(change), change.GetType().Name, "Unknown change"),
    };

    public Node? FindNode(Guid id) => _nodes.GetValueOrDefault(id);

    /// <summary>The nodes with this name (case, accents and spaces ignored), oldest first.</summary>
    public IReadOnlyList<Node> NodesNamed(string name) =>
        _nodesByName.TryGetValue(NameKey.Of(name), out var nodes) ? nodes : [];

    /// <summary>The links of a node whose other node is known, oldest first.</summary>
    public IReadOnlyList<Link> LinksOf(Guid node) =>
        _linksByNode.TryGetValue(node, out var links)
            ? links.Where(link => _nodes.ContainsKey(link.Other(node))).ToList()
            : [];

    /// <summary>The link of this type between these nodes, or null. Either direction for a symmetric type.</summary>
    public Link? FindLink(string type, Guid from, Guid to) => _linksByEnds.GetValueOrDefault(KeyOf(type, from, to));

    /// <summary>Adds a node (from a change or a snapshot). False when it is already there.</summary>
    public bool Add(Node node)
    {
        if (!_nodes.TryAdd(node.Id, node))
        {
            return false;
        }

        var key = NameKey.Of(node.Name);
        if (!_nodesByName.TryGetValue(key, out var sameName))
        {
            _nodesByName[key] = sameName = [];
        }

        InsertOldestFirst(sameName, node, other => (other.Created, other.Id));
        return true;
    }

    /// <summary>Adds a link (from a change or a snapshot). False when it is already there, or repeats another one.</summary>
    public bool Add(Link link)
    {
        if (_links.ContainsKey(link.Id) || !_linksByEnds.TryAdd(KeyOf(link.Type, link.From, link.To), link))
        {
            return false;
        }

        _links[link.Id] = link;
        AddToNode(link.From, link);
        if (link.To != link.From)
        {
            AddToNode(link.To, link);
        }

        return true;
    }

    private void AddToNode(Guid node, Link link)
    {
        if (!_linksByNode.TryGetValue(node, out var links))
        {
            _linksByNode[node] = links = [];
        }

        InsertOldestFirst(links, link, other => (other.Created, other.Id));
    }

    // Changes arrive in any order: keeps the list sorted by creation, then id (a stable order on every device).
    private static void InsertOldestFirst<T>(List<T> items, T item, Func<T, (DateTimeOffset Created, Guid Id)> order)
    {
        var position = order(item);
        var index = items.FindIndex(other => order(other).CompareTo(position) > 0);
        items.Insert(index < 0 ? items.Count : index, item);
    }

    private LinkKey KeyOf(string type, Guid from, Guid to)
    {
        var symmetric = _types.FindLinkType(type)?.IsSymmetric ?? false;
        return symmetric && from.CompareTo(to) > 0 ? new LinkKey(type, to, from) : new LinkKey(type, from, to);
    }

    private readonly record struct LinkKey(string Type, Guid First, Guid Second);
}
