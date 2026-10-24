using Kofrenot.Core.Datasets;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Views;

namespace Kofrenot.Core.Browse;

/// <summary>What a card shows under its node's name, by the node's type.</summary>
public abstract record CardBody;

/// <summary>A note's text, previewed.</summary>
public sealed record NoteCardBody(NotePreview Preview) : CardBody;

/// <summary>A note whose latest content has not arrived on this device yet.</summary>
public sealed record MissingCardBody : CardBody;

/// <summary>A dataset's latest file: its kind ("PNG file") and size; an image's thumbnail is in the card's <see cref="NodeVisual"/>.</summary>
public sealed record FileCardBody(string Kind, long Size) : CardBody;

/// <summary>A sentence: what a view shows, or that a dataset has no file yet.</summary>
public sealed record TextCardBody(string Text) : CardBody;

/// <summary>
/// A node as a card of a list view: its name, its picture (icon, thumbnail, colour), and a body by
/// its type (null: the name only). Made when the card comes into view: a note's content is read
/// then (<paramref name="readNote"/> in <see cref="Of"/>), and kept only as long as the card.
/// </summary>
public sealed record NodeCard(Node Node, NodeVisual Visual, CardBody? Body)
{
    /// <param name="readNote">Reads a note version's content; null when its file is not there.</param>
    public static NodeCard Of(KnowledgeGraph graph, Node node, Func<Guid, string?> readNote) =>
        new(node, NodeVisual.Of(graph, node), BodyOf(graph, node, readNote));

    private static CardBody? BodyOf(KnowledgeGraph graph, Node node, Func<Guid, string?> readNote) => node.Type switch
    {
        _ when node.Type == TypeCatalog.Note.Id => NoteBodyOf(graph.NoteVersionsOf(node.Id), readNote),
        _ when node.Type == TypeCatalog.Dataset.Id => graph.DatasetVersionsOf(node.Id) is [.., var latest]
            ? new FileCardBody(DatasetType.Of(latest.File).Name, latest.File.Size)
            : new TextCardBody("No file yet"),
        _ when node.Type == TypeCatalog.View.Id => new TextCardBody(ViewDraft.Describe(new ViewPlanner(graph).Load(node).Definition, graph)),
        _ => null,
    };

    private static CardBody NoteBodyOf(IReadOnlyList<NoteEdited> versions, Func<Guid, string?> readNote)
    {
        if (versions.Count == 0)
        {
            return new NoteCardBody(NotePreview.Of(string.Empty));
        }

        return readNote(versions[^1].Id) is { } text ? new NoteCardBody(NotePreview.Of(text)) : new MissingCardBody();
    }
}
