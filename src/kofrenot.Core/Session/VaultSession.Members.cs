using Kofrenot.Core.Graph;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Server;
using Kofrenot.Core.Settings;
using Kofrenot.Core.Sharing;
using Kofrenot.Core.Storage;

namespace Kofrenot.Core.Session;

/// <summary>
/// Members leaving a shared server vault: removed by its owner, or leaving on their own. Its key
/// is then rotated by the owner's kofrenot (at once, or at its next opening after a member left),
/// and the server's fence raised once the new keys are uploaded. Files written meanwhile under the
/// old key are sealed again; a newer key written by another device is taken up.
/// </summary>
public sealed partial class VaultSession
{
    /// <summary>This account owns the open shared vault: it removes members.</summary>
    public bool CanRemoveMembers => CanShare && Members.Count > 0;

    /// <summary>The open vault is shared with this account by someone else: it can leave it.</summary>
    public bool CanLeave => !ReadsOnly && Current is { } vault && MyRoleIn(vault) is MemberRole.Reader or MemberRole.Writer && _server.IsSignedIn;

    /// <summary>Removes a member from the open vault: their server access at once, then a new key they do not have.</summary>
    public async Task RemoveMember(ISessionQuestions ask, VaultMember member)
    {
        if (!CanRemoveMembers || member.Role == MemberRole.Owner || Selected?.Location.Store is not { } store || _server.Client is not { } client)
        {
            return;
        }

        if (!ask.Confirm($"Remove {member.Email} from this vault?\n\nThey can no longer reach it on the server, and what is written from now on is sealed with a new key they do not have. What they have read so far stays readable to them.", "Remove"))
        {
            _log.Info("Action", "Member removal cancelled");
            return;
        }

        try
        {
            await client.RemoveMemberAsync(store.Id, member.Account);
            await RotateAway(ask, [member]);
        }
        catch (ServerException ex)
        {
            Inform(ask, "The member cannot be removed", ex);
        }
    }

    /// <summary>Makes a member a reader if a writer, a writer if a reader: their record in the vault, then their role on the server.</summary>
    public async Task ChangeRole(ISessionQuestions ask, VaultMember member)
    {
        if (!CanRemoveMembers || member.Role == MemberRole.Owner || Current is not { } vault || _access is not { } access
            || Selected?.Location.Store is not { } store || _server.Client is not { } client)
        {
            return;
        }

        var role = member.Role == MemberRole.Writer ? MemberRole.Reader : MemberRole.Writer;
        var question = role == MemberRole.Reader
            ? $"Make {member.Email} a reader?\n\nThey can still read the vault, no longer change it."
            : $"Make {member.Email} a writer?\n\nThey can then change the vault too.";
        if (!ask.Confirm(question, role == MemberRole.Reader ? "Make reader" : "Make writer"))
        {
            _log.Info("Action", "Role change cancelled");
            return;
        }

        try
        {
            if (VaultSharing.ChangeRole(access, vault.Graph, member.Account, role, DateTimeOffset.UtcNow) is { } record
                && !Append(ask, vault, [record], $"Member role changed to {role}"))
            {
                return;
            }

            await client.SetMemberAsync(store.Id, member.Account, VaultSharing.ToStoreRole(role));
            _sync.Soon();
        }
        catch (ServerException ex)
        {
            Inform(ask, "The role cannot be changed", ex);
        }
        catch (InvalidOperationException ex)
        {
            _log.Error("Action", "Cannot change the role", ex);
            ask.Tell($"The role cannot be changed.\n\n{ex.Message}");
        }
    }

    /// <summary>Leaves the open vault, shared by someone else: it goes out of the list; its copy here stays until removed.</summary>
    public async Task Leave(ISessionQuestions ask)
    {
        if (!CanLeave || Selected is not { Location.Store: { } store } vault || _server.Client is not { } client)
        {
            return;
        }

        if (!ask.Confirm("Leave this vault?\n\nIt goes out of your list, and you can no longer reach it on the server. Its owner can share it with you again.", "Leave"))
        {
            _log.Info("Action", "Leaving cancelled");
            return;
        }

        try
        {
            await client.LeaveAsync(store.Id);
            _log.Info("Action", $"Vault {vault.Id} left");
            Unregister(ask, vault.Location);
        }
        catch (ServerException ex)
        {
            Inform(ask, "The vault cannot be left", ex);
        }
    }

