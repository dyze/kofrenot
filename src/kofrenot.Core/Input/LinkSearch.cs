using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

/// <summary>
/// A link found by a qualifier's value: read from <see cref="From"/> as <see cref="Verb"/> to <see cref="To"/>,
/// with the qualifier that matched (<see cref="Field"/>). <see cref="Found"/> is the node it leads to: the other
/// end of the node typed (his books), or else the one it is read from (the books written by someone).
/// </summary>
public sealed record LinkHit(Link Link, Node From, LinkVerb? Verb, Node To, NodeField Field, Node Found);

/// <summary>
/// Finds links by their qualifiers, offline, with the sentence's shape: <c>written by, Date 1965</c> (links of that
/// type with that value), <c>Frank Herbert wrote, Date 1960..1970</c> (his links in that range), <c>, Source isfdb</c>
/// (any link with that value). The value matches as <see cref="ValueQuery"/> says; none: any link with the qualifier set.
/// </summary>
public static class LinkSearch
{
    /// <summary>The links found, in the order of their nodes' names; null when the text is not such a search.</summary>
    public static IReadOnlyList<LinkHit>? Find(KnowledgeGraph graph, string text)
    {
        var comma = text.IndexOf(',');
        if (comma < 0 || Sentence(graph, text[..comma]) is not { } sentence)
        {
            return null;
        }

        var links = graph.LinksWithQualifiers
            .Where(link => sentence.Verb is not { } verb || link.Type == verb.Type.Id)
            .Select(link => (Link: link, Fields: NodeFields.OfLink(graph, link).Where(field => field.HasValue).ToList()))
            .ToList();
        var names = links.SelectMany(link => link.Fields).Select(field => new Fields.FieldDefinition(field.Field, field.Name, field.Kind)).DistinctBy(field => NameKey.Of(field.Name)).ToList();
        if (QualifierText.NameAt(text[(comma + 1)..], names) is not { } name)
        {
            return null;
        }

        var value = string.Join(' ', InputParser.Words(text[(comma + 1)..])[InputParser.Words(name.Name).Length..]);
        return links
            .SelectMany(link => link.Fields
                .Where(field => NameKey.Of(field.Name) == NameKey.Of(name.Name) && ValueQuery.Matches(field, value))
                .Take(1)
                .Select(field => HitOf(graph, link.Link, sentence, field)))
            .Where(hit => sentence.Nodes is not { } nodes || (nodes.Any(node => node.Id == hit.From.Id) && hit.Verb?.IsReverse == sentence.Verb!.IsReverse))
            .OrderBy(hit => NameKey.Of(hit.From.Name), StringComparer.Ordinal)
            .ThenBy(hit => NameKey.Of(hit.To.Name), StringComparer.Ordinal)
            .ToList();
    }

    // Read as typed: from the node typed, or the way the verb typed reads; else forward.
    private static LinkHit HitOf(KnowledgeGraph graph, Link link, (LinkVerb? Verb, IReadOnlyList<Node>? Nodes) sentence, NodeField field)
    {
        var reverse = sentence.Nodes is { } nodes
            ? nodes.Any(node => node.Id == link.To) && !nodes.Any(node => node.Id == link.From)
            : sentence.Verb?.IsReverse ?? false;
        var (from, to) = reverse ? (graph.FindNode(link.To)!, graph.FindNode(link.From)!) : (graph.FindNode(link.From)!, graph.FindNode(link.To)!);
        return new LinkHit(link, from, graph.Types.VerbOf(link, from.Id), to, field, sentence.Nodes is null ? from : to);
    }

    // What comes before the ",": nothing, a link phrase, or a node and a link phrase; null for anything else (a name with a comma).
    private static (LinkVerb? Verb, IReadOnlyList<Node>? Nodes)? Sentence(KnowledgeGraph graph, string before)
    {
        var key = NameKey.Of(before);
        if (key.Length == 0)
        {
            return (null, null);
        }

        if (graph.Types.Verbs.FirstOrDefault(verb => verb.Phrases.Any(phrase => NameKey.Of(phrase) == key)) is { } alone)
        {
            return (alone, null);
        }

        return InputParser.Parse(before, graph.Types) is LinkRequest { Right.Length: 0 } link && graph.NodesNamed(link.Left) is { Count: > 0 } nodes
            ? (link.Verb, nodes)
            : null;
    }
}
