namespace Kofrenot.Core.Graph;

/// <summary>
/// Changes arrive in any order: keeps a list sorted by creation, then id, so it is in the same
/// order on every device.
/// </summary>
internal static class OldestFirst
{
    public static void Insert<T>(List<T> items, T item, Func<T, (DateTimeOffset Created, Guid Id)> order)
    {
        var position = order(item);
        var index = items.FindIndex(other => order(other).CompareTo(position) > 0);
        items.Insert(index < 0 ? items.Count : index, item);
    }
}