    /// <summary>
    /// For the owner of the open shared vault: rotates its key for the members gone from the server
    /// (they left, or were removed elsewhere), then raises the fence once the new keys are uploaded.
    /// Quiet: runs after opening, and logs what fails, to try again at the next opening.
    /// </summary>
    private async Task SettleMembers(ISessionQuestions ask)
    {
        if (!CanRemoveMembers || Selected?.Location.Store is not { } store || _server.Client is not { } client)
        {
            return;
        }

        try
        {
            var gone = VaultSharing.Gone(Members, await client.MembersAsync(store.Id));
            if (gone.Count > 0)
            {
                await RotateAway(ask, gone);
            }
            else
            {
                await RaiseFence(client, store);
            }
        }
        catch (ServerException ex)
        {
            _log.Warn("Sync", $"The members of the vault cannot be checked: {ex.Message}");
        }
    }

    // A new key for the members left, the records of the gone removed, uploaded, then the fence raised.
    private async Task RotateAway(ISessionQuestions ask, IReadOnlyList<VaultMember> gone)
    {
        if (Current is not { } vault || Folder is not { } folder || _access is not { } access || _server.Identity is not { } account
            || Selected?.Location.Store is not { } store || _server.Client is not { } client)
        {
            return;
        }

        try
        {
            var (rotated, removals) = VaultSharing.Remove(folder, access, vault.Graph, [.. gone.Select(member => member.Account)], new AccountKey(account.Identity.Id, account.Identity.PublicKey), DateTimeOffset.UtcNow);
            vault.UseKeys(rotated.Keys);
            _access = rotated;
            Append(ask, vault, removals, $"Vault key rotated to epoch {rotated.Keys.CurrentEpoch}: {gone.Count} members out");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _log.Error("Action", "Cannot rotate the vault's key", ex);
            ask.Tell($"The vault's key cannot be changed.\n\n{ex.Message}");
            return;
        }

        await RaiseFence(client, store);
    }

    // The fence follows the latest epoch, once its keys are on the server: devices behind then seal again.
    private async Task RaiseFence(KofreClient client, StoreAddress store)
    {
        if (Folder is not { } folder || await _sync.SyncAsync(store) is not { Failure: null, Waiting: 0 })
        {
            return;
        }

        var latest = OwnerKey is { } ownerKey && _access is { } access ? VaultKeys.LatestEpoch(folder, access.Header.Id, ownerKey) : VaultKeys.LatestEpoch(folder);
        var stored = (await client.ListStoresAsync()).FirstOrDefault(listed => listed.Id == store.Id);
        if (stored is { Role: StoreRole.Owner } && (stored.Fence ?? -1) < latest)
        {
            await client.SetFenceAsync(store.Id, latest);
        }
    }

    /// <summary>
    /// After a sync: the changes downloaded read (a client without a folder watcher needs it), the
    /// open vault's files refused below the fence sealed again, and the keys of a newer epoch
    /// (written by another device) taken up.
    /// </summary>
    private void AfterSync()
    {
        if (Selected?.Location.IsOnServer == true)
        {
            Refresh();
        }

        // Content not downloaded (offline, signed out) is asked for again by what shows it.
        if (Current is { } open && Selected?.Location.Store is { } synced && _sync.StatusOf(synced).Failure is null)
        {
            if (open.RetryFailed())
            {
                Changed?.Invoke();
            }

            // Pinned for offline: what the sync brought (a pin, a new version) is downloaded ahead.
            _ = KeepPinnedAsync();
        }

        if (Current is not { } vault || Folder is not { } folder || _access?.Identity is not { } identity || Selected?.Location.Store is not { } store)
        {
            return;
        }

        try
        {
            if (!IsCurrent(folder, _access) && VaultKeys.Unlock(folder, _access.Header, identity, _log, SignerOf(folder, vault.Id)) is { } newer)
            {
                vault.UseKeys(newer.Keys);
                _access = newer;
                _log.Info(Category, $"Vault {vault.Id}: key of epoch {newer.Keys.CurrentEpoch} taken up");
            }

            if (ServerSync.Fenced(_files.SyncStateOf(store.Id)) is { Count: > 0 } fenced && vault.Reseal(fenced) > 0)
            {
                _sync.Soon();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            _log.Warn(Category, $"Files of the vault cannot be sealed again: {ex.Message}");
        }
    }
}
