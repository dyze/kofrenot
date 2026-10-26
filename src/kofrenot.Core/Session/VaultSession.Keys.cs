using Kofrenot.Core.Logging;
using Kofrenot.Core.Storage;

namespace Kofrenot.Core.Session;

/// <summary>
/// The keys of the vault: unlocked with what is remembered, its pass phrase or its recovery key;
/// its pass phrase changed. A server vault opens with its account's pass phrase, changed in
/// Settings > Server (<see cref="SetAccountPassPhrase(Window)"/>).
/// </summary>
public sealed partial class VaultSession
{
    // What opened the current vault (or the one waiting for its upgrade).
    private VaultAccess? _access;

    /// <summary>The pass phrase of the open vault can be changed: a folder vault, upgraded.</summary>
    public bool CanChangePassPhrase => !ReadsOnly && Current is not null && _access?.Identity is not null && Selected?.Location.IsOnServer == false;

    /// <summary>Asks a new pass phrase for the open vault, and seals its identity with it.</summary>
    public void ChangePassPhrase(ISessionQuestions ask)
    {
        if (CanChangePassPhrase && Folder is { } folder && ChangePassPhrase(ask, PlaceOf(Selected, folder), folder, _access!) is { } changed)
        {
            _access = changed;
        }
    }

    /// <summary>
    /// Unlocks a vault with what is remembered, else asks its pass phrase or recovery key. Null when
    /// cancelled; else what opens it, and whether to remember it.
    /// </summary>
    private (VaultAccess Access, bool Remember)? UnlockKeys(ISessionQuestions ask, string folder, VaultHeader header)
    {
        if (TakeUnlockedAhead(folder, header) is { } ahead)
        {
            _log.Info("Action", $"Vault {header.Id} unlocked");
            return (ahead.Access, Remembers(ahead.Access, ahead.Remember));
        }

        if (!_askPassPhrase && Remembered(folder, header) is { } remembered)
        {
            _log.Info(Category, $"Vault {header.Id} unlocked with the remembered pass phrase");
            return (remembered, false);
        }

        VaultAccess? access = null;
        var account = AccountIdentityOf(folder);
        var hint = Selected?.Location.IsOnServer == true ? "Type your server account's pass phrase (a vault not handed to it yet: its own)." : null;
        var answer = ask.AskPassPhrase(PlaceOf(Selected, folder), passPhrase =>
        {
            access = VaultKeys.Unlock(folder, header, passPhrase, _log, account, SignerOf(folder, header.Id));
            if (access is null)
            {
                _log.Warn(Category, $"Wrong pass phrase for vault {header.Id}");
            }
            else if (!IsCurrent(folder, access))
            {
                throw new IOException(OlderFilesOnly);
            }

            return access is not null;
        }, hint);
        if (answer is null)
        {
            _log.Info("Action", "Unlock cancelled");
            return null;
        }

        if (answer.UseRecoveryKey)
        {
            return Recover(ask, folder, header);
        }

        _log.Info("Action", $"Vault {header.Id} unlocked");
        return (access!, Remembers(access!, answer.Remember));
    }

    // Remembered when asked; a master key remembered before format 6 is replaced by the identity it now unlocks.
    private bool Remembers(VaultAccess access, bool asked) =>
        asked || (access.Identity is not null && _keys.RecallMasterKey(access.Header.Id) is not null);

    // A master key remembered before format 6 opens the vault until it has identities: then the
    // pass phrase is asked, so its identity is remembered instead. Either is used only while it
    // opens the latest files (a vault handed to its server account since: no longer).
    private VaultAccess? Remembered(string folder, VaultHeader header)
    {
        var remembered = _keys.RecallIdentity(header.Id) is { } identity ? VaultKeys.Unlock(folder, header, identity, _log, SignerOf(folder, header.Id))
            : _keys.RecallMasterKey(header.Id) is { } masterKey && !VaultKeys.HasIdentities(folder) ? new VaultAccess(header, KeyRing.Of(masterKey), Identity: null)
            : null;
        return remembered is not null && IsCurrent(folder, remembered) ? remembered : null;
    }

    /// <summary>Unlocks a vault with its recovery key (or its server account's), then asks a new pass phrase.</summary>
    private (VaultAccess Access, bool Remember)? Recover(ISessionQuestions ask, string folder, VaultHeader header)
    {
        var place = PlaceOf(Selected, folder);
        var account = AccountIdentityOf(folder);
        VaultAccess? access = null;
        var remember = ask.AskRecoveryKey(place, recovery =>
        {
            access = VaultKeys.Unlock(folder, header, recovery, _log, account, SignerOf(folder, header.Id));
            if (access is null)
            {
                _log.Warn(Category, $"Wrong recovery key for vault {header.Id}");
            }

            return access is not null;
        });
        if (remember is null || access is null)
        {
            _log.Info("Action", "Unlock with the recovery key cancelled");
            return null;
        }

        _log.Info("Action", $"Vault {header.Id} unlocked with its recovery key");
        if (_server.Identity is { } kept && access.Identity?.Id == kept.Identity.Id)
        {
            ChangeAccountPassPhrase(ask, kept, access.Identity);
        }
        else if (Selected?.Location.IsOnServer != true)
        {
            access = ChangePassPhrase(ask, place, folder, access) ?? access;
        }

        return (access, remember.Value);
    }

    /// <summary>Asks a new pass phrase and seals the vault's identity with it; null when cancelled.</summary>
    private VaultAccess? ChangePassPhrase(ISessionQuestions ask, string place, string folder, VaultAccess access)
    {
        VaultAccess? changed = null;
        if (!ask.AskToChange(place, passPhrase =>
        {
            changed = VaultKeys.ChangePassPhrase(folder, access, passPhrase, _keyIterations);
            return true;
        }))
        {
            _log.Info("Action", "Pass phrase change cancelled");
            return null;
        }

        _log.Info("Action", $"Pass phrase of vault {access.Header.Id} changed");
        return changed;
    }

    /// <summary>
    /// Gives a vault not upgraded its keys, once its upgrade is agreed: a folder vault's pass phrase
    /// asked again, then its recovery key shown; a server vault handed to its account. True when
    /// done, or not needed; false when cancelled.
    /// </summary>
    private bool UpgradeKeys(ISessionQuestions ask, Vault vault)
    {
        if (_access is not { Identity: null } access || Folder is not { } folder)
        {
            return true;
        }

        if (Selected?.Location.IsOnServer == true)
        {
            return AttachToAccount(ask, folder, vault);
        }

        var place = PlaceOf(Selected, folder);
        (VaultAccess Access, RecoveryKey? Recovery)? upgraded = null;
        var done = ask.AskToUpgrade(place, passPhrase =>
        {
            // Another device may have upgraded it already: its identity opens with the same pass phrase.
            upgraded = VaultKeys.Unlock(folder, access.Header, passPhrase, _log) is { Identity: not null } existing
                ? (existing, null)
                : VaultKeys.Upgrade(folder, access.Header, passPhrase, _keyIterations) is { } created ? (created.Access, created.Recovery) : null;
            return upgraded is not null;
        });
        if (!done || upgraded is not { } keys)
        {
            _log.Info("Action", $"Upgrade of the keys of vault {vault.Id} cancelled");
            return false;
        }

        _log.Info("Action", $"Keys of vault {vault.Id} upgraded");
        if (keys.Recovery is { } recovery)
        {
            ask.ShowRecoveryKey(place, recovery);
        }

        if (_keys.Has(vault.Id))
        {
            _keys.Remember(keys.Access);
        }

        _access = keys.Access;
        return true;
    }
}
