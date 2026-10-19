using Kofrenot.Core.Logging;
using Kofrenot.Core.Settings;

namespace Kofrenot.UI;

/// <summary>The hotkeys: the one that shows kofrenot, and one per vault that has one.</summary>
public partial class App
{
    // The hotkey that shows kofrenot; the vaults' hotkeys follow it, by vault.
    private const int MainHotkeyId = 1;
    private readonly Dictionary<int, VaultLocation> _vaultHotkeys = [];
    private IReadOnlyList<RegisteredVault> _vaultHotkeysRegistered = [];
    private IReadOnlyList<VaultLocation> _vaultHotkeysRefused = [];

    /// <summary>Registers a new hotkey. One that another program already uses: <paramref name="current"/> is registered again.</summary>
    private bool ApplyHotkey(Hotkey wanted, Hotkey current)
    {
        if (_hotkey is null || _hotkey.Register(MainHotkeyId, wanted))
        {
            return _hotkey is not null;
        }

        _hotkey.Register(MainHotkeyId, current);
        return false;
    }

    /// <summary>
    /// Gives a vault a new hotkey, or none. One that another program already uses is refused
    /// (false): the vault keeps its hotkey.
    /// </summary>
    private bool ApplyVaultHotkey(SettingsStore settings, VaultLocation location, Hotkey? wanted, ILog log)
    {
        var before = settings.Vaults;
        settings.SetVaults(before.With(location, vault => vault with { Hotkey = wanted }));
        if (!RegisterVaultHotkeys(settings, log).Contains(location))
        {
            return true;
        }

        settings.SetVaults(before);
        RegisterVaultHotkeys(settings, log);
        return false;
    }

    /// <summary>
    /// Registers the hotkey of each vault that has one, when they changed since the last time.
    /// The vaults whose hotkey another program already uses.
    /// </summary>
    private IReadOnlyList<VaultLocation> RegisterVaultHotkeys(SettingsStore settings, ILog log)
    {
        var wanted = settings.Vaults.Vaults.Where(vault => vault.Hotkey is not null).ToList();
        if (_hotkey is null || wanted.SequenceEqual(_vaultHotkeysRegistered))
        {
            return _vaultHotkeysRefused;
        }

        foreach (var registered in _vaultHotkeys.Keys)
        {
            _hotkey.Unregister(registered);
        }

        _vaultHotkeys.Clear();
        var refused = new List<VaultLocation>();
        var id = MainHotkeyId;
        foreach (var vault in wanted)
        {
            if (_hotkey.Register(++id, vault.Hotkey!))
            {
                _vaultHotkeys[id] = vault.Location;
            }
            else
            {
                log.Warn("App", $"Hotkey {vault.Hotkey} of vault {vault.Id} is used by another program");
                refused.Add(vault.Location);
            }
        }

        _vaultHotkeysRegistered = wanted;
        _vaultHotkeysRefused = refused;
        return refused;
    }
}
