using Kofrenot.Core.Graph;
using Kofrenot.Core.Views;

namespace Kofrenot.Core.History;

/// <summary>
/// Restores <see cref="Entry"/>: <see cref="Changes"/> write it again as a new version, after the
/// content of <see cref="Contents"/> (a note's text, a dataset's file) is written again under its id.
/// </summary>
public sealed record RestorePlan(HistoryEntry Entry, IReadOnlyList<Change> Changes, IReadOnlyList<ContentCopy> Contents);

/// <summary>
/// Turns a version of a node's history into the change that restores it: a new version holding
/// it, replacing every current one of its kind, so restoring a version of a conflict resolves it.
/// Nothing is overwritten: the versions it replaces stay in the history.
/// </summary>
public sealed class RestorePlanner(KnowledgeGraph graph, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    /// <summary>The plan, or null when restoring <paramref name="entry"/> would change nothing.</summary>
    public RestorePlan? Plan(Node node, HistoryEntry entry)
    {
        if (!entry.CanRestore)
        {
            return null;
        }

        var (id, at) = (Guid.NewGuid(), _clock.GetUtcNow());
        Change change = entry.Version switch
        {
            NameVersion name => new NodeRenamed(id, at, node.Id, name.Name, graph.NamesOf(node.Id).Select(current => current.Id).ToList()),
            NodeColored color => new NodeColored(id, at, node.Id, color.Color, graph.ColorsOf(node.Id).Select(current => current.Id).ToList()),
            FieldSet value => Restored(value, node, graph.FieldsOf(node.Id), id, at),
            NoteEdited => new NoteEdited(id, at, node.Id, graph.NoteVersionsOf(node.Id).Select(current => current.Id).ToList()),
            DatasetEdited dataset => new DatasetEdited(id, at, node.Id, dataset.File, graph.DatasetVersionsOf(node.Id).Select(current => current.Id).ToList()),
            ViewEdited view => new ViewEdited(id, at, node.Id, view.Definition, graph.ViewVersionsOf(node.Id).Select(current => current.Id).ToList()),
            MappingEdited mapping => new MappingEdited(id, at, node.Id, mapping.Definition, graph.MappingVersionsOf(node.Id).Select(current => current.Id).ToList()),
            _ => throw new ArgumentOutOfRangeException(nameof(entry), entry.Kind, "Unknown version"),
        };
        IReadOnlyList<ContentCopy> contents = entry.Version is NoteEdited or DatasetEdited
            ? [new ContentCopy((ContentEdited)entry.Version, (ContentEdited)change)]
            : [];
        return new RestorePlan(entry, [change], contents);
    }

    /// <summary>The plan restoring a value of a link's qualifier, or null when it would change nothing.</summary>
    public RestorePlan? Plan(Link link, HistoryEntry entry) =>
        entry is { CanRestore: true, Version: FieldSet value }
            ? new RestorePlan(entry, [Restored(value, link, graph.QualifiersOf(link), Guid.NewGuid(), _clock.GetUtcNow())], [])
            : null;

    // A value again, on its holder (a link's shown one, when it was set on a copy), over the field's current ones.
    private static FieldSet Restored(FieldSet value, FieldHolder holder, IReadOnlyList<FieldVersions> fields, Guid id, DateTimeOffset at) =>
        value with
        {
            Id = id,
            At = at,
            Holder = holder,
            Replaces = fields.Where(field => field.Owner == value.Owner && field.Field == value.Field).SelectMany(field => field.Current).Select(current => current.Id).ToList(),
            Content = value.File is null ? null : value.Content ?? value.Id,
        };
}
