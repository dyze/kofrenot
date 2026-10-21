using System.Windows;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Server;
using Kofrenot.Core.Settings;
using Kofrenot.Core.Storage;
using Kofrenot.UI.Sync;
using Kofrenot.UI.Views;

namespace Kofrenot.UI;

/// <summary>
/// The server vaults: created on the server, added from it, moved to it, deleted from it. Each
/// is a store mirrored in a local folder, opened as a folder vault; <see cref="SyncService"/>
/// keeps both alike.
/// </summary>
public sealed partial class VaultSession
{
    private const string NotSignedIn = "Sign in to a server first, in Settings > Server.";

    // The server vault being created: its empty mirror becomes a new vault, not one waiting for its download.
    private VaultLocation? _newOnServer;

    public ServerConnection Server => _server;

    public SyncService Sync => _sync;

    /// <summary>The stores of the account not registered here yet; null when signed out or the server failed (the user is told).</summary>
    public async Task<IReadOnlyList<ServerStore>?> StoresToAdd(Window owner)
    {
        if (_server.Client is not { Session: not null } client)
        {
            MessageDialog.Inform(owner, NotSignedIn);
            return null;
        }

        try
        {
            var registered = Vaults.Select(vault => vault.Location.Store?.Id).ToHashSet();
            return (await client.ListStoresAsync()).Where(store => !registered.Contains(store.Id)).ToList();
        }
        catch (ServerException ex)
        {
            Inform(owner, "The vaults on the server cannot be listed", ex);
            return null;
        }
    }

    /// <summary>Creates a vault on the server, then switches to it, asking its pass phrase. Not added when that is cancelled.</summary>
    public async Task CreateOnServer(Window owner)
    {
        if (_server.Client is not { Session: not null, Server: { } server } client)
        {
            MessageDialog.Inform(owner, NotSignedIn);
            return;
        }

        Guid store;
        try
        {
            store = await client.CreateStoreAsync();
        }
        catch (ServerException ex)
        {
            Inform(owner, "The vault cannot be created on the server", ex);
            return;
        }

        // A creation cancelled leaves an empty store: listed in Add from server, where it becomes a new vault.
        AddServerVault(owner, new StoreAddress(server, store), isNew: true);
    }

    /// <summary>Adds a vault of the server: downloads it, then switches to it. An empty store becomes a new vault.</summary>
    public async Task AddFromServer(Window owner, ServerStore store)
    {
        if (_server.Client is not { Server: { } server })
        {
            MessageDialog.Inform(owner, NotSignedIn);
            return;
        }

        var address = new StoreAddress(server, store.Id);
        if (store.Files > 0 && await _sync.SyncAsync(address) is var status && VaultFolder.Inspect(_files.MirrorOf(store.Id)) != FolderKind.Vault)
        {
            MessageDialog.Inform(owner, $"The vault cannot be downloaded: {status.Describe()}.");
            return;
        }

        AddServerVault(owner, address, isNew: store.Files == 0);
    }

