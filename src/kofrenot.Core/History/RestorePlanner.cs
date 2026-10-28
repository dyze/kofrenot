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
            FieldSet value => value with { Id = id, At = at, Replaces = CurrentValuesOf(node, value), Content = value.File is null ? null : value.Content ?? value.Id },
            NoteEdited => new NoteEdited(id, at, node.Id, graph.NoteVersionsOf(node.Id).Select(current => current.Id).ToList()),
            DatasetEdited dataset => new DatasetEdited(id, at, node.Id, dataset.File, graph.DatasetVersionsOf(node.Id).Select(current => current.Id).ToList()),
            ViewEdited view => new ViewEdited(id, at, node.Id, view.Definition, graph.ViewVersionsOf(node.Id).Select(current => current.Id).ToList()),
            _ => throw new ArgumentOutOfRangeException(nameof(entry), entry.Kind, "Unknown version"),
        };
        IReadOnlyList<ContentCopy> contents = entry.Version is NoteEdited or DatasetEdited
            ? [new ContentCopy((ContentEdited)entry.Version, (ContentEdited)change)]
            : [];
        return new RestorePlan(entry, [change], contents);
    }

    private List<Guid> CurrentValuesOf(Node node, FieldSet value) =>
        graph.FieldsOf(node.Id).Where(field => field.Owner == value.Owner && field.Field == value.Field).SelectMany(field => field.Current).Select(current => current.Id).ToList();
}
