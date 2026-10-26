using Kofrenot.Core.Logging;
using Kofrenot.Core.Server;
using Kofrenot.Core.Settings;
using Kofrenot.Core.Storage;

namespace Kofrenot.Core.Session;

/// <summary>
/// Whose signatures a server vault's keys and members need: its owner's. This account for its own
/// vaults (once handed to it: its account's signing key; before, the vault's own identity); the
/// owner kept for a vault shared with it. A folder vault checks none: only this user writes to it.
/// </summary>
public sealed partial class VaultSession
{
    /// <summary>Who must have signed the keys of the server vault in <paramref name="folder"/>; null for a folder vault.</summary>
    private KeySigner? SignerOf(string folder, Guid vault) =>
        Selected?.Location is { } location && location.Folder == folder ? SignerOf(location, vault) : null;

    private KeySigner? SignerOf(VaultLocation location, Guid vault)
    {
        if (location is not { IsOnServer: true, Store: { } store })
        {
            return null;
        }

        var mine = _server.Identity?.Identity;
        if (_server.Owners.Kept(store.Id) is { } owner && owner.Key.Id != mine?.Id)
        {
            return new KeySigner(owner.Key.SigningKey);
        }

        return mine?.SigningKey is { } key && VaultKeys.IsAttached(location.Folder, vault, mine.Id, key) ? new KeySigner(key) : KeySigner.Self;
    }

    /// <summary>Whether <paramref name="access"/> opens the files written last, as signed by the vault's owner.</summary>
    private bool IsCurrent(string folder, VaultAccess access) =>
        VaultKeys.IsCurrent(folder, access, access.Identity is { } identity ? SignerOf(folder, access.Header.Id)?.KeyFor(identity) : null);

    /// <summary>The signing key of the open vault's owner: what its member records need; null for a folder vault.</summary>
    private byte[]? OwnerKey =>
        Folder is { } folder && _access is { Identity: { } identity } access && SignerOf(folder, access.Header.Id) is { } signer
            ? signer.KeyFor(identity)
            : null;

    /// <summary>
    /// Adds the signing key to the account's public data, for an identity made before it had one
    /// (runs in the background, while a dialog waits). The identity as kept now.
    /// </summary>
    /// <exception cref="IOException">The server refused.</exception>
    private AccountIdentity Published(KofreClient client, AccountIdentity kept, UnlockedIdentity unlocked)
    {
        try
        {
            return _server.Identities.PublishSigningKeyAsync(client, kept, unlocked).GetAwaiter().GetResult();
        }
        catch (ServerException ex)
        {
            _log.Warn("Action", $"The account's signing key cannot be published: {ex.Message}");
            throw new IOException(Explain(ex), ex);
        }
    }
}
