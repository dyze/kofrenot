using Kofrenot.Core.Graph;
using Kofrenot.Core.Server;
using Kofrenot.Core.Storage;

namespace Kofrenot.Core.Sharing;

/// <summary>Someone a vault can be shared with, as found by email: their account, and their identity's key.</summary>
public sealed record ShareCandidate(Guid Account, string Email, Guid Identity, byte[] PublicKey)
{
    /// <summary>What the owner and they compare, out of band, before sharing.</summary>
    public string Fingerprint => Storage.Fingerprint.Of(PublicKey);

    public VaultMember As(MemberRole role) => new(Account, Identity, PublicKey, Email, role);
}

/// <summary>What looking someone up by email gave.</summary>
public enum Lookup
{
    Found,

    /// <summary>No active account with this email.</summary>
    NoAccount,

    /// <summary>An account without an identity yet: they open kofrenot 0.7 and choose their pass phrase first.</summary>
    NoKeyYet,
}

/// <summary>
/// Sharing a server vault (its owner): someone found by email, the fingerprint of their key
/// compared out of band, then every key of the vault wrapped for them and their record written in
/// the vault, so every member's device knows who shares it and with which key.
/// </summary>
public static class VaultSharing
{
    /// <summary>Finds who to share with by their exact email.</summary>
    /// <exception cref="ServerException">Too many lookups, or the call failed.</exception>
    public static async Task<(Lookup Result, ShareCandidate? Candidate)> FindAsync(KofreClient client, string email, CancellationToken cancel = default)
    {
        if (await client.FindAccountAsync(email.Trim(), cancel) is not { } account)
        {
            return (Lookup.NoAccount, null);
        }

        return AccountIdentities.KeyOf(account.Public) is { } key
            ? (Lookup.Found, new ShareCandidate(account.Id, email.Trim(), key.Id, key.PublicKey))
            : (Lookup.NoKeyYet, null);
    }

    /// <summary>
    /// Shares the open vault with <paramref name="member"/>: every key of <paramref name="access"/>
    /// wrapped for them now, and the changes to write recording them (and its owner, the first
    /// time). A record already as wanted is not written again.
    /// </summary>
    /// <exception cref="IOException">A key file cannot be written.</exception>
    public static IReadOnlyList<Change> Share(string folder, VaultAccess access, KnowledgeGraph graph, VaultMember owner, ShareCandidate member, MemberRole role, DateTimeOffset now)
    {
        VaultKeys.Share(folder, access, member.Identity, member.PublicKey);
        var changes = new List<Change>();
        if (graph.Members.All(recorded => recorded.Account != owner.Account))
        {
            changes.Add(new MemberAdded(Guid.NewGuid(), now, owner));
        }

        if (graph.Members.FirstOrDefault(recorded => recorded.Account == member.Account) is not { } current
            || current.Role != role || current.Identity != member.Identity)
        {
            changes.Add(new MemberAdded(Guid.NewGuid(), now, member.As(role)));
        }

        return changes;
    }

    /// <summary>
    /// Takes members out of the open vault (removed, or gone from the server): a new epoch wrapped
    /// for every other member (<paramref name="owner"/> included), and the change removing their
    /// records, to write with it. Returns the access with the new epoch.
    /// </summary>
    /// <exception cref="InvalidOperationException">The access does not hold the latest epoch, or has no identity.</exception>
    /// <exception cref="IOException">A key file cannot be written.</exception>
    public static (VaultAccess Access, ChangesRemoved Removal) Remove(
        string folder, VaultAccess access, KnowledgeGraph graph, IReadOnlyCollection<Guid> accounts, AccountKey owner, DateTimeOffset now)
    {
        var staying = graph.Members
            .Where(member => !accounts.Contains(member.Account))
            .Select(member => (member.Identity, member.PublicKey))
            .Append((owner.Id, owner.PublicKey))
            .DistinctBy(identity => identity.Item1)
            .ToList();
        var rotated = VaultKeys.Rotate(folder, access, staying);
        var records = accounts.SelectMany(graph.MemberRecordsOf).ToList();
        return (rotated, new ChangesRemoved(Guid.NewGuid(), now, records));
    }

    /// <summary>The members recorded in the vault who are no longer members on the server: they left (or were removed from another device).</summary>
    public static IReadOnlyList<VaultMember> Gone(KnowledgeGraph graph, IEnumerable<StoreMember> onServer)
    {
        var members = onServer.Select(member => member.Account).ToHashSet();
        return graph.Members.Where(member => member.Role != MemberRole.Owner && !members.Contains(member.Account)).ToList();
    }

    /// <summary>The role of <paramref name="identity"/> in a shared vault; null when it is not shared with it (its own vault).</summary>
    public static MemberRole? RoleOf(KnowledgeGraph graph, Guid identity) =>
        graph.Members.FirstOrDefault(member => member.Identity == identity)?.Role;

    public static StoreRole ToStoreRole(MemberRole role) => role switch
    {
        MemberRole.Reader => StoreRole.Reader,
        MemberRole.Writer => StoreRole.Writer,
        _ => StoreRole.Owner,
    };
}
