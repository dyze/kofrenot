namespace Kofrenot.Core.Graph;

/// <summary>
/// The nodes and links of a vault, in memory, built by applying its changes. Applying a change
/// twice does nothing, so a change read again (from a snapshot, or later from a pack) is harmless.
/// </summary>
/// <remarks>
/// Nodes with the same name (<see cref="NameKey"/>) are all kept: they are duplicates the user
/// sees. A link repeating another one (same type and nodes) is counted once. A link may arrive
/// before one of its nodes (the sync tool copies files in any order): it is kept, and shown once
/// both nodes are there. A node is shown with its current name (<see cref="NodeNames"/>); a rename
/// may also arrive before its node.
/// </remarks>
public sealed class KnowledgeGraph
{
    private readonly TypeCatalog _types;
    private readonly Dictionary<Guid, Node> _nodes = [];
    private readonly Dictionary<Guid, NodeNames> _names = [];
    private readonly Dictionary<string, List<Node>> _nodesByName = [];
    private readonly Dictionary<Guid, Link> _links = [];
    private readonly Dictionary<LinkKey, Link> _linksByEnds = [];
    private readonly Dictionary<Guid, List<Link>> _linksByNode = [];
    private readonly HashSet<Guid> _removed = [];
    private readonly HashSet<Guid> _removals = [];

    public KnowledgeGraph(TypeCatalog? types = null)
    {
        _types = types ?? TypeCatalog.BuiltIn;
        Aliases = new LinkAliases(_types, _removed);
    }

    /// <summary>The link types, with the user's aliases: what the input box reads.</summary>
    public TypeCatalog Types => Aliases.Catalog;

    public LinkAliases Aliases { get; }

    /// <summary>The nodes, each with its current name.</summary>
    public IReadOnlyCollection<Node> Nodes => _nodes.Values;

    public IReadOnlyCollection<Link> Links => _links.Values;

    /// <summary>The nodes by name key (<see cref="NameKey"/>), each list oldest first: what the search goes through.</summary>
    internal IReadOnlyDictionary<string, List<Node>> NodesByKey => _nodesByName;

    /// <summary>Applies a change. False when it changed nothing (already applied, or a repeated link).</summary>
    public bool Apply(Change change) => change switch
    {
        NodeAdded added => Add(added.Node),
        LinkAdded added => Add(added.Link),
        NodeRenamed renamed => Rename(renamed),
        AliasAdded alias => Aliases.Add(alias),
        ChangesRemoved removal => Remove(removal),
        _ => throw new ArgumentOutOfRangeException(nameof(change), change.GetType().Name, "Unknown change"),
    };

    public Node? FindNode(Guid id) => _nodes.GetValueOrDefault(id);

    /// <summary>
    /// The current names of a node, oldest first: one, or several when it was renamed on two
    /// devices before they synced (a conflict). Empty for an unknown node.
    /// </summary>
    public IReadOnlyList<NameVersion> NamesOf(Guid node) =>
        _names.TryGetValue(node, out var names) && names.Added is not null ? names.Current() : [];

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

    /// <summary>Adds a node (from a change or a snapshot), with the name it was added with. False when it is already there.</summary>
    public bool Add(Node node)
    {
        var names = NamesFor(node.Id);
        if (names.Added is not null)
        {
            return false;
        }

        names.Added = node;
        var shown = names.Shown(node);
        Show(shown == node.Name ? node : node with { Name = shown });
        return true;
    }

    /// <summary>Renames a node (from a change or a snapshot). False when the rename is already there.</summary>
    public bool Rename(NodeRenamed rename)
    {
        var names = NamesFor(rename.Node);
        if (!names.Add(rename))
        {
            return false;
        }

        if (_nodes.TryGetValue(rename.Node, out var node))
        {
            Hide(node);
            Show(node with { Name = names.Shown(names.Added!) });
        }

        return true;
    }

    /// <summary>Takes back aliases or pins. False when this removal is already there.</summary>
    public bool Remove(ChangesRemoved removal)
    {
        if (!_removals.Add(removal.Id))
        {
            return false;
        }

        _removed.UnionWith(removal.Removes);
        Aliases.Changed();
        return true;
    }

    private NodeNames NamesFor(Guid node)
    {
        if (!_names.TryGetValue(node, out var names))
        {
            _names[node] = names = new NodeNames();
        }

        return names;
    }

    private void Show(Node node)
    {
        _nodes[node.Id] = node;
        var key = NameKey.Of(node.Name);
        if (!_nodesByName.TryGetValue(key, out var sameName))
        {
            _nodesByName[key] = sameName = [];
        }

        InsertOldestFirst(sameName, node, other => (other.Created, other.Id));
    }

    private void Hide(Node node)
    {
        var key = NameKey.Of(node.Name);
        var sameName = _nodesByName[key];
        sameName.RemoveAll(other => other.Id == node.Id);
        if (sameName.Count == 0)
        {
            _nodesByName.Remove(key);
        }
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
