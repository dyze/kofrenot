using System.Globalization;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Fields;
using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Views;

/// <summary>
/// The children of a node in a tree sorted by a qualifier of their links (books by date): by its value, a
/// number as a number, a date or a text as written (case and accents ignored); the children without a value
/// after, by name. A child reached by several links takes its first value.
/// </summary>
internal static class QualifierOrder
{
    public static IReadOnlyList<Node> Of(KnowledgeGraph graph, IEnumerable<Link> links, Guid parent, string field)
    {
        var children = links
            .Select(link => (Node: graph.FindNode(link.Other(parent))!, Value: NodeFields.OfLink(graph, link).FirstOrDefault(shown => shown.Field == field && shown.Value is not null)))
            .GroupBy(child => child.Node.Id)
            .Select(same => same.FirstOrDefault(child => child.Value is not null, same.First()))
            .ToList();
        var valued = children
            .Where(child => child.Value is not null)
            .OrderBy(child => child.Value!.Kind == FieldKind.Number ? decimal.Parse(child.Value.Value!, CultureInfo.InvariantCulture) : 0)
            .ThenBy(child => child.Value!.Kind == FieldKind.Number ? string.Empty : NameKey.Of(child.Value.Value!), StringComparer.Ordinal)
            .ThenBy(child => NameKey.Of(child.Node.Name), StringComparer.Ordinal)
            .Select(child => child.Node);
        return [.. valued, .. NodeOrder.ByName(children.Where(child => child.Value is null).Select(child => child.Node))];
    }
}
