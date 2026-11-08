using Kofrenot.Core.Graph;
using Kofrenot.Core.Views;

namespace Kofrenot.Core.Imports;

/// <summary>What a node of the other vault writes here: its changes, and the content to write first (read from the other vault).</summary>
internal sealed record NodeWrite(IReadOnlyList<Change> Changes, IReadOnlyList<ContentCopy> Contents)
{
    public static NodeWrite None { get; } = new([], []);
}

/// <summary>
/// A node of the other vault, planned: its <see cref="Outcome"/>, the node here it found (<see cref="Found"/>),
/// why it is not imported (<see cref="Reason"/>), and what the preview lists for it (<see cref="Details"/>:
/// what a new node gets, or what differs on the node found).
/// </summary>
public sealed record VaultImportItem(Node Source, RecordOutcome Outcome, Node? Found, string? Reason, IReadOnlyList<string> Details)
{
    /// <summary>Whether the user may choose for it: a node is added or updated.</summary>
    public bool IsChosen => Outcome is RecordOutcome.New or RecordOutcome.Updated or RecordOutcome.ToCheck;

    /// <summary>The node types it needs (its own, and its mapping's): a node type copied is written only when a node written needs it.</summary>
    internal IReadOnlyList<string> Types { get; init; } = [];

    /// <summary>The link types its mapping's definition names: copied when it is written.</summary>
    internal IReadOnlyList<string> LinkTypes { get; init; } = [];

    /// <summary>What adding it writes: a node with its id in the other vault.</summary>
    internal NodeWrite AsNew { get; init; } = NodeWrite.None;

    /// <summary>What updating <see cref="Found"/> writes: only what differs, its links aside.</summary>
    internal NodeWrite AsUpdate { get; init; } = NodeWrite.None;

    /// <summary>The node here it writes with this choice, or null when it writes none.</summary>
    internal Guid? WrittenAs(RecordChoice choice) =>
        !IsChosen || choice == RecordChoice.Skip ? null
        : choice == RecordChoice.AddAsNew || Outcome == RecordOutcome.New ? Source.Id
        : Found!.Id;

    /// <summary>Whether this choice adds it (rather than update the node found).</summary>
    internal bool AddsFor(RecordChoice choice) => IsChosen && (choice == RecordChoice.AddAsNew || (choice == RecordChoice.Import && Outcome == RecordOutcome.New));

    /// <summary>The node here its links lead to with this choice: the one it writes, the one found when unchanged, else none.</summary>
    internal Guid? LinkedAs(RecordChoice choice) => Outcome == RecordOutcome.Unchanged ? Found!.Id : WrittenAs(choice);
}

/// <summary>A link of the other vault touching a node imported, with its qualifiers' values.</summary>
internal sealed record SourceLink(Link Link, IReadOnlyList<FieldSet> Qualifiers);

/// <summary>What importing writes as chosen: the content to write first (read from the other vault), then the changes.</summary>
public sealed record VaultImportWrite(IReadOnlyList<Change> Changes, IReadOnlyList<ContentCopy> Contents);

