using Kofrenot.Core.Logging;

namespace Kofrenot.Core.Browse;

/// <summary>
/// The views open as tabs on this device, per vault, in their order: they open again with the
/// vault. Kept on this device only (<see cref="NodeListsFile{T}"/>), never in the vault.
/// </summary>
public sealed class ViewTabs(string path, ILog? log = null)
{
    private readonly NodeListsFile<Guid> _file = new(path, "Tabs", "open tabs", log);

    /// <summary>The views open in this vault, in their order. Some may be unknown (removed, or not synced yet).</summary>
    public IReadOnlyList<Guid> Of(Guid vault) => _file.Of(vault);

    /// <summary>Adds a view as the last tab, unless it is open already.</summary>
    public void Open(Guid vault, Guid view)
    {
        if (!Of(vault).Contains(view))
        {
            _file.Change(vault, views => views.Add(view));
        }
    }

    /// <summary>
    /// Moves an open view just before <paramref name="before"/>, or last when it is null (or not open):
    /// a tab dragged among the others. Nothing changes for a view not open, or put where it is.
    /// </summary>
    public void Move(Guid vault, Guid view, Guid? before)
    {
        var views = Of(vault).ToList();
        if (!views.Remove(view) || before == view)
        {
            return;
        }

        var index = before is { } target && views.IndexOf(target) is >= 0 and var at ? at : views.Count;
        views.Insert(index, view);
        if (!views.SequenceEqual(Of(vault)))
        {
            _file.Change(vault, kept =>
            {
                kept.Clear();
                kept.AddRange(views);
            });
        }
    }

    public void Close(Guid vault, Guid view)
    {
        if (Of(vault).Contains(view))
        {
            _file.Change(vault, views => views.Remove(view));
        }
    }
}
