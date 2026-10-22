using System.Buffers.Binary;
using System.Text;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Storage;

namespace Kofrenot.Core.Sharing;

/// <summary>
/// A shared vault's member records, signed by its owner: the members are those it signed, each by
/// its latest record (added, or removed). A record any other member wrote is ignored, so a writer
/// can neither add a key the owner's rotation would wrap for, nor drop a member.
/// </summary>
public static class MemberRecords
{
    /// <summary>A record of <paramref name="member"/>, signed by the vault's owner.</summary>
    public static MemberAdded Added(Guid vault, VaultMember member, DateTimeOffset at, UnlockedIdentity owner)
    {
        var id = Guid.NewGuid();
        return new MemberAdded(id, at, member, Signing.Sign(owner, Payload(vault, id, at, member)));
    }

    /// <summary>The removal of <paramref name="account"/>'s records, signed by the vault's owner.</summary>
    public static MemberRemoved Removed(Guid vault, Guid account, DateTimeOffset at, UnlockedIdentity owner)
    {
        var id = Guid.NewGuid();
        return new MemberRemoved(id, at, account, Signing.Sign(owner, Payload(vault, id, at, account)));
    }

    /// <summary>The members of a shared vault, as signed by its owner (<paramref name="ownerKey"/>: its signing key); none before it is shared.</summary>
    public static IReadOnlyList<VaultMember> Members(KnowledgeGraph graph, Guid vault, byte[] ownerKey) =>
        graph.MemberChanges
            .Where(change => IsSigned(change, vault, ownerKey))
            .GroupBy(change => change is MemberAdded added ? added.Member.Account : ((MemberRemoved)change).Account)
            .Select(records => records.OrderBy(change => change.At).ThenBy(change => change.Id).Last())
            .OfType<MemberAdded>()
            .Select(added => added.Member)
            .ToList();

    private static bool IsSigned(Change change, Guid vault, byte[] ownerKey) => change switch
    {
        MemberAdded added => Signing.Verify(ownerKey, Payload(vault, added.Id, added.At, added.Member), added.Signature),
        MemberRemoved removed => Signing.Verify(ownerKey, Payload(vault, removed.Id, removed.At, removed.Account), removed.Signature),
        _ => false,
    };

    // "kofrenot member" · vault · record · at · account · identity · its keys · email · role
    private static byte[] Payload(Guid vault, Guid record, DateTimeOffset at, VaultMember member) =>
    [
        .. Common("kofrenot member", vault, record, at, member.Account),
        .. member.Identity.ToByteArray(),
        .. Sized(member.PublicKey),
        .. Sized(member.SigningKey ?? []),
        .. Sized(Encoding.UTF8.GetBytes(member.Email)),
        (byte)member.Role,
    ];

    private static byte[] Payload(Guid vault, Guid record, DateTimeOffset at, Guid account) =>
        Common("kofrenot member removed", vault, record, at, account);

    private static byte[] Common(string kind, Guid vault, Guid record, DateTimeOffset at, Guid account)
    {
        var ticks = new byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(ticks, at.UtcTicks);
        return [.. Sized(Encoding.ASCII.GetBytes(kind)), .. vault.ToByteArray(), .. record.ToByteArray(), .. ticks, .. account.ToByteArray()];
    }

    // Length first: two fields cannot run into each other.
    private static byte[] Sized(byte[] bytes)
    {
        var length = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
        return [.. length, .. bytes];
    }
}
