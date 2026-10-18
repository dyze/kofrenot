using Kofrenot.Core.Datasets;
using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Browse;

/// <summary>The icon of a node: by its type, and for a dataset by its file's category.</summary>
public enum NodeIcon
{
    Generic,
    Note,
    File,
    Image,
    Text,
    Code,
    Audio,
    Video,
    Archive,
    Pdf,
}

/// <summary>
/// How a node is pictured in the rows (search, links) and a dataset's details: its icon, and for
/// an image dataset the version whose thumbnail replaces it once read (<see cref="Thumbnail"/>;
/// the icon stays while it is missing).
/// </summary>
public sealed record NodeVisual(NodeIcon Icon, Guid? Thumbnail = null)
{
    public static NodeVisual Of(KnowledgeGraph graph, Node node)
    {
        if (node.Type == TypeCatalog.Note.Id)
        {
            return new NodeVisual(NodeIcon.Note);
        }

        if (node.Type != TypeCatalog.Dataset.Id)
        {
            return new NodeVisual(NodeIcon.Generic);
        }

        var versions = graph.DatasetVersionsOf(node.Id);
        if (versions.Count == 0)
        {
            return new NodeVisual(NodeIcon.File);
        }

        var file = versions[^1].File;
        return new NodeVisual(IconOf(DatasetType.Of(file).Category), DatasetType.HasThumbnail(file) ? versions[^1].Id : null);
    }

    private static NodeIcon IconOf(DatasetCategory category) => category switch
    {
        DatasetCategory.Image => NodeIcon.Image,
        DatasetCategory.Text => NodeIcon.Text,
        DatasetCategory.Code => NodeIcon.Code,
        DatasetCategory.Audio => NodeIcon.Audio,
        DatasetCategory.Video => NodeIcon.Video,
        DatasetCategory.Archive => NodeIcon.Archive,
        DatasetCategory.Pdf => NodeIcon.Pdf,
        _ => NodeIcon.File,
    };
}
