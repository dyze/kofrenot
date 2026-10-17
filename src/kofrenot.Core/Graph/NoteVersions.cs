namespace Kofrenot.Core.Graph;

/// <summary>
/// The content versions of every note. A note's current versions are those no other version
/// replaces: one normally, several after edits made apart (a conflict). The result does not
/// depend on the order the changes arrive in.
/// </summary>
internal sealed class NoteVersions
{
    private readonly Dictionary<Guid, Dictionary<Guid, NoteEdited>> _byNote = [];

    /// <summary>False when this version is already there.</summary>
    public bool Add(NoteEdited version)
    {
        if (!_byNote.TryGetValue(version.Node, out var versions))
        {
            _byNote[version.Node] = versions = [];
        }

        return versions.TryAdd(version.Id, version);
    }

    /// <summary>The current versions of a note, oldest first; empty before its first save.</summary>
    public IReadOnlyList<NoteEdited> Current(Guid node)
    {
        if (!_byNote.TryGetValue(node, out var versions))
        {
            return [];
        }

        var replaced = versions.Values.SelectMany(version => version.Replaces).ToHashSet();
        return versions.Values
            .Where(version => !replaced.Contains(version.Id))
            .OrderBy(version => version.At)
            .ThenBy(version => version.Id)
            .ToList();
    }
}