    /// <summary>
    /// Moves a folder vault to the server: its files copied as they are into a new store's mirror
    /// and uploaded, the vault then listed as a server vault (same id, hotkey and name). Its folder
    /// is left as it was. The open vault is closed first.
    /// </summary>
    public async Task MoveToServer(Window owner, RegisteredVault vault)
    {
        if (_server.Client is not { Session: not null, Server: { } server } client)
        {
            MessageDialog.Inform(owner, NotSignedIn);
            return;
        }

        var wasSelected = vault.Location == _settings.SelectedVault;
        if (wasSelected)
        {
            Close();
        }

        Guid? created = null;
        try
        {
            var store = (created = await client.CreateStoreAsync()).Value;
            var mirror = _files.MirrorOf(store);
            var copied = await Task.Run(() => VaultMove.CopyToMirror(vault.Location.Folder, mirror));
            var moved = new VaultLocation(mirror, new StoreAddress(server, store));
            _settings.SetVaults(_settings.Vaults.With(vault.Location, kept => kept with { Location = moved }));
            if (wasSelected)
            {
                _settings.SelectVault(moved);
            }

            _log.Info("Action", $"Vault {vault.Id} moved to the server ({copied} files)");
            await _sync.SyncNowAsync();
            MessageDialog.Inform(owner, $"The vault is on the server: {_sync.StatusOf(moved.Store!).Describe()}.\n\nIts folder {vault.Location.Folder} is left as it was: delete it once you no longer need it.");
        }
        catch (ServerException ex)
        {
            Inform(owner, "The vault cannot be moved to the server", ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Error("Action", "Cannot move the vault to the server", ex);
            MessageDialog.Inform(owner, $"The vault cannot be moved to the server.\n\n{ex.Message}");
            DeleteMirror(created);
        }
        finally
        {
            if (wasSelected)
            {
                Open(owner);
            }

            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Moves a server vault to the server's trash (the account's <paramref name="password"/> asked
    /// again), then takes it out of the list and deletes its mirror. False when the server refused.
    /// </summary>
    public async Task<bool> DeleteFromServer(Window owner, RegisteredVault vault, string password)
    {
        if (_server.Client is not { Session: not null } client || vault.Location.Store is not { } store)
        {
            MessageDialog.Inform(owner, NotSignedIn);
            return false;
        }

        try
        {
            await client.DeleteStoreAsync(store.Id, password);
        }
        catch (ServerException ex)
        {
            Inform(owner, ex.Code == "wrong-password" ? "Wrong password" : "The vault cannot be deleted from the server", ex);
            return false;
        }

        Unregister(owner, vault.Location);
        DeleteMirror(store.Id);
        return true;
    }

    /// <summary>Deletes a mirror no longer used, and its sync state; a failure is only logged (it is a cache).</summary>
    private void DeleteMirror(Guid? store)
    {
        if (store is not { } id || !Directory.Exists(_files.MirrorOf(id)))
        {
            return;
        }

        var mirror = _files.MirrorOf(id);

        try
        {
            Directory.Delete(mirror, recursive: true);
            File.Delete(_files.SyncStateOf(id));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Warn("Action", $"A server vault's mirror cannot be deleted ({ex.GetType().Name})");
        }
    }

    private void AddServerVault(Window owner, StoreAddress store, bool isNew)
    {
        var location = new VaultLocation(_files.MirrorOf(store.Id), store);
        _settings.SetVaults(_settings.Vaults.Add(new RegisteredVault(location)));
        _log.Info("Settings", $"Server vault added: store {store.Id}");
        _newOnServer = isNew ? location : null;
        try
        {
            SwitchTo(owner, location);
        }
        finally
        {
            _newOnServer = null;
        }

        if (VaultFolder.Inspect(location.Folder) != FolderKind.Vault)
        {
            _settings.SetVaults(_settings.Vaults.Remove(location));
            _log.Info("Settings", "Server vault not added: its creation was cancelled");
            Changed?.Invoke();
            return;
        }

        _sync.Soon();
    }

    private void Inform(Window owner, string what, ServerException ex)
    {
        _log.Warn("Action", $"{what}: {ex.Message}");
        MessageDialog.Inform(owner, $"{what}.\n\n{Explain(ex)}");
    }

    private static string Explain(ServerException ex) => ex.Failure switch
    {
        ServerFailure.Offline => "The server cannot be reached. Check the connection, then try again.",
        ServerFailure.SignInNeeded => "The session ended: sign in again in Settings > Server.",
        ServerFailure.Refused when ex.Code == "wrong-password" => "The password of the server account is not this one.",
        ServerFailure.Refused or ServerFailure.NotFound => "The server refused: the vault is no longer there, or no longer yours.",
        ServerFailure.TooManyRequests => "Too many attempts: try again in a minute.",
        _ => $"The server failed ({ex.Message}).",
    };
}
