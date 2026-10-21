using System.Windows;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Storage;
using Kofrenot.UI.Views;

namespace Kofrenot.UI;

/// <summary>
/// The upgrade of a vault of an older format (<see cref="Vault.NeedsUpgrade"/>): asked once per
/// vault and device before it is used. Declined, the vault stays unlocked but not used until
/// <see cref="Upgrade"/> is agreed, so older kofrenot keep opening it.
/// </summary>
public sealed partial class VaultSession
{
    private readonly VaultUpgrades _upgrades;
    private Vault? _notUpgraded;

    /// <summary>The format of the vault unlocked but not used, its upgrade declined; null otherwise.</summary>
    public int? OlderFormat => _notUpgraded?.Format;

    /// <summary>Asks again to upgrade the vault whose upgrade was declined, then uses it.</summary>
    public void Upgrade(Window owner)
    {
        if (_notUpgraded is not { } vault || Folder is not { } folder)
        {
            return;
        }

        _notUpgraded = null;
        try
        {
            // Another device may have upgraded it meanwhile: then nothing is asked.
            vault.Refresh();
            Use(owner, folder, vault);
        }
        catch (NewerVaultFormatException ex)
        {
            NeedsNewerKofrenot(ex);
        }

        Changed?.Invoke();
    }

    /// <summary>True when the vault needs no upgrade, or the user agrees to it (now, or before on this device).</summary>
    private bool AgreesToUpgrade(Window owner, Vault vault)
    {
        if (!vault.NeedsUpgrade)
        {
            return true;
        }

        var agreed = _upgrades.IsAgreed(vault.Id, VaultFormat.Current);
        if (!agreed && !MessageDialog.Confirm(owner, VaultFormat.UpgradeQuestion(vault.Format), "Upgrade and open", "Not now"))
        {
            _log.Info("Action", $"Upgrade of vault {vault.Id} from format {vault.Format} declined");
            _notUpgraded = vault;
            return false;
        }

        // Format 6 seals the vault's key for an identity: made now, with the pass phrase.
        if (!UpgradeKeys(owner, vault))
        {
            _notUpgraded = vault;
            return false;
        }

        if (!agreed)
        {
            _log.Info("Action", $"Upgrade of vault {vault.Id} from format {vault.Format} agreed");
            _upgrades.Agree(vault.Id, VaultFormat.Current);
        }

        vault.AgreeToUpgrade();
        return true;
    }
}
