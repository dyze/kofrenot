using Kofrenot.Core.Browse;
using Kofrenot.Core.Logging;

namespace Kofrenot.Core.Storage;

/// <summary>
/// The vault upgrades the user agreed to on this device (<see cref="Vault.NeedsUpgrade"/>): the
/// highest format agreed for each vault, so it is not asked again until a newer kofrenot raises the
/// format once more. Kept on this device only (<see cref="LocalJsonFile{T}"/>), holding ids, never names.
/// </summary>
public sealed class VaultUpgrades(string path, ILog? log = null)
{
    private readonly LocalJsonFile<Dictionary<Guid, int>> _file = new(path, "Vault", "vault upgrades agreed", log);
    private Dictionary<Guid, int>? _byVault;

    private Dictionary<Guid, int> ByVault => _byVault ??= _file.Load();

    /// <summary>Whether the upgrade of <paramref name="vault"/> to <paramref name="format"/> was agreed.</summary>
    public bool IsAgreed(Guid vault, int format) => ByVault.GetValueOrDefault(vault) >= format;

    /// <summary>The upgrade of <paramref name="vault"/> to <paramref name="format"/> is agreed: saved.</summary>
    public void Agree(Guid vault, int format)
    {
        if (!IsAgreed(vault, format))
        {
            ByVault[vault] = format;
            _file.Save(ByVault);
        }
    }
}
