namespace Kofrenot.Core.Graph;

/// <summary>
/// The content versions of every note, or of every dataset. A node's current versions are those
/// no other version replaces: one normally, several after edits made apart (a conflict). The
/// result does not depend on the order the changes arrive in.
/// </summary>
internal sealed class ContentVersions<T>
    where T : ContentEdited
{
    private readonly Dictionary<Guid, Dictionary<Guid, T>> _byNode = [];

    /// <summary>False when this version is already there.</summary>
    public bool Add(T version)
    {
        if (!_byNode.TryGetValue(version.Node, out var versions))
        {
            _byNode[version.Node] = versions = [];
        }

        return versions.TryAdd(version.Id, version);
    }

    /// <summary>The current versions of a node, oldest first; empty before its first save.</summary>
    public IReadOnlyList<T> Current(Guid node)
    {
        if (!_byNode.TryGetValue(node, out var versions))
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
