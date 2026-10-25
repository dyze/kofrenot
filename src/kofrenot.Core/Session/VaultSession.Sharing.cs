using Kofrenot.Core.Graph;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Server;
using Kofrenot.Core.Sharing;
using Kofrenot.Core.Storage;

namespace Kofrenot.Core.Session;

/// <summary>
/// A shared server vault: its members (recorded in the vault), shared by its owner with someone
/// found by email once the fingerprint of their key is compared; read only for its readers.
/// </summary>
public sealed partial class VaultSession
{
    private const string ReadOnlyMessage = "This vault is shared with you to read only: it cannot be changed.";

    /// <summary>The members of the open vault; none before it is shared.</summary>
    public IReadOnlyList<VaultMember> Members => Current is { } vault && OwnerKey is { } ownerKey ? MemberRecords.Members(vault.Graph, vault.Id, ownerKey) : [];

    /// <summary>The open vault is read only: shared with this account to read, or this client only reads (<see cref="ReadsOnly"/>).</summary>
    public bool IsReadOnly => Current is { } vault && (ReadsOnly || MyRoleIn(vault) == MemberRole.Reader);

    /// <summary>The open vault can be shared from here: a server vault of this account, opened with its pass phrase, the server recent enough.</summary>
    public bool CanShare =>
        !ReadsOnly
        && Current is { } vault
        && Selected?.Location.IsOnServer == true
        && _server.IsSignedIn && _server.TooOld is null
        && _server.Identity is { } account && _access?.Identity?.Id == account.Identity.Id
        && MyRoleIn(vault) is null or MemberRole.Owner;

    /// <summary>Asks who to share the open vault with, and shares it: their keys, their record in the vault, then their access on the server.</summary>
    public async Task Share(ISessionQuestions ask)
    {
        if (!CanShare || Current is not { } vault || Folder is not { } folder || Selected?.Location.Store is not { } store
            || _server.Client is not { } client || _server.Account is not { } account || _access is not { } access)
        {
            return;
        }

        var choice = ask.AskShare(PlaceOf(Selected, folder), email => VaultSharing.FindAsync(client, email));
        if (choice is null)
        {
            _log.Info("Action", "Sharing cancelled");
            return;
        }

        try
        {
            // Members check the owner's signatures: its signing key public first.
            if (_server.Identity is { } kept)
            {
                await _server.Identities.PublishSigningKeyAsync(client, kept, access.Identity!);
            }

            var me = await OwnerRecord(client, account.Email, access);
            var changes = VaultSharing.Share(folder, access, vault.Graph, me, choice.Candidate, choice.Role, DateTimeOffset.UtcNow);
            if (!Append(ask, vault, changes, $"Vault shared with an account as {choice.Role}"))
            {
                return;
            }

            await client.SetMemberAsync(store.Id, choice.Candidate.Account, VaultSharing.ToStoreRole(choice.Role));
            _sync.Soon();
            ask.Tell($"The vault is shared with {choice.Candidate.Email}. It appears in their Settings > Vault > Add... > Vault from the server..., and opens with their own pass phrase.");
        }
        catch (ServerException ex)
        {
            Inform(ask, "The vault cannot be shared", ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _log.Error("Action", "Cannot share the vault", ex);
            ask.Tell($"The vault cannot be shared.\n\n{ex.Message}");
        }
    }

    // The owner's record: its account found by its own email.
    private static async Task<VaultMember> OwnerRecord(KofreClient client, string email, VaultAccess access)
    {
        var (_, me) = await VaultSharing.FindAsync(client, email);
        return me is { SigningKey: { } signingKey } && me.Identity == access.Identity!.Id && signingKey.AsSpan().SequenceEqual(Signing.PublicKeyOf(access.Identity))
            ? me.As(MemberRole.Owner)
            : throw new InvalidOperationException("Your account's key on the server is not the one on this computer: sign out, then in again.");
    }

    // Null: not shared with this account (its own vault, or not shared yet).
    private MemberRole? MyRoleIn(Vault vault) =>
        _server.Identity is { } account && Current == vault ? VaultSharing.RoleOf(Members, account.Identity.Id) : null;

    /// <summary>False, the user told, when the open vault is read only for this account.</summary>
    private bool CanWrite(ISessionQuestions ask)
    {
        if (RefusesChanges(ask))
        {
            return false;
        }

        if (!IsReadOnly)
        {
            return true;
        }

        _log.Info("Action", "Change refused: the vault is read only");
        ask.Tell(ReadOnlyMessage);
        return false;
    }
}
