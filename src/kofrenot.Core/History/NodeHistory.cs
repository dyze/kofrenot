using Kofrenot.Core.Datasets;
using Kofrenot.Core.Fields;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Views;

namespace Kofrenot.Core.History;

/// <summary>What a version of a node holds.</summary>
public enum VersionKind
{
    /// <summary>A note's text: not read to list it.</summary>
    Text,

    /// <summary>A dataset's file.</summary>
    File,

    /// <summary>A view's definition.</summary>
    View,

    Name,

    Color,

    /// <summary>A field's value, or none (cleared).</summary>
    Field,
}

/// <summary>
/// A version in a node's history, all from the vault's metadata (offline). <see cref="Label"/>
/// says what it holds: a name, a colour, a field's value, a file's name and size, what a view
/// shows; empty for a note's text. <see cref="Field"/> names the field of a value.
/// <see cref="IsCurrent"/>: the node has it now, one of a conflict included.
/// <see cref="CanRestore"/>: restoring it changes something (it is not the only current one).
/// </summary>
public sealed record HistoryEntry(Guid Id, DateTimeOffset At, VersionKind Kind, string? Field, string Label, bool IsCurrent, bool CanRestore)
{
    /// <summary>What it is, as listed: the field's name, or its kind ("Text", "Colour").</summary>
    public string What => Field ?? Kind switch
    {
        VersionKind.Text => "Text",
        VersionKind.File => "File",
        VersionKind.View => "Definition",
        VersionKind.Name => "Name",
        _ => "Colour",
    };

    /// <summary>The version itself: a <see cref="NameVersion"/>, a <see cref="NodeColored"/>, a <see cref="FieldSet"/> or a <see cref="ContentEdited"/>.</summary>
    internal object Version { get; init; } = null!;
}

/// <summary>
/// The versions of a node, newest first: its content versions (a note's texts, a dataset's files, a
/// view's definitions), its names, its colours and its fields' values. Nothing is overwritten in a
/// vault, so each is there to restore (<see cref="RestorePlanner"/>). Its types and links have no
/// versions: they are added and taken out.
/// </summary>
public static class NodeHistory
{
    public static IReadOnlyList<HistoryEntry> Of(KnowledgeGraph graph, Node node)
    {
        var entries = Contents(graph, node)
            .Concat(Names(graph, node))
            .Concat(Colors(graph, node))
            .Concat(Fields(graph, node));
        return entries.OrderByDescending(entry => entry.At).ThenBy(entry => entry.Id).ToList();
    }

    private static IEnumerable<HistoryEntry> Contents(KnowledgeGraph graph, Node node)
    {
        var current = graph.NoteVersionsOf(node.Id).Concat<ContentEdited>(graph.DatasetVersionsOf(node.Id)).Concat(graph.ViewVersionsOf(node.Id)).Select(version => version.Id).ToHashSet();
        return graph.ContentHistoryOf(node.Id).Select(version => Entry(version, version.Id, version.At, KindOf(version), field: null, LabelOf(version, graph), current));
    }

    private static IEnumerable<HistoryEntry> Names(KnowledgeGraph graph, Node node)
    {
        var current = graph.NamesOf(node.Id).Select(name => name.Id).ToHashSet();
        return graph.NameHistoryOf(node.Id).Select(name => Entry(name, name.Id, name.At, VersionKind.Name, field: null, name.Name, current));
    }

    private static IEnumerable<HistoryEntry> Colors(KnowledgeGraph graph, Node node)
    {
        var current = graph.ColorsOf(node.Id).Select(color => color.Id).ToHashSet();
        return graph.ColoringsOf(node.Id).Select(color => Entry(color, color.Id, color.At, VersionKind.Color, field: null, NodeColorNames.Parse(color.Color).ToString(), current));
    }

    // Each field's values, named as its latest value names it.
    private static IEnumerable<HistoryEntry> Fields(KnowledgeGraph graph, Node node) =>
        graph.FieldsOf(node.Id).SelectMany(field =>
        {
            var current = field.Current.Select(value => value.Id).ToHashSet();
            var values = graph.FieldSetsOf(node.Id).Where(value => value.Owner == field.Owner && value.Field == field.Field);
            return values.Select(value => Entry(value, value.Id, value.At, VersionKind.Field, field.Shown.Name, LabelOf(value), current));
        });

    private static HistoryEntry Entry(object version, Guid id, DateTimeOffset at, VersionKind kind, string? field, string label, HashSet<Guid> current)
    {
        var isCurrent = current.Contains(id);
        return new HistoryEntry(id, at, kind, field, label, isCurrent, CanRestore: !isCurrent || current.Count > 1) { Version = version };
    }

    private static VersionKind KindOf(ContentEdited version) => version switch
    {
        NoteEdited => VersionKind.Text,
        DatasetEdited => VersionKind.File,
        _ => VersionKind.View,
    };

    private static string LabelOf(ContentEdited version, KnowledgeGraph graph) => version switch
    {
        DatasetEdited dataset => $"{dataset.File.Name}, {ByteSize.Format(dataset.File.Size)}",
        ViewEdited view => ViewDraft.Describe(view.Definition, graph),
        _ => string.Empty,
    };

    private static string LabelOf(FieldSet value) =>
        value.File is { } file ? file.Name
        : value.Value is { } text ? FieldValue.Display(value.Kind, text)
        : "(empty)";
}
