using Kofrenot.Core.Browse;
using Kofrenot.Core.Datasets;
using Kofrenot.Core.Fields;
using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

/// <summary>An image dataset to give an image field: its node, and its latest version, the image.</summary>
public sealed record ImageChoice(Node Node, DatasetEdited Image);

/// <summary>
/// What an image is given to, and what it is taken from: the nodes with an image field (a picture
/// shared from another app), and the image datasets already in the vault (no file copied).
/// </summary>
public static class ImagePicker
{
    public const int Limit = 50;

    /// <summary>Whether a file picked or shared can be an image field's, as <see cref="FieldPlanner.WhyNotImage"/> tells before it is read.</summary>
    public static bool CanBeImage(ImportFile file) =>
        DatasetType.Of(file.Name).Viewer == DatasetViewer.Image && file.Size <= DatasetType.MaxImageSize;

    /// <summary>The image fields of a node, in the order of its fields.</summary>
    public static IReadOnlyList<NodeField> FieldsOf(KnowledgeGraph graph, Guid node) =>
        NodeFields.Of(graph, node).Where(field => field.Kind == FieldKind.Image).ToList();

    /// <summary>
    /// The nodes with an image field whose name or value matches <paramref name="text"/>, as the
    /// search finds them; <paramref name="first"/> (the node shown) before them while it has one.
    /// </summary>
    public static IReadOnlyList<Node> Targets(KnowledgeGraph graph, string text, Node? first = null)
    {
        var found = GraphSearch.Find(graph, text, int.MaxValue).Select(hit => hit.Node);
        IEnumerable<Node> nodes = first is null ? found : [first, .. found.Where(node => node.Id != first.Id)];
        return nodes.Where(node => FieldsOf(graph, node.Id).Count > 0).Take(Limit).ToList();
    }

    /// <summary>The datasets whose latest file is an image an image field takes, by name; those matching <paramref name="text"/> once typed.</summary>
    public static IReadOnlyList<ImageChoice> Images(KnowledgeGraph graph, string text)
    {
        var matches = GraphSearch.MatcherOf(text);
        return graph.Nodes
            .Where(node => node.Type == TypeCatalog.Dataset.Id && (text.Trim().Length == 0 || matches(node.Name)))
            .Select(node => (Node: node, Versions: graph.DatasetVersionsOf(node.Id)))
            .Where(dataset => dataset.Versions.Count > 0 && FieldPlanner.WhyNotImage(dataset.Versions[^1].File) is null)
            .OrderBy(dataset => NameKey.Of(dataset.Node.Name), StringComparer.Ordinal)
            .ThenBy(dataset => dataset.Node.Created)
            .Take(Limit)
            .Select(dataset => new ImageChoice(dataset.Node, dataset.Versions[^1]))
            .ToList();
    }
}
