namespace Kofrenot.Core.Settings;

/// <summary>
/// A vault the user registered: its location, its id once its header was read (null for a vault
/// of an earlier version not opened since), and its optional hotkey.
/// </summary>
public sealed record RegisteredVault(VaultLocation Location, Guid? Id = null, Hotkey? Hotkey = null);

/// <summary>
/// The registered vaults, in the user's order. Each location and each vault id is listed once.
/// Immutable: each change gives a new list.
/// </summary>
public sealed class VaultList
{
    private readonly IReadOnlyList<RegisteredVault> _vaults;

    public VaultList(IEnumerable<RegisteredVault> vaults)
    {
        var listed = new List<RegisteredVault>();
        foreach (var vault in vaults)
        {
            if (!IsListed(listed, vault.Location, vault.Id))
            {
                listed.Add(vault);
            }
        }

        _vaults = listed;
    }

    public static VaultList Empty { get; } = new([]);

    public IReadOnlyList<RegisteredVault> Vaults => _vaults;

    public RegisteredVault? Find(VaultLocation location) => _vaults.FirstOrDefault(vault => vault.Location == location);

    /// <summary>Whether this location, or this vault elsewhere, is listed already.</summary>
    public bool IsListed(VaultLocation location, Guid? id) => IsListed(_vaults, location, id);

    /// <summary>The list with <paramref name="vault"/> last.</summary>
    /// <exception cref="ArgumentException">Its location or its id is listed already (<see cref="IsListed"/>).</exception>
    public VaultList Add(RegisteredVault vault) =>
        IsListed(vault.Location, vault.Id)
            ? throw new ArgumentException("This vault is listed already", nameof(vault))
            : new([.. _vaults, vault]);

    /// <summary>The list without the vault at <paramref name="location"/>; the same list when it is not there.</summary>
    public VaultList Remove(VaultLocation location) => new(_vaults.Where(vault => vault.Location != location));

    /// <summary>The list with the vault at <paramref name="location"/> moved by <paramref name="offset"/> places, stopped at either end.</summary>
    public VaultList Move(VaultLocation location, int offset)
    {
        var index = IndexOf(location);
        if (index < 0)
        {
            return this;
        }

        var moved = _vaults.ToList();
        var vault = moved[index];
        moved.RemoveAt(index);
        moved.Insert(Math.Clamp(index + offset, 0, moved.Count), vault);
        return new(moved);
    }

    /// <summary>The list with the vault at <paramref name="location"/> replaced by <paramref name="change"/>'s result.</summary>
    public VaultList With(VaultLocation location, Func<RegisteredVault, RegisteredVault> change) =>
        new(_vaults.Select(vault => vault.Location == location ? change(vault) : vault));

    private int IndexOf(VaultLocation location)
    {
        for (var index = 0; index < _vaults.Count; index++)
        {
            if (_vaults[index].Location == location)
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsListed(IEnumerable<RegisteredVault> vaults, VaultLocation location, Guid? id) =>
        vaults.Any(vault => vault.Location == location || (id is not null && vault.Id == id));
}
