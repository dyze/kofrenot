using System.Windows;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Server;
using Kofrenot.Core.Storage;
using Kofrenot.UI.Views;

namespace Kofrenot.UI;

/// <summary>
/// The server account's identity, which opens all its server vaults: unlocked or, the first time,
/// chosen (its recovery key then shown); a server vault handed to it (attached); its pass phrase
/// changed. Needs a server from 0.2 (<see cref="KofreVersion"/>).
/// </summary>
public sealed partial class VaultSession
{
    private const string AttachHint = "This vault opens with its own pass phrase so far. From now on, your server account's pass phrase opens it, and the vault's own only its older files.";

    /// <summary>The account's identity, to look for among those of a server vault; null for a folder vault.</summary>
    private Identity? AccountIdentityOf(string folder) =>
        Selected is { Location: { IsOnServer: true } location } && location.Folder == folder ? _server.Identity?.Identity : null;

    /// <summary>Asks the account's pass phrase, and seals its identity again with a new one.</summary>
    public void ChangeAccountPassPhrase(Window owner)
    {
        if (UnlockAccount(owner, "Type your server account's current pass phrase.") is { } account)
        {
            ChangeAccountPassPhrase(owner, account.Identity, account.Unlocked);
        }
    }

    /// <summary>
    /// The account's identity, unlocked by its pass phrase; chosen, the first time, then its
    /// recovery key shown. Null when cancelled, or not possible now (the user is told).
    /// </summary>
    private (AccountIdentity Identity, UnlockedIdentity Unlocked)? UnlockAccount(Window owner, string hint)
    {
        if (_server.Client is not { Session: not null } client || _server.Account is not { } account)
        {
            MessageDialog.Inform(owner, NotSignedIn);
            return null;
        }

        if (_server.TooOld is { } tooOld)
        {
            MessageDialog.Inform(owner, tooOld);
            return null;
        }

        var kept = _server.Identity;
        var place = $"{account.Email} on {account.Server.Host}";
        (AccountIdentity Identity, UnlockedIdentity Unlocked, RecoveryKey? Recovery)? unlocked = null;
        var done = PassphraseDialog.AskAccount(owner, place, exists: kept is not null, hint, passPhrase =>
        {
            unlocked = kept is null
                ? CreateAccountIdentity(client, account.Server, account.Email, passPhrase)
                : kept.Identity.Unlock(passPhrase) is { } opened ? (Published(client, kept, opened), opened, null) : null;
            return unlocked is not null;
        });
        if (!done || unlocked is not { } result)
        {
            _log.Info("Action", "Server account pass phrase not given");
            return null;
        }

        if (result.Recovery is { } recovery)
        {
            RecoveryKeyDialog.Show(owner, place, recovery);
        }

        return (result.Identity, result.Unlocked);
    }

    /// <summary>Gives the account its identity (runs in the background, while a dialog waits).</summary>
    /// <exception cref="IOException">The server refused, or another device gave the account its identity meanwhile.</exception>
    private (AccountIdentity, UnlockedIdentity, RecoveryKey?) CreateAccountIdentity(KofreClient client, Uri server, string email, string passPhrase)
    {
        try
        {
            var (identity, unlocked, recovery) = _server.Identities.CreateAsync(client, server, email, passPhrase).GetAwaiter().GetResult();
            _log.Info("Action", "Server account pass phrase chosen");
            return (identity, unlocked, recovery);
        }
        catch (ServerException ex) when (ex.Failure == ServerFailure.Conflict)
        {
            _ = _server.RefreshAsync();
            throw new IOException("Your account got its pass phrase on another device meanwhile. Cancel, then try again with that pass phrase.", ex);
        }
        catch (ServerException ex)
        {
            _log.Warn("Action", $"The server account's pass phrase cannot be set: {ex.Message}");
            throw new IOException(Explain(ex), ex);
        }
    }

    /// <summary>Asks a new pass phrase for the account, and seals its identity with it; null when cancelled.</summary>
    private AccountIdentity? ChangeAccountPassPhrase(Window owner, AccountIdentity current, UnlockedIdentity unlocked)
    {
        if (_server.Client is not { } client)
        {
            return null;
        }

        AccountIdentity? changed = null;
        var place = $"{current.Email} on {current.Server.Host}";
        if (!PassphraseDialog.AskToChangeAccount(owner, place, passPhrase =>
        {
            try
            {
                changed = _server.Identities.ChangePassPhraseAsync(client, current, unlocked, passPhrase).GetAwaiter().GetResult();
                return true;
            }
            catch (ServerException ex) when (ex.Failure == ServerFailure.Conflict)
            {
                _ = _server.RefreshAsync();
                throw new IOException("The pass phrase was changed on another device meanwhile: it is the one to use now.", ex);
            }
            catch (ServerException ex)
            {
                throw new IOException(Explain(ex), ex);
            }
        }))
        {
            _log.Info("Action", "Server account pass phrase change cancelled");
            return null;
        }

        _log.Info("Action", "Server account pass phrase changed");
        return changed;
    }

    /// <summary>
    /// Hands the open server vault to the account's identity, unless it is already: its pass
    /// phrase asked (chosen the first time). True when done or not needed; false when cancelled or
    /// not possible now.
    /// </summary>
    private bool AttachToAccount(Window owner, string folder, Vault vault)
    {
        if (Selected?.Location.IsOnServer != true || _access is not { } access
            || (_server.Identity is { Identity.SigningKey: { } key } kept && VaultKeys.IsAttached(folder, access.Header.Id, kept.Identity.Id, key)))
        {
            return true;
        }

        if (UnlockAccount(owner, AttachHint) is not { } account)
        {
            return false;
        }

        try
        {
            var attached = VaultKeys.Attach(folder, access, account.Identity.Identity, account.Unlocked);
            vault.UseKeys(attached.Keys);
            if (_keys.Has(vault.Id))
            {
                _keys.Remember(attached);
            }

            _access = attached;
            _log.Info("Action", $"Vault {vault.Id} handed to the server account, epoch {attached.Keys.CurrentEpoch}");
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _log.Error("Action", "Cannot hand the vault to the server account", ex);
            MessageDialog.Inform(owner, $"The vault cannot be handed to your server account.\n\n{ex.Message}");
            return false;
        }
    }
}
