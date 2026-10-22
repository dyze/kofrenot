using Kofrenot.Core.Browse;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Server;

namespace Kofrenot.Core.Sharing;

/// <summary>A server vault's owner: its account, and its identity's public keys (what its signatures are checked with).</summary>
public sealed record VaultOwner(Guid Account, AccountKey Key);

/// <summary>
/// The owners of the server vaults on this computer, by store: read once from the server (the
/// member with the owner role, and the public part of its data), then kept, so a vault shared with
/// this account checks its owner's signatures offline, and a server cannot swap the owner later.
/// Its fingerprint is shown among the vault's members, to compare with the owner.
/// </summary>
public sealed class VaultOwners(string path, ILog? log = null)
{
    private readonly LocalJsonFile<Dictionary<Guid, VaultOwner>> _file = new(path, "Sync", "vault owners", log);

    public VaultOwner? Kept(Guid store) => _file.Load().GetValueOrDefault(store);

    /// <summary>
    /// The owner of <paramref name="store"/>: kept, or read from the server and kept once it has its
    /// signing key. Null when it cannot be known yet.
    /// </summary>
    /// <exception cref="ServerException">The call failed.</exception>
    public async Task<VaultOwner?> OwnerOfAsync(KofreClient client, Guid store, CancellationToken cancel = default)
    {
        if (Kept(store) is { } kept)
        {
            return kept;
        }

        if ((await client.MembersAsync(store, cancel)).FirstOrDefault(member => member.Role == StoreRole.Owner) is not { } owner
            || AccountIdentities.KeyOf(await client.PublicPartAsync(owner.Account, cancel)) is not { SigningKey: not null } key)
        {
            return null;
        }

        var found = new VaultOwner(owner.Account, key);
        var owners = _file.Load();
        owners[store] = found;
        _file.Save(owners);
        return found;
    }
}
