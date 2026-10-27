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
    private readonly NodeTypeSets _types = new();
    private readonly FieldValues _fields = new();
    private readonly IReadOnlyList<NodeType> _builtInNodeTypes;
    private (IReadOnlyList<NodeType> NodeTypes, TypeCatalog Links, TypeCatalog Catalog)? _catalog;
    private readonly Dictionary<string, List<Node>> _nodesByName = [];
    private readonly LinkIndex _links;
    private readonly HashSet<Guid> _removed = [];
    private readonly HashSet<Guid> _removals = [];
    private readonly RemovableChanges<NodePinned> _pins;
    private readonly NodeVersions<NoteEdited> _notes = new();
    private readonly NodeVersions<DatasetEdited> _datasets = new();
    private readonly NodeVersions<ViewEdited> _views = new();
    private readonly NodeVersions<NodeColored> _colors = new();
    private readonly Dictionary<Guid, VaultNamed> _vaultNames = [];
    private readonly Dictionary<Guid, Change> _memberChanges = [];

    public KnowledgeGraph(TypeCatalog? types = null)
    {
        _builtInNodeTypes = (types ?? TypeCatalog.BuiltIn).NodeTypes;
        NodeTypes = new CustomNodeTypes(_builtInNodeTypes, _removed);
        LinkTypes = new CustomLinkTypes(types ?? TypeCatalog.BuiltIn, _removed);
        Aliases = new LinkAliases(() => LinkTypes.Catalog, _removed);
        _links = new LinkIndex(() => LinkTypes.Catalog, _removed);
        _pins = new RemovableChanges<NodePinned>(_removed);
    }

    /// <summary>The node types and the link types, the user's included, with the user's aliases: what the input box reads.</summary>
    public TypeCatalog Types
    {
        get
        {
            var (nodeTypes, links) = (NodeTypes.Types, Aliases.Catalog);
            if (_catalog is not { } built || !ReferenceEquals(built.NodeTypes, nodeTypes) || !ReferenceEquals(built.Links, links))
            {
                _catalog = built = (nodeTypes, links, ReferenceEquals(nodeTypes, _builtInNodeTypes) ? links : new TypeCatalog(nodeTypes, links.LinkTypes));
            }

            return built.Catalog;
        }
    }

    /// <summary>The user's node types.</summary>
    public CustomNodeTypes NodeTypes { get; }

    /// <summary>The user's link types.</summary>
    public CustomLinkTypes LinkTypes { get; }

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
        NodeTypeAdded added => _types.Add(added) && TypesChanged(added.Node),
        NodeTypeRemoved removed => _types.Add(removed) && TypesChanged(removed.Node),
        NodeTypeDefined definition => NodeTypes.Add(definition) && TypeChanged(definition.NodeType),
        FieldSet value => _fields.Add(value),
        AliasAdded alias => Aliases.Add(alias),
        LinkTypeDefined definition => Define(definition),
        ChangesRemoved removal => Remove(removal),
        NodePinned pin => _pins.Add(pin),
        NoteEdited version => _notes.Add(version),
        DatasetEdited version => _datasets.Add(version),
        ViewEdited version => _views.Add(version),
        NodeColored color => _colors.Add(color),
        VaultNamed name => _vaultNames.TryAdd(name.Id, name),
        MemberAdded or MemberRemoved => _memberChanges.TryAdd(change.Id, change),
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

    /// <summary>
    /// The records of a shared vault's members, added and removed, as written: not checked. Its
    /// members are the ones its owner signed (<c>VaultSharing.Members</c>).
    /// </summary>
    public IReadOnlyCollection<Change> MemberChanges => _memberChanges.Values;

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
        => _types.Add(retype) && TypesChanged(retype.Node);

    // A type of the user's arrived or was removed: the nodes that have it are shown again with their types.
    private bool TypeChanged(string type)
    {
        foreach (var node in _nodes.Values.Where(node => HasTypeGiven(node.Id, type)).ToList())
        {
            TypesChanged(node.Id);
        }

        return true;
    }

    // Whether a change gave this type to the node, removed or not: a type arriving may be shown again.
    private bool HasTypeGiven(Guid node, string type) =>
        _names[node].Added!.Type == type
        || _types.Of(node).Any(change => (change is NodeTypeAdded added && added.Type == type) || (change is NodeRetyped retyped && retyped.Type == type));

    // A node's types changed: it is shown again with its type (TypeIdsOf).
    private bool TypesChanged(Guid id)
    {
        if (_nodes.TryGetValue(id, out var node))
        {
            Hide(node);
            Show(Shown(_names[id].Added!));
        }

        return true;
    }

    /// <summary>
    /// The types of a node, each with the change that gave it, oldest first: one type may be given
    /// twice (on two devices before they synced). Empty for an unknown node, or one with no type left.
    /// </summary>
    public IReadOnlyList<TypeVersion> TypesOf(Guid node) =>
        _names.TryGetValue(node, out var names) && names.Added is { } added ? _types.Current(added, NodeTypes.IsRemoved) : [];

    /// <summary>The ids of the types of a node, each once, oldest first.</summary>
    public IReadOnlyList<string> TypeIdsOf(Guid node) => TypesOf(node).Select(type => type.Type).Distinct().ToList();

    /// <summary>Whether a node has this type; <see cref="TypeCatalog.Generic"/> when it has none left.</summary>
    public bool HasType(Guid node, string type) =>
        TypesOf(node) is { Count: > 0 } types ? types.Any(version => version.Type == type) : type == TypeCatalog.Generic.Id && FindNode(node) is not null;

    /// <summary>The changes of a node's types (retypes, types added and removed), whatever they replace: when it last changed.</summary>
    public IEnumerable<Change> TypeChangesOf(Guid node) => _types.Of(node);

    /// <summary>The fields of a node that were set, each with its current values, in the order they were first set.</summary>
    public IReadOnlyList<FieldVersions> FieldsOf(Guid node) => _fields.Current(node);

    /// <summary>The values set on a node, whatever they replace: when it last changed.</summary>
    public IEnumerable<FieldSet> FieldSetsOf(Guid node) => _fields.Of(node);

    /// <summary>
    /// The current colours of a node, oldest first: none before it is given one, one, or several
    /// when it was given different colours on two devices before they synced (a conflict).
    /// </summary>
    public IReadOnlyList<ColorVersion> ColorsOf(Guid node) =>
        _colors.Current(node).Select(color => new ColorVersion(color.Id, NodeColorNames.Parse(color.Color), color.At)).ToList();

    /// <summary>The colour shown for a node: the latest current one; none before it is given one.</summary>
    public NodeColor ColorOf(Guid node) => ColorsOf(node) is [.., var latest] ? latest.Color : NodeColor.None;

    /// <summary>The colour changes of a node, whatever they replace: when it last changed.</summary>
    public IEnumerable<NodeColored> ColoringsOf(Guid node) => _colors.Of(node);

    /// <summary><paramref name="added"/> as shown: its current name and type.</summary>
    private Node Shown(Node added)
    {
        var (name, type) = (_names[added.Id].Shown(added), _types.Shown(added, NodeTypes.IsRemoved, HasContent));
        return name == added.Name && type == added.Type ? added : added with { Name = name, Type = type };
    }

    private bool HasContent(string type) => _builtInNodeTypes.FirstOrDefault(builtIn => builtIn.Id == type)?.HasContent ?? false;

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

    /// <summary>Takes back nodes, links, aliases, link types or pins. False when this removal is already there.</summary>
    public bool Remove(ChangesRemoved removal)
    {
        if (!_removals.Add(removal.Id))
        {
            return false;
        }

        var symmetry = SymmetryOf(LinkTypes.NamedBy(removal.Removes));
        var nodeTypes = NodeTypes.DefinedBy(removal.Removes);

        _removed.UnionWith(removal.Removes);
        foreach (var id in removal.Removes)
        {
            if (_nodes.Remove(id, out var node))
            {
                Hide(node);
            }
        }

        _links.Remove(removal.Removes);
        NodeTypes.Changed();
        foreach (var type in nodeTypes)
        {
            TypeChanged(type);
        }

        LinkTypes.Changed();
        LinkTypesChanged(symmetry);
        return true;
    }

    /// <summary>Adds or renames a link type of the user's (from a change or a snapshot). False when it is already there.</summary>
    private bool Define(LinkTypeDefined definition)
    {
        var symmetry = SymmetryOf([definition.LinkType]);
        if (!LinkTypes.Add(definition))
        {
            return false;
        }

        LinkTypesChanged(symmetry);
        return true;
    }

    // Whether each link type reads the same both ways; a type not known reads one way.
    private Dictionary<string, bool> SymmetryOf(IEnumerable<string> linkTypes) =>
        linkTypes.ToDictionary(type => type, type => LinkTypes.Catalog.FindLinkType(type)?.IsSymmetric ?? false);

    // The links of a type whose symmetry changed (it arrived, or was removed) are filed again.
    private void LinkTypesChanged(Dictionary<string, bool> symmetry)
    {
        Aliases.Changed();
        var changed = symmetry.Where(type => (LinkTypes.Catalog.FindLinkType(type.Key)?.IsSymmetric ?? false) != type.Value).Select(type => type.Key).ToHashSet();
        if (changed.Count > 0)
        {
            _links.Refile(changed);
        }
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
