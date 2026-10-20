using System.Windows;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Settings;
using Kofrenot.Core.Storage;

namespace Kofrenot.UI;

/// <summary>
/// The registered vaults: switching from one to another, adding, removing and ordering them, and
/// how each is shown (its name, kept on this computer to show it while locked).
/// </summary>
public sealed partial class VaultSession
{
    /// <summary>The registered vaults, in the user's order.</summary>
    public IReadOnlyList<RegisteredVault> Vaults => _settings.Vaults.Vaults;

    /// <summary>The vault to open, or open; null when none is registered.</summary>
    public RegisteredVault? Selected => _settings.SelectedVault is { } location ? _settings.Vaults.Find(location) : null;

    public string? Folder => Selected?.Location.Folder;

    /// <summary>
    /// The name a vault is shown by: its name when open, else the last one read in it on this
    /// computer, else its folder's name.
    /// </summary>
    public string NameOf(RegisteredVault vault) =>
        (Current is { } open && open.Id == vault.Id ? open.Graph.VaultName : null)
        ?? (vault.Id is { } id ? _names.Recall(id) : null)
        ?? vault.Location.FolderName;

    /// <summary>Whether the key of <paramref name="vault"/> is remembered on this computer.</summary>
    public bool IsRemembered(RegisteredVault vault) => vault.Id is { } id && _keys.Has(id);

    /// <summary>Forgets the remembered key of <paramref name="vault"/>: its pass phrase is asked when it is opened again.</summary>
    public void Forget(RegisteredVault vault)
    {
        if (vault.Id is { } id)
        {
            _keys.Forget(id);
        }
    }

    /// <summary>Closes the open vault, still selected: <see cref="Open"/> unlocks it again.</summary>
    public void Lock()
    {
        if (Current is { } vault)
        {
            _log.Info("Action", $"Vault {vault.Id} locked");
            Close();
        }
    }

    /// <summary>
    /// Closes the open vault, then opens the registered one at <paramref name="target"/>, selected
    /// from now on. When it cannot be opened (its pass phrase cancelled, not a vault, an error),
    /// the vault open before is used again.
    /// </summary>
    public void SwitchTo(Window owner, VaultLocation target)
    {
        if (target == _settings.SelectedVault && (Current is not null || _notUpgraded is not null))
        {
            return;
        }

        var (before, beforeVault) = (_settings.SelectedVault, Current);
        Close();
        _settings.SelectVault(target);
        _log.Info("Action", $"Switch to vault {Selected?.Id?.ToString() ?? "not opened yet"}");
        Open(owner);
        if (Current is not null || _notUpgraded is not null || NewerFormat is not null || before is null || beforeVault is null)
        {
            return;
        }

        _log.Info(Category, $"Vault {beforeVault.Id} used again");
        _settings.SelectVault(before);
        Use(owner, before.Folder, beforeVault);
        Refresh();
        Changed?.Invoke();
    }

    /// <summary>
    /// Adds the vault in <paramref name="folder"/>, then switches to it: an empty folder becomes a
    /// new vault. What the user is told when it cannot be added, or null. Not added either when
    /// the new vault's creation is cancelled.
    /// </summary>
    public string? Register(Window owner, string folder)
    {
        var location = new VaultLocation(folder);
        var kind = VaultFolder.Inspect(folder);
        if (kind == FolderKind.Other)
        {
            return $"{folder} holds other files.\n\nChoose an empty folder for a new vault, or the folder of a kofrenot vault.";
        }

        if (_settings.Vaults.IsListed(location, kind == FolderKind.Vault ? IdIn(folder) : null))
        {
            return "This vault is in the list already.";
        }

        _settings.SetVaults(_settings.Vaults.Add(new RegisteredVault(location)));
        _log.Info("Settings", $"Vault added: {folder}");
        SwitchTo(owner, location);
        if (VaultFolder.Inspect(folder) == FolderKind.Empty)
        {
            _settings.SetVaults(_settings.Vaults.Remove(location));
            _log.Info("Settings", "Vault not added: its creation was cancelled");
            Changed?.Invoke();
        }

        return null;
    }

    /// <summary>
    /// Takes a vault out of the list, forgetting its key and name on this computer; its folder is
    /// not touched. The open vault is closed, and the next one in the list opened.
    /// </summary>
    public void Unregister(Window owner, VaultLocation location)
    {
        if (_settings.Vaults.Find(location) is not { } vault)
        {
            return;
        }

        var index = Vaults.ToList().IndexOf(vault);
        var wasSelected = location == _settings.SelectedVault;
        if (wasSelected)
        {
            Close();
        }

        if (vault.Id is { } id)
        {
            _keys.Forget(id);
            _names.Forget(id);
        }

        _settings.SetVaults(_settings.Vaults.Remove(location));
        _log.Info("Settings", $"Vault removed: {vault.Id?.ToString() ?? location.Folder}");
        if (wasSelected && Vaults.Count > 0)
        {
            SwitchTo(owner, Vaults[Math.Min(index, Vaults.Count - 1)].Location);
        }

        Changed?.Invoke();
    }

    /// <summary>Moves a vault up (negative <paramref name="offset"/>) or down the list.</summary>
    public void Move(VaultLocation location, int offset)
    {
        _settings.SetVaults(_settings.Vaults.Move(location, offset));
        _log.Info("Settings", $"Vault moved {(offset < 0 ? "up" : "down")}");
        Changed?.Invoke();
    }

    /// <summary>Names the open vault. False when none is open, or it cannot be written (the user is told).</summary>
    public bool Name(Window owner, VaultNamed name) =>
        Write(owner, name, name.Replaces.Count > 1 ? $"Vault named (a conflict of {name.Replaces.Count} names resolved)" : "Vault named");

    /// <summary>The id in the header of the vault in <paramref name="folder"/>; null when it cannot be read (opening it says why).</summary>
    private static Guid? IdIn(string folder)
    {
        try
        {
            return VaultHeader.Read(VaultFolder.HeaderOf(folder)).Id;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>Keeps the open vault's name on this computer, to show it while the vault is locked.</summary>
    private void KeepName()
    {
        if (Current is { Graph.VaultName: { } name } vault && name != _keptName)
        {
            _names.Remember(vault.Id, name);
            _keptName = name;
        }
    }

    /// <summary>Records the selected vault's id, read in its header, when it is not known yet (a vault of an earlier version, or new).</summary>
    private void Identify(VaultHeader header)
    {
        if (Selected is { Id: null } selected && _settings.Vaults.Find(header.Id) is null)
        {
            _settings.SetVaults(_settings.Vaults.With(selected.Location, vault => vault with { Id = header.Id }));
        }
    }
}
