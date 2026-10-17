using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Notes;

/// <summary>A version of a note shown in a conflict: when it was saved, and its content (null while not synced).</summary>
public sealed record ConflictVersion(Guid Id, DateTimeOffset At, string? Text);

/// <summary>
/// A note as loaded in its details. <see cref="Text"/> is the content of its latest current
/// version: empty before the first save, null while that version's file is not synced yet (the
/// note cannot be edited then). <see cref="Conflict"/> lists the current versions when it was
/// edited on two devices before they synced.
/// </summary>
public sealed record NoteState(IReadOnlyList<NoteEdited> Versions, string? Text, IReadOnlyList<ConflictVersion> Conflict)
{
    public bool IsMissing => Text is null;
}

/// <summary>
/// Loads a note's content on demand, and turns an edit into the version that saves it. A version
/// replaces every version loaded, so saving a note in conflict (keeping one version, or a merge
/// typed by hand) resolves it; a version saved meanwhile elsewhere stays, as a new conflict.
/// </summary>
public sealed class NotePlanner(KnowledgeGraph graph, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    /// <param name="read">Reads a version's content; null when its file is not there.</param>
    public NoteState Load(Node node, Func<Guid, string?> read)
    {
        var versions = graph.NoteVersionsOf(node.Id);
        var text = versions.Count == 0 ? string.Empty : read(versions[^1].Id);
        IReadOnlyList<ConflictVersion> conflict = versions.Count > 1
            ? versions.Select(version => new ConflictVersion(version.Id, version.At, read(version.Id))).ToList()
            : [];
        return new NoteState(versions, text, conflict);
    }

    /// <summary>
    /// The version saving <paramref name="text"/>, or null when there is nothing to save: the text
    /// is the one loaded and there is no conflict to resolve.
    /// </summary>
    /// <exception cref="InvalidOperationException">The loaded content is missing: saving would replace a version not seen.</exception>
    public NoteEdited? Save(Node node, string text, NoteState loaded)
    {
        if (loaded.IsMissing)
        {
            throw new InvalidOperationException("The note's content is not synced yet.");
        }

        return text == loaded.Text && loaded.Conflict.Count == 0
            ? null
            : new NoteEdited(Guid.NewGuid(), _clock.GetUtcNow(), node.Id, loaded.Versions.Select(version => version.Id).ToList());
    }
}
