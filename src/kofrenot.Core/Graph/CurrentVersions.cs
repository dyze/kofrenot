namespace Kofrenot.Core.Graph;

/// <summary>A version of something that replaces earlier ones (a rename, a content version, a vault name).</summary>
internal interface IReplacing
{
    Guid Id { get; }

    DateTimeOffset At { get; }

    IReadOnlyList<Guid> Replaces { get; }
}

/// <summary>
/// The current versions are those no other version replaces: one normally, several after changes
/// made apart (a conflict). The result does not depend on the order the versions arrive in.
/// </summary>
internal static class CurrentVersions
{
    /// <summary>The current versions, oldest first.</summary>
    public static IReadOnlyList<T> Of<T>(IEnumerable<T> versions)
        where T : IReplacing
    {
        var all = versions.ToList();
        var replaced = all.SelectMany(version => version.Replaces).ToHashSet();
        return all
            .Where(version => !replaced.Contains(version.Id))
            .OrderBy(version => version.At)
            .ThenBy(version => version.Id)
            .ToList();
    }
}
