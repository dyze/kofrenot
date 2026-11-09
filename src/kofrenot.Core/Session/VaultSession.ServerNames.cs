using Kofrenot.Core.Logging;
using Kofrenot.Core.Server;
using Kofrenot.Core.Storage;

namespace Kofrenot.Core.Session;

/// <summary>
/// The names of the server vaults, kept in the account's data (<see cref="AccountVaultNames"/>):
/// written when a vault is opened or named, read to show the vaults not added yet by name.
/// </summary>
public sealed partial class VaultSession
{
    private const string NamesHint = "Your vaults' names are sealed for your server account: type its pass phrase to show them. Cancel shows them by id.";

    /// <summary>
    /// The names of <paramref name="stores"/> the account keeps, read with its identity: remembered
    /// on this device, else unlocked by its pass phrase (asked only when there is a name to read).
    /// </summary>
    private async Task<IReadOnlyList<StoreChoice>> ChoicesOf(ISessionQuestions ask, IReadOnlyList<ServerStore> stores)
    {
        await _server.RefreshAsync();
        if (_server.Identity is not { } kept || !stores.Any(store => kept.VaultNames.ContainsKey(store.Id)))
        {
            return [.. stores.Select(store => new StoreChoice(store, null))];
        }

        var unlocked = UnlockedAccountIdentity(kept.Identity) ?? UnlockAccount(ask, NamesHint)?.Unlocked;
        var account = _server.Identity ?? kept;
        return [.. stores.Select(store => new StoreChoice(store, unlocked is null ? null : account.NameOf(store.Id, unlocked)))];
    }

    /// <summary>The account's identity unlocked on this device: by the open vault, or remembered for a server vault; null when none is.</summary>
    private UnlockedIdentity? UnlockedAccountIdentity(Identity account) =>
        Vaults.Where(vault => vault.Location.IsOnServer && vault.Id is not null)
            .Select(vault => _keys.RecallIdentity(vault.Id!.Value))
            .Prepend(_access?.Identity)
            .FirstOrDefault(unlocked => unlocked?.Id == account.Id);

    /// <summary>
    /// Keeps the open server vault's name in its account when the account does not have it, so
    /// another device adding the vault shows it by name. A failure is only logged: it is tried
    /// again at its next opening.
    /// </summary>
    private async Task KeepNameInAccountAsync(string name)
    {
        if (Selected?.Location.Store is not { } store || _server.TooOld is not null || _server.Identity is not { } account || KeepsName(account, store.Id, name))
        {
            return;
        }

        await NameInAccountAsync(account, store.Id, name);
    }

    /// <summary>Whether the account keeps <paramref name="name"/> for <paramref name="store"/>; without its identity unlocked, whether it keeps a name.</summary>
    private bool KeepsName(AccountIdentity account, Guid store, string name) =>
        _access?.Identity is { } unlocked && unlocked.Id == account.Identity.Id
            ? account.NameOf(store, unlocked) == name
            : account.VaultNames.ContainsKey(store);

    /// <summary>Keeps <paramref name="name"/> for <paramref name="store"/> in the account, or takes it out (null); a failure is only logged.</summary>
    private async Task NameInAccountAsync(AccountIdentity account, Guid store, string? name)
    {
        if (_server.Client is not { Session: not null } client)
        {
            return;
        }

        try
        {
            await _server.Identities.NameVaultAsync(client, account, store, name);
        }
        catch (Exception ex) when (ex is ServerException or InvalidDataException)
        {
            _log.Warn("Server", $"The name of store {store} cannot be {(name is null ? "taken out of" : "kept in")} the account: {ex.Message}");
        }
    }
}
