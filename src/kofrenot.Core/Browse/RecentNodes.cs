using Kofrenot.Core.Logging;

namespace Kofrenot.Core.Browse;

/// <summary>
/// The nodes last opened on this device, per vault, the latest first: what the empty input box
/// lists after the pinned ones. Kept on this device only (<see cref="NodeListsFile"/>).
/// </summary>
public sealed class RecentNodes(string path, ILog? log = null)
{
    public const int Max = 10;

    private readonly NodeListsFile _file = new(path, "Recent", "recent nodes", log);

    /// <summary>The nodes opened in this vault, the latest first.</summary>
    public IReadOnlyList<Guid> Of(Guid vault) => _file.Of(vault);

    /// <summary>Puts a node first, keeping at most <see cref="Max"/>, and saves.</summary>
    public void Opened(Guid vault, Guid node) => _file.Change(vault, nodes =>
    {
        nodes.Remove(node);
        nodes.Insert(0, node);
        if (nodes.Count > Max)
        {
            nodes.RemoveRange(Max, nodes.Count - Max);
        }
    });
}
