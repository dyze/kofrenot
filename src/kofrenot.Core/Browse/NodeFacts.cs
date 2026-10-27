using Kofrenot.Core.Datasets;
using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Browse;

/// <summary>
/// What the details of a view show of a node, all from the vault's metadata (offline): its type
/// (a dataset's: its file's kind, "PNG file"), a dataset's size, and when it last changed.
/// </summary>
/// <param name="Size">The latest file's size in bytes, for a dataset with a file; null otherwise.</param>
/// <param name="Modified">When it last changed (<see cref="ModifiedOf"/>).</param>
public sealed record NodeFacts(string Type, long? Size, DateTimeOffset Modified)
{
    public static NodeFacts Of(KnowledgeGraph graph, Node node)
    {
        var file = graph.DatasetVersionsOf(node.Id) is [.., var latest] ? latest.File : null;
        return new NodeFacts(file is null ? graph.Types.NodeTypeName(node.Type) : DatasetType.Of(file).Name, file?.Size, ModifiedOf(graph, node));
    }

    /// <summary>The latest of a node's creation, its names, its types, its colours, its field values, its content versions and its links added.</summary>
    public static DateTimeOffset ModifiedOf(KnowledgeGraph graph, Node node) =>
        graph.NamesOf(node.Id).Select(name => name.At)
            .Concat(graph.TypeChangesOf(node.Id).Select(change => change.At))
            .Concat(graph.FieldSetsOf(node.Id).Select(value => value.At))
            .Concat(graph.ColoringsOf(node.Id).Select(color => color.At))
            .Concat(graph.NoteVersionsOf(node.Id).Select(version => version.At))
            .Concat(graph.DatasetVersionsOf(node.Id).Select(version => version.At))
            .Concat(graph.ViewVersionsOf(node.Id).Select(version => version.At))
            .Concat(graph.LinksOf(node.Id).Select(link => link.Created))
            .Append(node.Created)
            .Max();
}
