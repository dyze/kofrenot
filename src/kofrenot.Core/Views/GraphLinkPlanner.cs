using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;

namespace Kofrenot.Core.Views;

/// <summary>
/// A link a graph view can add between two of its cards: <see cref="Verb"/> read from
/// <see cref="From"/>, as <see cref="Label"/> says ("Dune written by Frank Herbert").
/// <see cref="Exists"/> when the two nodes are already linked so: it adds nothing.
/// </summary>
public sealed record GraphLinkChoice(LinkVerb Verb, Guid From, Guid To, string Label, bool Exists);

/// <summary>
/// The links a graph view adds by hand: of its link types, either way, between two of its cards
/// (a card dragged to another), or to a new node (New node from a card).
/// </summary>
public sealed class GraphLinkPlanner(KnowledgeGraph graph, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    /// <summary>Each way the view's link types read, in the view's order; none of a type this version does not know.</summary>
    public IReadOnlyList<LinkVerb> Verbs(GraphDefinition definition) => definition.Links
        .Select(link => link.LinkType)
        .Distinct()
        .Select(graph.Types.FindLinkType)
        .OfType<LinkType>()
        .SelectMany(type => type.Verbs)
        .ToList();

    /// <summary>The links that can join <paramref name="from"/> to <paramref name="to"/>, read from <paramref name="from"/>; none from a node to itself.</summary>
    public IReadOnlyList<GraphLinkChoice> Choices(GraphDefinition definition, Guid from, Guid to)
    {
        if (from == to || graph.FindNode(from) is not { } fromNode || graph.FindNode(to) is not { } toNode)
        {
            return [];
        }

        var text = new PlanText(graph);
        return Verbs(definition)
            .Select(verb => new GraphLinkChoice(verb, from, to, $"{text.NodeLabel(fromNode)} {verb.Name} {text.NodeLabel(toNode)}", IsLinked(verb, from, to)))
            .ToList();
    }

    /// <summary>The change that adds a choice's link; null when it is there already.</summary>
    public LinkAdded? Add(GraphLinkChoice choice)
    {
        if (IsLinked(choice.Verb, choice.From, choice.To))
        {
            return null;
        }

        var now = _clock.GetUtcNow();
        var (from, to) = choice.Verb.IsReverse ? (choice.To, choice.From) : (choice.From, choice.To);
        return new LinkAdded(Guid.NewGuid(), now, new Link(Guid.NewGuid(), choice.Verb.Type.Id, from, to, now));
    }

    // A symmetric link reads the same both ways: either one is the same link.
    private bool IsLinked(LinkVerb verb, Guid from, Guid to) =>
        verb.IsReverse ? graph.FindLink(verb.Type.Id, to, from) is not null
        : graph.FindLink(verb.Type.Id, from, to) is not null || (verb.Type.IsSymmetric && graph.FindLink(verb.Type.Id, to, from) is not null);
}
