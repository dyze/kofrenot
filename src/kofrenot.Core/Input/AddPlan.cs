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
}

/// <summary>
/// Adds a link, read as typed (<see cref="Verb"/>), and the nodes it needs that do not exist yet.
/// <see cref="Existing"/> is the same link, when it is already there: then nothing is added.
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
