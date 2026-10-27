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
    View,
}

/// <summary>
/// How a node is pictured in the rows (search, links) and a dataset's details: its icon, and for
/// an image dataset the version whose thumbnail replaces it once read (<see cref="Thumbnail"/>;
/// the icon stays while it is missing), with the node's colour (<see cref="Color"/>). A node of a
/// type of the user's has that type's icon (<see cref="TypeIcon"/>, one of <see cref="TypeIcons"/>),
/// and its colour unless the node has one.
/// </summary>
public sealed record NodeVisual(NodeIcon Icon, Guid? Thumbnail = null, NodeColor Color = NodeColor.None, string? TypeIcon = null)
{
    public static NodeVisual Of(KnowledgeGraph graph, Node node)
    {
        var versions = node.Type == TypeCatalog.Dataset.Id ? graph.DatasetVersionsOf(node.Id) : [];
        var color = graph.ColorOf(node.Id);
        if (versions.Count == 0)
        {
            var type = graph.NodeTypes.Find(node.Type)?.Type;
            return OfType(node.Type) with
            {
                Color = color == NodeColor.None && type is not null ? type.Color : color,
                TypeIcon = TypeIcons.Find(type?.Icon),
            };
        }

        var file = versions[^1].File;
        return new NodeVisual(IconOf(DatasetType.Of(file).Category), DatasetType.HasThumbnail(file) ? versions[^1].Id : null, color);
    }

    /// <summary>The icon of a node type alone (a node without file, or a type on the creation screen).</summary>
    public static NodeVisual OfType(string typeId) => new(typeId switch
    {
        _ when typeId == TypeCatalog.Note.Id => NodeIcon.Note,
        _ when typeId == TypeCatalog.Dataset.Id => NodeIcon.File,
        _ when typeId == TypeCatalog.View.Id => NodeIcon.View,
        _ => NodeIcon.Generic,
    });

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
