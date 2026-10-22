namespace Kofrenot.Core.Graph;

/// <summary>
/// The nodes and links of a vault, in memory, built by applying its changes. Applying a change
/// twice does nothing, so a change read again (from a snapshot, or later from a pack) is harmless.
/// </summary>
/// <remarks>
/// Nodes with the same name (<see cref="NameKey"/>) are all kept: they are duplicates the user
/// sees. A link repeating another one (same type and nodes) is counted once (<see cref="LinkIndex"/>).
/// A link may arrive before one of its nodes (the sync tool copies files in any order): it is kept,
/// and shown once both nodes are there. A node is shown with its current name (<see cref="NodeNames"/>);
/// a rename may also arrive before its node. A removed node or link is no longer shown, whatever
/// arrives after: a removal wins.
/// </remarks>
public sealed class KnowledgeGraph
{
    private readonly Dictionary<Guid, Node> _nodes = [];
    private readonly Dictionary<Guid, NodeNames> _names = [];
    private readonly NodeTypes _types = new();
    private readonly Dictionary<string, List<Node>> _nodesByName = [];
    private readonly LinkIndex _links;
    private readonly HashSet<Guid> _removed = [];
    private readonly HashSet<Guid> _removals = [];
    private readonly RemovableChanges<NodePinned> _pins;
    private readonly ContentVersions<NoteEdited> _notes = new();
    private readonly ContentVersions<DatasetEdited> _datasets = new();
    private readonly ContentVersions<ViewEdited> _views = new();
    private readonly Dictionary<Guid, VaultNamed> _vaultNames = [];
    private readonly RemovableChanges<MemberAdded> _members;

    public KnowledgeGraph(TypeCatalog? types = null)
    {
        var catalog = types ?? TypeCatalog.BuiltIn;
        Aliases = new LinkAliases(catalog, _removed);
        _links = new LinkIndex(catalog, _removed);
        _pins = new RemovableChanges<NodePinned>(_removed);
        _members = new RemovableChanges<MemberAdded>(_removed);
    }

    /// <summary>The link types, with the user's aliases: what the input box reads.</summary>
    public TypeCatalog Types => Aliases.Catalog;

    public LinkAliases Aliases { get; }

    /// <summary>The nodes, each with its current name.</summary>
    public IReadOnlyCollection<Node> Nodes => _nodes.Values;

    public IReadOnlyCollection<Link> Links => _links.Shown;

    /// <summary>The nodes by name key (<see cref="NameKey"/>), each list oldest first: what the search goes through.</summary>
    internal IReadOnlyDictionary<string, List<Node>> NodesByKey => _nodesByName;

    /// <summary>Applies a change. False when it changed nothing (already applied, or a repeated link).</summary>
    public bool Apply(Change change) => change switch
    {
        NodeAdded added => Add(added.Node),
        LinkAdded added => Add(added.Link),
        NodeRenamed renamed => Rename(renamed),
        NodeRetyped retyped => Retype(retyped),
        AliasAdded alias => Aliases.Add(alias),
        ChangesRemoved removal => Remove(removal),
        NodePinned pin => _pins.Add(pin),
        NoteEdited version => _notes.Add(version),
        DatasetEdited version => _datasets.Add(version),
        ViewEdited version => _views.Add(version),
        VaultNamed name => _vaultNames.TryAdd(name.Id, name),
        MemberAdded member => _members.Add(member),
        _ => throw new ArgumentOutOfRangeException(nameof(change), change.GetType().Name, "Unknown change"),
    };

    public Node? FindNode(Guid id) => _nodes.GetValueOrDefault(id);

    /// <summary>The pinned nodes, each once, the first pinned first. A pin of a node not arrived yet shows nothing.</summary>
    public IReadOnlyList<Node> PinnedNodes =>
        _pins.Current.Select(pin => pin.Node).Distinct().Select(FindNode).OfType<Node>().ToList();

    /// <summary>
    /// The current content versions of a note, oldest first: none before its first save, one, or
    /// several when it was edited on two devices before they synced (a conflict).
    /// </summary>
    public IReadOnlyList<NoteEdited> NoteVersionsOf(Guid node) => _notes.Current(node);

    /// <summary>The current file versions of a dataset, oldest first, as <see cref="NoteVersionsOf"/>.</summary>
    public IReadOnlyList<DatasetEdited> DatasetVersionsOf(Guid node) => _datasets.Current(node);

