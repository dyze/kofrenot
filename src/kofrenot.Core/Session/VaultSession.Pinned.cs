using Kofrenot.Core.Logging;

namespace Kofrenot.Core.Session;

/// <summary>
/// Pinned for offline: the content of the pinned nodes of a server vault is downloaded ahead
/// (<see cref="Storage.Vault.KeepPinnedAsync"/>) when the vault opens, after each sync, and after
/// each change written here (a pin, a new version).
/// </summary>
public sealed partial class VaultSession
{
    private bool _keepingPinned;
    private bool _keepPinnedAgain;

    /// <summary>Downloads the pinned nodes' content not here yet; once more after the run under way when it runs already.</summary>
    public async Task KeepPinnedAsync()
    {
        if (Current is not { FetchesContent: true } vault)
        {
            return;
        }

        if (_keepingPinned)
        {
            _keepPinnedAgain = true;
            return;
        }

        _keepingPinned = true;
        var downloaded = 0;
        try
        {
            do
            {
                _keepPinnedAgain = false;
                downloaded += await vault.KeepPinnedAsync();
            }
            while (_keepPinnedAgain && Current == vault);
        }
        finally
        {
            _keepingPinned = false;
        }

        if (downloaded > 0)
        {
            _log.Debug(Category, $"{downloaded} files of pinned nodes kept offline");
            if (Current == vault)
            {
                Changed?.Invoke();
            }
        }
    }
}
