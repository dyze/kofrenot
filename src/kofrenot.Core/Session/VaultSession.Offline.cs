using Kofrenot.Core.Logging;

namespace Kofrenot.Core.Session;

/// <summary>
/// Kept offline: the nodes this device keeps offline (<see cref="Browse.OfflineNodes"/>, never in the
/// vault) have their content downloaded ahead (<see cref="Storage.Vault.KeepOfflineAsync"/>) when the
/// vault opens, after each sync, and when a node is kept offline. Only a server vault downloads
/// content: a folder vault has all of it.
/// </summary>
public sealed partial class VaultSession
{
    private bool _keepingOffline;
    private bool _keepOfflineAgain;

    /// <summary>The open vault downloads content when needed: its nodes can be kept offline.</summary>
    public bool CanKeepOffline => Current?.FetchesContent == true;

    public bool IsKeptOffline(Guid node) => Current is { } vault && _offline.Has(vault.Id, node);

    /// <summary>Keeps a node of the open server vault offline on this device, or no longer. Nothing is written to the vault.</summary>
    public void KeepOffline(Guid node, bool kept)
    {
        if (!CanKeepOffline || Current is not { } vault)
        {
            return;
        }

        _offline.Set(vault.Id, node, kept);
        _log.Info("Action", kept ? "Node kept offline" : "Node no longer kept offline");
        Changed?.Invoke();
        if (kept)
        {
            _ = KeepOfflineAsync();
        }
    }

    /// <summary>Downloads the content kept offline not here yet; once more after the run under way when it runs already.</summary>
    public async Task KeepOfflineAsync()
    {
        if (Current is not { FetchesContent: true } vault)
        {
            return;
        }

        if (_keepingOffline)
        {
            _keepOfflineAgain = true;
            return;
        }

        _keepingOffline = true;
        var downloaded = 0;
        try
        {
            do
            {
                _keepOfflineAgain = false;
                downloaded += await vault.KeepOfflineAsync(_offline.Of(vault.Id));
            }
            while (_keepOfflineAgain && Current == vault);
        }
        finally
        {
            _keepingOffline = false;
        }

        if (downloaded > 0)
        {
            _log.Debug(Category, $"{downloaded} files of nodes kept offline downloaded");
            if (Current == vault)
            {
                Changed?.Invoke();
            }
        }
    }
}
