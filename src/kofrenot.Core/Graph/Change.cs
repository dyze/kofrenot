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
/// Gives <see cref="Node"/> another type (<see cref="Type"/>, a node type id). <see cref="Replaces"/>
/// are the types it was given over: the retypes, or the node's id for its type as added. Two
/// retypes that replace the same type, neither knowing the other, make a type conflict.
/// </summary>
public sealed record NodeRetyped(Guid Id, DateTimeOffset At, Guid Node, string Type, IReadOnlyList<Guid> Replaces) : Change(Id, At), IReplacing;

/// <summary>
/// Names the vault itself, encrypted like any change, so every device shows the same name.
/// <see cref="Replaces"/> are the names it was given over (none for the first). Two names
/// replacing the same one, neither knowing the other (two devices before syncing), make a conflict.
/// </summary>
public sealed record VaultNamed(Guid Id, DateTimeOffset At, string Name, IReadOnlyList<Guid> Replaces) : Change(Id, At), IReplacing;

/// <summary>
/// Adds <see cref="Phrase"/> as an alias of a link type (<see cref="LinkType"/> is its id), read
/// one way: forward, or reverse when <see cref="IsReverse"/>. Removed by a <see cref="ChangesRemoved"/>.
/// </summary>
public sealed record AliasAdded(Guid Id, DateTimeOffset At, string LinkType, bool IsReverse, string Phrase) : Change(Id, At);

/// <summary>
/// Removes earlier changes that can be removed (nodes, links, aliases, pins), by id. Files are never deleted:
/// a removal is a change of its own, and what is added again later has a new id.
/// </summary>
public sealed record ChangesRemoved(Guid Id, DateTimeOffset At, IReadOnlyList<Guid> Removes) : Change(Id, At);

/// <summary>Pins <see cref="Node"/>: it is listed while the input box is empty. Unpinned by a <see cref="ChangesRemoved"/>.</summary>
public sealed record NodePinned(Guid Id, DateTimeOffset At, Guid Node) : Change(Id, At);

/// <summary>
/// A new version of the content of <see cref="Node"/>: a note's text or a dataset's file, kept in a
/// file of its own named by <see cref="Change.Id"/> and read only when needed; a view's definition,
/// small, kept in the change. <see cref="Replaces"/>
/// are the versions it was written over (none for the first). Two versions replacing the same one,
/// neither knowing the other (two devices before syncing), make a conflict.
/// </summary>
public abstract record ContentEdited(Guid Id, DateTimeOffset At, Guid Node, IReadOnlyList<Guid> Replaces) : Change(Id, At), IReplacing;

/// <summary>A new version of a note's Markdown text.</summary>
public sealed record NoteEdited(Guid Id, DateTimeOffset At, Guid Node, IReadOnlyList<Guid> Replaces)
    : ContentEdited(Id, At, Node, Replaces);

/// <summary>A new version of a dataset's file; <see cref="File"/> describes it, so it is known without reading it.</summary>
public sealed record DatasetEdited(Guid Id, DateTimeOffset At, Guid Node, DatasetFile File, IReadOnlyList<Guid> Replaces)
    : ContentEdited(Id, At, Node, Replaces);

/// <summary>A new version of a view's definition, held by the change: a view works offline.</summary>
public sealed record ViewEdited(Guid Id, DateTimeOffset At, Guid Node, ViewDefinition Definition, IReadOnlyList<Guid> Replaces)
    : ContentEdited(Id, At, Node, Replaces);

/// <summary>
/// Records a member of a shared vault, encrypted like any change, so every member's device knows
/// who shares it and their keys. Signed by the vault's owner: a record it did not sign is ignored
/// (<c>VaultSharing.Members</c>). A later one for the same account replaces it (a role changed).
/// </summary>
public sealed record MemberAdded(Guid Id, DateTimeOffset At, VaultMember Member, byte[] Signature) : Change(Id, At);

/// <summary>Takes a member out of a shared vault's records, signed by its owner as <see cref="MemberAdded"/>.</summary>
public sealed record MemberRemoved(Guid Id, DateTimeOffset At, Guid Account, byte[] Signature) : Change(Id, At);
