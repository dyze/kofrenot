namespace Kofrenot.Core.Graph;

/// <summary>
/// Changes that a <see cref="ChangesRemoved"/> can take back (aliases, pins): those added, minus
/// those removed. A removal may arrive before what it removes (the sync tool copies files in any
/// order): it is kept, so the result does not depend on the order.
/// </summary>
internal sealed class RemovableChanges<T>(ISet<Guid> removed)
    where T : Change
{
    private readonly Dictionary<Guid, T> _added = [];

    /// <summary>False when it is already there.</summary>
    public bool Add(T change) => _added.TryAdd(change.Id, change);

    /// <summary>Those not removed, oldest first.</summary>
    public IEnumerable<T> Current => _added.Values
        .Where(change => !removed.Contains(change.Id))
        .OrderBy(change => change.At)
        .ThenBy(change => change.Id);
}
