using Kofrenot.Core.Logging;

namespace Kofrenot.Core.Browse;

/// <summary>
/// The nodes last opened on this device, per vault, the latest first, each with when: what the
/// empty input box lists as recent (<see cref="StartNodes"/>). Kept on this device only (<see cref="NodeListsFile{T}"/>).
/// </summary>
public sealed class RecentNodes(string path, ILog? log = null, TimeProvider? clock = null)
{
    public const int Max = 10;

    private readonly NodeListsFile<OpenedNode> _file = new(path, "Recent", "recent nodes", log);
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    /// <summary>The nodes opened in this vault, the latest first.</summary>
    public IReadOnlyList<OpenedNode> Of(Guid vault) => _file.Of(vault);

    /// <summary>Puts a node first, opened now, keeping at most <see cref="Max"/>, and saves.</summary>
    public void Opened(Guid vault, Guid node) => _file.Change(vault, nodes =>
    {
        nodes.RemoveAll(opened => opened.Node == node);
        nodes.Insert(0, new OpenedNode(node, _clock.GetUtcNow()));
        if (nodes.Count > Max)
        {
            nodes.RemoveRange(Max, nodes.Count - Max);
        }
    });
}
