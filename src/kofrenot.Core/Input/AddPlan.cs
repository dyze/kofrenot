using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

/// <summary>
/// What Enter would add: the preview shows it, and <see cref="Changes"/> are appended to the
/// database. <see cref="NeedsConfirmation"/>: it repeats a node that is already there.
/// </summary>
public abstract record AddPlan(IReadOnlyList<Change> Changes, bool NeedsConfirmation)
{
    public bool CanAdd => Changes.Count > 0;
}

/// <summary>Adds the node <see cref="Name"/>; <see cref="Existing"/> are the nodes that already have it.</summary>
public sealed record NodePlan(string Name, IReadOnlyList<Node> Existing, IReadOnlyList<Change> Changes)
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
/// Adds a link, and the nodes it needs that do not exist yet. <see cref="Existing"/> is the same
/// link, when it is already there: then nothing is added.
/// </summary>
public sealed record LinkPlan(
    PlannedSide Left,
    LinkType Type,
    PlannedSide? Right,
    LinkProblem Problem,
    Link? Existing,
    IReadOnlyList<Change> Changes)
    : AddPlan(Changes, NeedsConfirmation: false);

/// <summary>
/// One side of a link: the nodes with its name, and the one chosen (the oldest by default).
/// No node with that name: a new one is added.
/// </summary>
public sealed record PlannedSide(string Name, IReadOnlyList<Node> Candidates, int Choice)
{
    public bool IsNew => Candidates.Count == 0;

    public Node? Chosen => IsNew ? null : Candidates[Choice];
}
