using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;

namespace Kofrenot.Core.Browse;

/// <summary>A node deleted from its details, with its links and pins: <see cref="Question"/> asks first, naming what goes.</summary>
public sealed record NodeDeletion(Node Node, int Links, ChangesRemoved Change)
{
    public string Question =>
        $"Delete “{Node.Name}”{(Links == 0 ? string.Empty : Links == 1 ? " and its link" : $" and its {Links} links")}?\n\nIt is deleted on every device; its history goes with it.";
}

/// <summary>A link removed, with its copies: <see cref="Question"/> names it, and the nodes it leaves with no link.</summary>
public sealed record LinkRemoval(Link Link, string Question, ChangesRemoved Change);

/// <summary>
/// Deletes one node from its details (not a subtree, as a tree does: <see cref="Views.DeletePlanner"/>),
/// or one link. A removal is final: what is added again later is new.
/// </summary>
public sealed class DeletionPlanner(KnowledgeGraph graph, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public NodeDeletion Node(Node node)
    {
        var links = graph.LinksOf(node.Id);
        IReadOnlyList<Guid> removed = [node.Id, .. links.SelectMany(graph.CopiesOf), .. graph.PinsOf(node.Id)];
        return new NodeDeletion(node, links.Count, new ChangesRemoved(Guid.NewGuid(), _clock.GetUtcNow(), removed));
    }

    /// <summary>Removes a link, with its copies (the same link added on two devices), as read from <paramref name="seenFrom"/>.</summary>
    public LinkRemoval Link(Link link, Guid seenFrom)
    {
        var text = new PlanText(graph);
        var from = graph.FindNode(seenFrom)!;
        var other = graph.FindNode(link.Other(seenFrom))!;
        var unlinked = new[] { from, other }
            .Where(node => graph.LinksOf(node.Id).Count == 1)
            .Select(node => $"\n“{text.NodeLabel(node)}” then has no link left.");
        var question = $"Remove the link “{text.NodeLabel(from)} {graph.Types.LinkName(link, seenFrom)} {text.NodeLabel(other)}”?\n\nThe nodes stay.{string.Concat(unlinked)}";
        return new LinkRemoval(link, question, new ChangesRemoved(Guid.NewGuid(), _clock.GetUtcNow(), graph.CopiesOf(link)));
    }
}
