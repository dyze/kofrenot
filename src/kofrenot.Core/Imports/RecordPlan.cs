using Kofrenot.Core.Fields;
using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Imports;

/// <summary>What importing a record does, as the preview groups it.</summary>
public enum RecordOutcome
{
    /// <summary>Same name as a node, another match value (an ISBN): the user chooses.</summary>
    ToCheck,

    New,

    Updated,

    NotImported,

    Unchanged,
}

/// <summary>What the user chose for a record of the preview.</summary>
public enum RecordChoice
{
    /// <summary>Added when new, updated when found: the default.</summary>
    Import,

    /// <summary>Added as a new node even though one was found (another edition).</summary>
    AddAsNew,

    Skip,
}

/// <summary>A node a link rule links to: found by its name among the nodes of its type, else added by the import (<see cref="Node"/> null).</summary>
public sealed record LinkTarget(string NodeType, string Name, Guid? Node);

/// <summary>A field's value to write: parsed already, over the field's current values (none for a new node).</summary>
internal sealed record ValueToSet(FieldDefinition Field, string Value, IReadOnlyList<Guid> Replaces);

/// <summary>A link to add, by a rule, to a node found or added.</summary>
internal sealed record LinkToAdd(LinkRule Rule, LinkTarget Target);

/// <summary>
/// A record of the file, planned: its <see cref="Outcome"/>, the node it found (<see cref="Node"/>),
/// why it is not imported (<see cref="Reason"/>), what the preview lists for it (<see cref="Details"/>:
/// its values for a new node, what differs for one found) and the values it cannot import (<see cref="ValueProblems"/>).
/// </summary>
public sealed record RecordItem(ImportRecord Record, RecordOutcome Outcome, string? Name, Node? Node, string? Reason, IReadOnlyList<string> Details)
{
    public IReadOnlyList<string> ValueProblems { get; init; } = [];

    /// <summary>Whether the user may choose for it: a node is added or updated.</summary>
    public bool IsChosen => Outcome is RecordOutcome.New or RecordOutcome.Updated or RecordOutcome.ToCheck;

    internal string NodeType { get; init; } = string.Empty;

    /// <summary>All its values and links: what a new node gets.</summary>
    internal IReadOnlyList<ValueToSet> Values { get; init; } = [];

    internal IReadOnlyList<LinkToAdd> Links { get; init; } = [];

    /// <summary>For a node found: the names a rename replaces, or null when its name stays.</summary>
    internal IReadOnlyList<Guid>? RenameOver { get; init; }

    /// <summary>For a node found: the values that differ, and the links it lacks.</summary>
    internal IReadOnlyList<ValueToSet> Updates { get; init; } = [];

    internal IReadOnlyList<LinkToAdd> MissingLinks { get; init; } = [];
}

/// <summary>
/// What importing a file through a mapping does, record by record (<see cref="Items"/>, in the file's
/// order), before anything is written. <see cref="Refusal"/> says why nothing can be imported (the
/// mapping's type is gone); <see cref="Warnings"/> name the rules left out (their field or type is gone).
/// </summary>
public sealed record RecordPlan(IReadOnlyList<RecordItem> Items, IReadOnlyList<string> Warnings, string? Refusal = null)
{
    internal static RecordPlan Refused(string refusal) => new([], [], refusal);

    /// <summary>The records of an outcome.</summary>
    public IReadOnlyList<RecordItem> Of(RecordOutcome outcome) => Items.Where(item => item.Outcome == outcome).ToList();

    /// <summary>How many nodes are written with these choices: those added or updated.</summary>
    public int CountFor(Func<RecordItem, RecordChoice> choose) => Items.Count(item => item.IsChosen && choose(item) != RecordChoice.Skip);

    /// <summary>How many of them are added: the new ones, and those added as new.</summary>
    public int AddedFor(Func<RecordItem, RecordChoice> choose) =>
        Items.Count(item => item.IsChosen && choose(item) is var choice && (choice == RecordChoice.AddAsNew || (choice == RecordChoice.Import && item.Outcome == RecordOutcome.New)));

    /// <summary>
    /// The changes writing the records as chosen, in the file's order: each new node with its values
    /// and links, each node found with what differs. A linked node the import adds is added once,
    /// before its first link, only when a record chosen links to it.
    /// </summary>
    public IReadOnlyList<Change> ChangesFor(Func<RecordItem, RecordChoice> choose, DateTimeOffset at)
    {
        var writer = new ChangeWriter(at);
        foreach (var item in Items.Where(item => item.IsChosen))
        {
            var choice = choose(item);
            if (choice == RecordChoice.Skip)
            {
                continue;
            }

            if (choice == RecordChoice.AddAsNew || item.Outcome == RecordOutcome.New)
            {
                writer.AddNode(item);
            }
            else
            {
                writer.Update(item);
            }
        }

        return writer.Changes;
    }

    private sealed class ChangeWriter(DateTimeOffset at)
    {
        private readonly Dictionary<(string Type, string Key), Guid> _added = [];

        public List<Change> Changes { get; } = [];

        public void AddNode(RecordItem item)
        {
            var node = new Node(Guid.NewGuid(), item.NodeType, item.Name!, at);
            Changes.Add(new NodeAdded(Guid.NewGuid(), at, node));
            Write(node.Id, item.NodeType, item.Values, item.Links);
        }

        public void Update(RecordItem item)
        {
            var node = item.Node!;
            if (item.RenameOver is { } names)
            {
                Changes.Add(new NodeRenamed(Guid.NewGuid(), at, node.Id, item.Name!, names));
            }

            Write(node.Id, item.NodeType, item.Updates, item.MissingLinks);
        }

        private void Write(Guid node, string type, IReadOnlyList<ValueToSet> values, IReadOnlyList<LinkToAdd> links)
        {
            Changes.AddRange(values.Select(value =>
                new FieldSet(Guid.NewGuid(), at, FieldHolder.OfNode(node), type, value.Field.Id, value.Field.Name, value.Field.Kind, value.Value, value.Replaces)));
            foreach (var link in links)
            {
                var target = TargetOf(link.Target);
                var (from, to) = link.Rule.IsReverse ? (target, node) : (node, target);
                Changes.Add(new LinkAdded(Guid.NewGuid(), at, new Link(Guid.NewGuid(), link.Rule.LinkType, from, to, at)));
            }
        }

        private Guid TargetOf(LinkTarget target)
        {
            if (target.Node is { } existing)
            {
                return existing;
            }

            var key = (target.NodeType, NameKey.Of(target.Name));
            if (!_added.TryGetValue(key, out var id))
            {
                var node = new Node(Guid.NewGuid(), target.NodeType, target.Name, at);
                Changes.Add(new NodeAdded(Guid.NewGuid(), at, node));
                _added[key] = id = node.Id;
            }

            return id;
        }
    }
}