    /// <summary>The current definitions of a view, oldest first, as <see cref="NoteVersionsOf"/>.</summary>
    public IReadOnlyList<ViewEdited> ViewVersionsOf(Guid node) => _views.Current(node);

    /// <summary>
    /// The current names of the vault, oldest first: none before it is named, one, or several when
    /// it was named on two devices before they synced (a conflict).
    /// </summary>
    public IReadOnlyList<VaultNamed> VaultNames => CurrentVersions.Of(_vaultNames.Values);

    /// <summary>The vault's name shown: the latest current one; null before it is named.</summary>
    public string? VaultName => VaultNames.LastOrDefault()?.Name;

    /// <summary>The members of a shared vault, each by its latest record; none before it is shared.</summary>
    public IReadOnlyList<VaultMember> Members =>
        _members.Current.GroupBy(added => added.Member.Account).Select(records => records.Last().Member).ToList();

    /// <summary>The records of a member (to remove them all).</summary>
    public IReadOnlyList<Guid> MemberRecordsOf(Guid account) =>
        _members.Current.Where(added => added.Member.Account == account).Select(added => added.Id).ToList();

    /// <summary>The pins of a node: several when it was pinned on two devices. Unpinning removes them all.</summary>
    public IReadOnlyList<Guid> PinsOf(Guid node) =>
        _pins.Current.Where(pin => pin.Node == node).Select(pin => pin.Id).ToList();

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
        _links.Of(node).Where(link => _nodes.ContainsKey(link.Other(node))).ToList();

    /// <summary>The link of this type between these nodes, or null. Either direction for a symmetric type.</summary>
    public Link? FindLink(string type, Guid from, Guid to) => _links.Find(type, from, to);

    /// <summary>
    /// The ids of a link and of its copies (the same link added on two devices before they synced),
    /// oldest first: what removes it.
    /// </summary>
    public IReadOnlyList<Guid> CopiesOf(Link link) => _links.CopiesOf(link);

    /// <summary>Adds a node (from a change or a snapshot), with the name it was added with. False when it is already there.</summary>
    public bool Add(Node node)
    {
        var names = NamesFor(node.Id);
        if (names.Added is not null)
        {
            return false;
        }

        names.Added = node;
        if (!_removed.Contains(node.Id))
        {
            Show(Shown(node));
        }

        return true;
    }

    /// <summary>Gives a node another type (from a change or a snapshot). False when the retype is already there.</summary>
    public bool Retype(NodeRetyped retype)
    {
        if (!_types.Add(retype))
        {
            return false;
        }

        if (_nodes.TryGetValue(retype.Node, out var node))
        {
            Hide(node);
            Show(Shown(_names[retype.Node].Added!));
        }

        return true;
    }

    /// <summary>
    /// The current types of a node, oldest first: one, or several when its type was changed on two
    /// devices before they synced (a conflict). Empty for an unknown node.
    /// </summary>
    public IReadOnlyList<TypeVersion> TypesOf(Guid node) =>
        _names.TryGetValue(node, out var names) && names.Added is { } added ? _types.Current(added) : [];

    /// <summary>The retypes of a node, whatever they replace: when it last changed.</summary>
    public IEnumerable<NodeRetyped> RetypesOf(Guid node) => _types.Of(node);

    /// <summary><paramref name="added"/> as shown: its current name and type.</summary>
    private Node Shown(Node added)
    {
        var (name, type) = (_names[added.Id].Shown(added), _types.Shown(added));
        return name == added.Name && type == added.Type ? added : added with { Name = name, Type = type };
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
            Show(Shown(names.Added!));
        }

        return true;
    }

    /// <summary>Takes back nodes, links, aliases or pins. False when this removal is already there.</summary>
    public bool Remove(ChangesRemoved removal)
    {
        if (!_removals.Add(removal.Id))
        {
            return false;
        }

        _removed.UnionWith(removal.Removes);
        foreach (var id in removal.Removes)
        {
            if (_nodes.Remove(id, out var node))
            {
                Hide(node);
            }
        }

        _links.Remove(removal.Removes);
        Aliases.Changed();
        return true;
    }

    /// <summary>Adds a link (from a change or a snapshot). False when it is already there, removed, or repeats another one.</summary>
    public bool Add(Link link) => _links.Add(link);

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

        OldestFirst.Insert(sameName, node, other => (other.Created, other.Id));
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
}