/// <summary>
/// What importing nodes of another vault does, node by node (<see cref="Items"/>), before anything is
/// written: the node and link types it adds here, the links it leaves out (their other node is not here,
/// or their type cannot be added) and what else it cannot import (<see cref="Problems"/>).
/// </summary>
public sealed record VaultImportPlan(
    IReadOnlyList<VaultImportItem> Items,
    IReadOnlyList<string> NodeTypesAdded,
    IReadOnlyList<string> LinkTypesAdded,
    int LinksLeftOut,
    IReadOnlyList<string> Problems)
{
    internal KnowledgeGraph Target { get; init; } = new();

    internal DateTimeOffset At { get; init; }

    internal IReadOnlyList<NodeTypeDefined> NodeTypeChanges { get; init; } = [];

    /// <summary>The changes adding each link type, by its id.</summary>
    internal IReadOnlyDictionary<string, IReadOnlyList<Change>> LinkTypeChanges { get; init; } = new Dictionary<string, IReadOnlyList<Change>>();

    internal IReadOnlyList<SourceLink> Links { get; init; } = [];

    /// <summary>The lines the preview shows under its groups: the types added, the links and values left out.</summary>
    public IReadOnlyList<string> Summary =>
    [
        .. NodeTypesAdded.Count == 0 ? [] : new[] { $"Also adds node type{(NodeTypesAdded.Count == 1 ? string.Empty : "s")} {string.Join(", ", NodeTypesAdded)}" },
        .. LinkTypesAdded.Count == 0 ? [] : new[] { $"Also adds link type{(LinkTypesAdded.Count == 1 ? string.Empty : "s")} {string.Join(", ", LinkTypesAdded.Select(name => $"“{name}”"))}" },
        .. LinksLeftOut switch
        {
            0 => [],
            1 => new[] { "1 link left out: its other node is not here" },
            _ => new[] { $"{LinksLeftOut} links left out: their other node is not here" },
        },
        .. Problems,
    ];

    public IReadOnlyList<VaultImportItem> Of(RecordOutcome outcome) => Items.Where(item => item.Outcome == outcome).ToList();

    /// <summary>How many nodes are written with these choices: those added or updated.</summary>
    public int CountFor(Func<VaultImportItem, RecordChoice> choose) => Items.Count(item => item.WrittenAs(choose(item)) is not null);

    /// <summary>How many of them are added.</summary>
    public int AddedFor(Func<VaultImportItem, RecordChoice> choose) =>
        Items.Count(item => item.AddsFor(choose(item)));

    /// <summary>
    /// What writing the nodes as chosen writes: the node types and link types they need, each node
    /// added or updated, then the links of the other vault between the nodes written, or to a node
    /// here, that are not here yet, with their qualifiers.
    /// </summary>
    public VaultImportWrite WriteFor(Func<VaultImportItem, RecordChoice> choose)
    {
        var choices = Items.ToDictionary(item => item.Source.Id, item => (Item: item, Choice: choose(item)));
        var written = Items.Where(item => item.WrittenAs(choices[item.Source.Id].Choice) is not null).ToList();
        var nodes = written.SelectMany(item => item.AddsFor(choices[item.Source.Id].Choice) ? item.AsNew.Changes : item.AsUpdate.Changes).ToList();
        var contents = written.SelectMany(item => item.AddsFor(choices[item.Source.Id].Choice) ? item.AsNew.Contents : item.AsUpdate.Contents).ToList();
        var links = new LinkWriter(Target, At, Resolve, LinkTypeChanges.Keys.ToHashSet());
        foreach (var link in Links.Where(link => IsWritten(link.Link.From) || IsWritten(link.Link.To)))
        {
            links.Add(link);
        }

        var types = written.SelectMany(item => item.Types).ToHashSet();
        IReadOnlyList<Change> changes =
        [
            .. NodeTypeChanges.Where(change => types.Contains(change.NodeType)),
            .. LinkTypeChanges.Where(pair => links.Types.Contains(pair.Key) || written.Any(item => item.LinkTypes.Contains(pair.Key))).SelectMany(pair => pair.Value),
            .. nodes,
            .. links.Changes,
        ];
        return new VaultImportWrite(changes, [.. contents, .. links.Contents]);

        bool IsWritten(Guid node) => choices.TryGetValue(node, out var chosen) && chosen.Item.WrittenAs(chosen.Choice) is not null;

        // A node of the other vault, here: the node imported as it, else the same node already here.
        Guid? Resolve(Guid node) =>
            choices.TryGetValue(node, out var chosen) && chosen.Item.LinkedAs(chosen.Choice) is { } here ? here
            : Target.FindNode(node) is not null ? node
            : null;
    }
}

/// <summary>Adds the links of the other vault whose nodes are both here (or written), each once, with their qualifiers.</summary>
internal sealed class LinkWriter(KnowledgeGraph target, DateTimeOffset at, Func<Guid, Guid?> resolve, IReadOnlySet<string> typesAdded)
{
    private readonly HashSet<(string Type, Guid From, Guid To)> _added = [];

    public List<Change> Changes { get; } = [];

    public List<ContentCopy> Contents { get; } = [];

    public HashSet<string> Types { get; } = [];

    public void Add(SourceLink source)
    {
        var link = source.Link;
        if (resolve(link.From) is not { } from || resolve(link.To) is not { } to
            || (target.Types.FindLinkType(link.Type) is null && !typesAdded.Contains(link.Type))
            || target.FindLink(link.Type, from, to) is not null
            || !_added.Add((link.Type, from, to)))
        {
            return;
        }

        var added = new Link(Guid.NewGuid(), link.Type, from, to, at);
        Changes.Add(new LinkAdded(Guid.NewGuid(), at, added));
        Types.Add(link.Type);
        foreach (var value in source.Qualifiers)
        {
            var copied = value with { Id = Guid.NewGuid(), At = at, Holder = FieldHolder.OfLink(added.Id), Replaces = [], Content = null };
            if (value.Image is { } image)
            {
                Contents.Add(new ContentCopy(image, copied.Image!));
            }

            Changes.Add(copied);
        }
    }
}
