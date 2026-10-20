using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

/// <summary>Why a link cannot be changed to the way chosen.</summary>
public enum LinkChangeProblem
{
    None,

    /// <summary>The link reads that way already.</summary>
    Unchanged,

    /// <summary>The nodes are linked that way already.</summary>
    Exists,
}

/// <summary>
/// Changes <see cref="Link"/> so that it reads <see cref="Target"/> from the node it is seen from:
/// <see cref="Changes"/> remove it (its copies too) and add the new link. Empty when it cannot (<see cref="Problem"/>).
/// </summary>
public sealed record LinkChangePlan(Link Link, LinkVerb Target, LinkChangeProblem Problem, IReadOnlyList<Change> Changes);

/// <summary>
/// Changes a link's type, or its direction, as a removal and a new link: no change of its own.
/// Changed on two devices before they synced, both new links are shown, as any two links.
/// </summary>
public sealed class LinkChangePlanner(KnowledgeGraph graph, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    /// <param name="seenFrom">The node whose details show the link: <paramref name="target"/> reads from it.</param>
    public LinkChangePlan Plan(Link link, Guid seenFrom, LinkVerb target)
    {
        var other = link.Other(seenFrom);
        var (from, to) = target.IsReverse ? (other, seenFrom) : (seenFrom, other);
        var problem = graph.Types.VerbOf(link, seenFrom) is { } current && current.Type.Id == target.Type.Id && current.IsReverse == target.IsReverse
            ? LinkChangeProblem.Unchanged
            : graph.FindLink(target.Type.Id, from, to) is not null ? LinkChangeProblem.Exists
            : LinkChangeProblem.None;
        if (problem != LinkChangeProblem.None)
        {
            return new LinkChangePlan(link, target, problem, []);
        }

        var now = _clock.GetUtcNow();
        Change[] changes =
        [
            new ChangesRemoved(Guid.NewGuid(), now, graph.CopiesOf(link)),
            new LinkAdded(Guid.NewGuid(), now, new Link(Guid.NewGuid(), target.Type.Id, from, to, now)),
        ];
        return new LinkChangePlan(link, target, problem, changes);
    }
}
