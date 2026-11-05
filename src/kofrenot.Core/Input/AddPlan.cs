using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

/// <summary>
/// What Enter would add: the preview shows it, and <see cref="Changes"/> are appended to the
/// vault. <see cref="NeedsConfirmation"/>: it repeats a node that is already there.
/// </summary>
public abstract record AddPlan(IReadOnlyList<Change> Changes, bool NeedsConfirmation)
{
    public bool CanAdd => Changes.Count > 0;

    /// <summary>The nodes it adds, in the order typed.</summary>
    public IReadOnlyList<Node> NewNodes => Changes.OfType<NodeAdded>().Select(added => added.Node).ToList();

    /// <summary>Every node it involves, new or existing, in the order typed; none when it adds nothing.</summary>
    public virtual IReadOnlyList<Node> Nodes => NewNodes;
}

/// <summary>Adds the node <see cref="Name"/> of type <see cref="Type"/>; <see cref="Existing"/> are the nodes that already have its name.</summary>
public sealed record NodePlan(string Name, NodeType Type, IReadOnlyList<Node> Existing, IReadOnlyList<Change> Changes)
    : AddPlan(Changes, NeedsConfirmation: Existing.Count > 0);

/// <summary>Why a link cannot be added yet.</summary>
public enum LinkProblem
{
    None,

    /// <summary>The second node is not typed yet.</summary>
    SecondNodeMissing,

    /// <summary>Both sides are the same node.</summary>
    SameNode,

    /// <summary>The link is already there (<see cref="LinkPlan.Existing"/>): a repeated link would count once.</summary>
    AlreadyLinked,

    /// <summary>The link is already there with the qualifiers typed: nothing to update.</summary>
    QualifiersUnchanged,

    /// <summary>A qualifier's value does not fit its kind (<see cref="LinkPlan.QualifierError"/>).</summary>
    QualifierInvalid,
}

/// <summary>A qualifier a link plan writes: added (no <see cref="Old"/>), changed, or taken out (no <see cref="New"/>), as shown.</summary>
public sealed record QualifierChange(string Name, string? Old, string? New)
{
    /// <summary>"Date 1965", "Date: 1965 → 1966", "Date 1965 taken out".</summary>
    public override string ToString() => (Old, New) switch
    {
        (null, _) => $"{Name} {New}",
        (_, null) => $"{Name} {Old} taken out",
        _ => $"{Name}: {Old} → {New}",
    };
}

/// <summary>
/// Adds a link, read as typed (<see cref="Verb"/>), and the nodes it needs that do not exist yet, with the
/// qualifiers typed (<see cref="Qualifiers"/>). <see cref="Existing"/> is the same link, when it is already there:
/// then nothing is added, or its qualifiers are updated (<see cref="IsUpdate"/>). <see cref="Between"/> are the links
/// of its type between both nodes: with qualifiers typed, <see cref="LinkChoice"/> says which one is updated, or
/// (its count) that a separate one is added.
/// </summary>
public sealed record LinkPlan(
    PlannedSide Left,
    LinkVerb Verb,
    PlannedSide? Right,
    LinkProblem Problem,
    Link? Existing,
    IReadOnlyList<Change> Changes)
    : AddPlan(Changes, NeedsConfirmation: false)
{
    public LinkType Type => Verb.Type;

    /// <summary>The links of its type already between both nodes, oldest first.</summary>
    public IReadOnlyList<Link> Between { get; init; } = [];

    /// <summary>Which of <see cref="Between"/> the qualifiers update; its count for a new separate link.</summary>
    public int LinkChoice { get; init; }

    /// <summary>The qualifiers it writes, in the order typed.</summary>
    public IReadOnlyList<QualifierChange> Qualifiers { get; init; } = [];

    /// <summary>Why a qualifier typed does not fit (<see cref="LinkProblem.QualifierInvalid"/>), naming it.</summary>
    public string? QualifierError { get; init; }

    /// <summary>It updates the qualifiers of <see cref="Existing"/>, adding nothing else.</summary>
    public bool IsUpdate => Existing is not null && CanAdd;

    /// <summary>Qualifiers are typed for nodes already linked so: which of <see cref="Between"/> they update, or a new link, is a choice.</summary>
    public bool HasLinkChoice { get; init; }

    /// <summary>Both ends: the chosen node of a side, or the node added for it.</summary>
    public override IReadOnlyList<Node> Nodes
    {
        get
        {
            if (!CanAdd)
            {
                return [];
            }

            var added = new Queue<Node>(NewNodes);
            return [Left.Chosen ?? added.Dequeue(), Right!.Chosen ?? added.Dequeue()];
        }
    }
}

/// <summary>
/// One side of a link: the nodes with its name, and the one chosen (the oldest by default).
/// No node with that name: a new one is added, of type <see cref="Type"/>.
/// </summary>
public sealed record PlannedSide(string Name, NodeType Type, IReadOnlyList<Node> Candidates, int Choice)
{
    public bool IsNew => Candidates.Count == 0;

    public Node? Chosen => IsNew ? null : Candidates[Choice];
}
