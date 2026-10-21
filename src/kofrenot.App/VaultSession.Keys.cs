using System.Windows;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Storage;
using Kofrenot.UI.Views;

namespace Kofrenot.UI;

/// <summary>
/// The keys of the vault: unlocked with what is remembered, its pass phrase or its recovery key;
/// its pass phrase changed. A server vault's identity is in its store, written once: its pass
/// phrase cannot change.
/// </summary>
public sealed partial class VaultSession
{
    // What opened the current vault (or the one waiting for its upgrade).
    private VaultAccess? _access;

    /// <summary>The pass phrase of the open vault can be changed: a folder vault, upgraded.</summary>
    public bool CanChangePassPhrase => Current is not null && _access?.Identity is not null && Selected?.Location.IsOnServer == false;

    /// <summary>Asks a new pass phrase for the open vault, and seals its identity with it.</summary>
    public void ChangePassPhrase(Window owner)
    {
        if (CanChangePassPhrase && Folder is { } folder && ChangePassPhrase(owner, PlaceOf(Selected, folder), folder, _access!) is { } changed)
        {
            _access = changed;
        }
    }

    /// <summary>
    /// Unlocks a vault with what is remembered, else asks its pass phrase or recovery key. Null when
    /// cancelled; else what opens it, and whether to remember it.
    /// </summary>
    private (VaultAccess Access, bool Remember)? UnlockKeys(Window owner, string folder, VaultHeader header)
    {
        if (!_askPassPhrase && Remembered(folder, header) is { } remembered)
        {
            _log.Info(Category, $"Vault {header.Id} unlocked with the remembered pass phrase");
            return (remembered, false);
        }

        VaultAccess? access = null;
        var answer = PassphraseDialog.Ask(owner, PlaceOf(Selected, folder), passPhrase =>
        {
            access = VaultKeys.Unlock(folder, header, passPhrase, _log);
            if (access is null)
            {
                _log.Warn(Category, $"Wrong pass phrase for vault {header.Id}");
            }

            return access is not null;
        });
        if (answer is null)
        {
            _log.Info("Action", "Unlock cancelled");
            return null;
        }

        if (answer.UseRecoveryKey)
        {
            return Recover(owner, folder, header);
        }

        _log.Info("Action", $"Vault {header.Id} unlocked");

        // A master key remembered before format 6 is replaced by the identity it now unlocks.
        return (access!, answer.Remember || (access!.Identity is not null && _keys.RecallMasterKey(header.Id) is not null));
    }

    // A master key remembered before format 6 opens the vault until it has identities: then the
    // pass phrase is asked, so its identity is remembered instead.
    private VaultAccess? Remembered(string folder, VaultHeader header) =>
        _keys.RecallIdentity(header.Id) is { } identity ? VaultKeys.Unlock(folder, header, identity, _log)
        : _keys.RecallMasterKey(header.Id) is { } masterKey && !VaultKeys.HasIdentities(folder) ? new VaultAccess(header, KeyRing.Of(masterKey), Identity: null)
        : null;

    /// <summary>Unlocks a vault with its recovery key, then asks a new pass phrase (a folder vault).</summary>
    private (VaultAccess Access, bool Remember)? Recover(Window owner, string folder, VaultHeader header)
    {
        var place = PlaceOf(Selected, folder);
        VaultAccess? access = null;
        var remember = RecoveryKeyDialog.Ask(owner, place, recovery =>
        {
            access = VaultKeys.Unlock(folder, header, recovery, _log);
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
        if (Selected?.Location.IsOnServer != true)
        {
            access = ChangePassPhrase(owner, place, folder, access) ?? access;
        }

        return (access, remember.Value);
    }

    /// <summary>Asks a new pass phrase and seals the vault's identity with it; null when cancelled.</summary>
    private VaultAccess? ChangePassPhrase(Window owner, string place, string folder, VaultAccess access)
    {
        VaultAccess? changed = null;
        if (!PassphraseDialog.AskToChange(owner, place, passPhrase =>
        {
            changed = VaultKeys.ChangePassPhrase(folder, access, passPhrase);
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
    /// Gives a vault not upgraded its keys, once its upgrade is agreed: its pass phrase asked again,
    /// then its recovery key shown. True when done, or not needed; false when cancelled.
    /// </summary>
    private bool UpgradeKeys(Window owner, Vault vault)
    {
        if (_access is not { Identity: null } access || Folder is not { } folder)
        {
            return true;
        }

        var place = PlaceOf(Selected, folder);
        (VaultAccess Access, RecoveryKey? Recovery)? upgraded = null;
        var done = PassphraseDialog.AskToUpgrade(owner, place, passPhrase =>
        {
            // Another device may have upgraded it already: its identity opens with the same pass phrase.
            upgraded = VaultKeys.Unlock(folder, access.Header, passPhrase, _log) is { Identity: not null } existing
                ? (existing, null)
                : VaultKeys.Upgrade(folder, access.Header, passPhrase) is { } created ? (created.Access, created.Recovery) : null;
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
            RecoveryKeyDialog.Show(owner, place, recovery);
        }

        if (_keys.Has(vault.Id))
        {
            _keys.Remember(keys.Access);
        }

        _access = keys.Access;
        return true;
    }
}
