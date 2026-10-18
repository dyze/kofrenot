namespace Kofrenot.Core.Graph;

/// <summary>
/// One change of the vault, stored as one file named by <see cref="Id"/>. <see cref="At"/>
/// orders the changes when they are applied.
/// </summary>
public abstract record Change(Guid Id, DateTimeOffset At);

public sealed record NodeAdded(Guid Id, DateTimeOffset At, Node Node) : Change(Id, At);

public sealed record LinkAdded(Guid Id, DateTimeOffset At, Link Link) : Change(Id, At);

/// <summary>
/// Gives <see cref="Node"/> a new name. <see cref="Replaces"/> are the names it was given over: the
/// renames, or the node's id for its name as added. Two renames that replace the same name,
/// neither knowing the other (two devices before syncing), make a name conflict.
/// </summary>
public sealed record NodeRenamed(Guid Id, DateTimeOffset At, Guid Node, string Name, IReadOnlyList<Guid> Replaces) : Change(Id, At);

/// <summary>
/// Adds <see cref="Phrase"/> as an alias of a link type (<see cref="LinkType"/> is its id), read
/// one way: forward, or reverse when <see cref="IsReverse"/>. Removed by a <see cref="ChangesRemoved"/>.
/// </summary>
public sealed record AliasAdded(Guid Id, DateTimeOffset At, string LinkType, bool IsReverse, string Phrase) : Change(Id, At);

/// <summary>
/// Removes earlier changes that can be removed (aliases, pins), by id. Files are never deleted:
/// a removal is a change of its own, and an alias or pin added again later has a new id.
/// </summary>
public sealed record ChangesRemoved(Guid Id, DateTimeOffset At, IReadOnlyList<Guid> Removes) : Change(Id, At);

/// <summary>Pins <see cref="Node"/>: it is listed while the input box is empty. Unpinned by a <see cref="ChangesRemoved"/>.</summary>
public sealed record NodePinned(Guid Id, DateTimeOffset At, Guid Node) : Change(Id, At);

/// <summary>
/// A new version of the content of <see cref="Node"/> (a note's text, a dataset's file), kept in a
/// file of its own named by <see cref="Change.Id"/> and read only when needed. <see cref="Replaces"/>
/// are the versions it was written over (none for the first). Two versions replacing the same one,
/// neither knowing the other (two devices before syncing), make a conflict.
/// </summary>
public abstract record ContentEdited(Guid Id, DateTimeOffset At, Guid Node, IReadOnlyList<Guid> Replaces) : Change(Id, At);

/// <summary>A new version of a note's Markdown text.</summary>
public sealed record NoteEdited(Guid Id, DateTimeOffset At, Guid Node, IReadOnlyList<Guid> Replaces)
    : ContentEdited(Id, At, Node, Replaces);

/// <summary>A new version of a dataset's file; <see cref="File"/> describes it, so it is known without reading it.</summary>
public sealed record DatasetEdited(Guid Id, DateTimeOffset At, Guid Node, DatasetFile File, IReadOnlyList<Guid> Replaces)
    : ContentEdited(Id, At, Node, Replaces);
