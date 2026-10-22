using System.Net;
using System.Text.Json;
using Kofrenot.Core.Sharing;
using Kofrenot.Core.Tests.Server;
using static Kofrenot.Core.Tests.Server.StubHandler;

namespace Kofrenot.Core.Tests.Sharing;

public sealed class VaultSharingTests : IDisposable
{
    private const int Iterations = 10;
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid OwnerAccount = Guid.NewGuid();
    private static readonly Guid MemberAccount = Guid.NewGuid();

    private readonly TempFolder _temp = new();
    private readonly ManualClock _clock = new();
    private readonly (Identity Identity, UnlockedIdentity Unlocked, RecoveryKey Recovery) _owner = Identity.Create("owner phrase", Iterations);
    private readonly (Identity Identity, UnlockedIdentity Unlocked, RecoveryKey Recovery) _member = Identity.Create("member phrase", Iterations);

    private string Folder => _temp.Sub("vault");

    [Fact]
    public async Task AnAccountWithAKey_IsFound_WithItsFingerprint()
    {
        var publicPart = JsonSerializer.SerializeToUtf8Bytes(new AccountKey(_member.Identity.Id, _member.Identity.PublicKey));
        var account = Guid.NewGuid();
        var stub = new StubHandler(_ => Json(HttpStatusCode.OK, JsonSerializer.Serialize(new { id = account, @public = publicPart })));

        var (result, candidate) = await VaultSharing.FindAsync(Client(stub), " bob@example.com ");

        Assert.Equal(Lookup.Found, result);
        Assert.Equal((account, "bob@example.com", _member.Identity.Id), (candidate!.Account, candidate.Email, candidate.Identity));
        Assert.Equal(Fingerprint.Of(_member.Identity.PublicKey), candidate.Fingerprint);
        Assert.Equal("/v1/accounts?email=bob%40example.com", stub.Requests[0].Path);
    }

    [Fact]
    public async Task NoAccount_OrOneWithoutKey_IsSaid()
    {
        var found = false;
        var stub = new StubHandler(_ => found ? Json(HttpStatusCode.OK, $$"""{"id":"{{Guid.NewGuid()}}","public":null}""") : Json(HttpStatusCode.NotFound, """{"error":"no-account"}"""));

        Assert.Equal(Lookup.NoAccount, (await VaultSharing.FindAsync(Client(stub), "x@example.com")).Result);
        found = true;
        Assert.Equal(Lookup.NoKeyYet, (await VaultSharing.FindAsync(Client(stub), "x@example.com")).Result);
    }

    [Fact]
    public void Sharing_WrapsEveryKeyForTheMember_AndRecordsTheOwnerTheFirstTime()
    {
        var (access, vault) = OwnersVault();

        var changes = VaultSharing.Share(Folder, access, vault.Graph, Owner, Candidate, MemberRole.Writer, Now);

        Assert.Equal([MemberRole.Owner, MemberRole.Writer], changes.Cast<MemberAdded>().Select(change => change.Member.Role));
        var opened = VaultKeys.Unlock(Folder, access.Header, _member.Unlocked);
        Assert.Equal(access.Keys.CurrentKey, opened?.Keys.CurrentKey);
        foreach (var change in changes)
        {
            vault.Append(change);
        }

        Assert.Equal(MemberRole.Writer, VaultSharing.RoleOf(vault.Graph, _member.Identity.Id));
        Assert.Null(VaultSharing.RoleOf(vault.Graph, Guid.NewGuid()));
        Assert.Empty(VaultSharing.Share(Folder, access, vault.Graph, Owner, Candidate, MemberRole.Writer, Now));
        var roleChanged = Assert.Single(VaultSharing.Share(Folder, access, vault.Graph, Owner, Candidate, MemberRole.Reader, Now.AddMinutes(1)));
        vault.Append(roleChanged);
        Assert.Equal(MemberRole.Reader, VaultSharing.RoleOf(vault.Graph, _member.Identity.Id));
        Assert.Equal(2, vault.Graph.Members.Count);
    }

    [Fact]
    public void TheMembers_AreReadBack_FromTheVaultsFiles()
    {
        var (access, vault) = OwnersVault();
        foreach (var change in VaultSharing.Share(Folder, access, vault.Graph, Owner, Candidate, MemberRole.Reader, Now))
        {
            vault.Append(change);
        }

        var members = Vault.Open(Folder, access.Header, access.Keys).Graph.Members;

        var bob = Assert.Single(members, member => member.Email == "bob@example.com");
        Assert.Equal((MemberRole.Reader, _member.Identity.Id), (bob.Role, bob.Identity));
        Assert.Equal(_member.Identity.PublicKey, bob.PublicKey);
    }

    [Fact]
    public void ARemovedMember_IsNoLongerListed()
    {
        var (access, vault) = OwnersVault();
        foreach (var change in VaultSharing.Share(Folder, access, vault.Graph, Owner, Candidate, MemberRole.Reader, Now))
        {
            vault.Append(change);
        }

        vault.Append(new ChangesRemoved(Guid.NewGuid(), Now, vault.Graph.MemberRecordsOf(Candidate.Account)));

        Assert.Equal([Owner.Account], vault.Graph.Members.Select(member => member.Account));
    }

    [Fact]
    public void AnAccessBehind_CannotShare()
    {
        var (access, vault) = OwnersVault();
        VaultKeys.Attach(Folder, access, _member.Identity, _member.Unlocked);

        Assert.Throws<InvalidOperationException>(() => VaultSharing.Share(Folder, access, vault.Graph, Owner, Candidate, MemberRole.Reader, Now));
    }

    [Theory]
    [InlineData(MemberRole.Reader, StoreRole.Reader)]
    [InlineData(MemberRole.Writer, StoreRole.Writer)]
    [InlineData(MemberRole.Owner, StoreRole.Owner)]
    public void ARole_IsTheStoresRole(MemberRole role, StoreRole store) =>
        Assert.Equal(store, VaultSharing.ToStoreRole(role));

    public void Dispose() => _temp.Dispose();

    private VaultMember Owner => new(OwnerAccount, _owner.Identity.Id, _owner.Identity.PublicKey, "ada@example.com", MemberRole.Owner);

    private ShareCandidate Candidate => new(MemberAccount, "bob@example.com", _member.Identity.Id, _member.Identity.PublicKey);

    private (VaultAccess Access, Vault Vault) OwnersVault()
    {
        var access = VaultFolder.Create(Folder, _owner.Identity, _owner.Unlocked);
        return (access, Vault.Open(Folder, access.Header, access.Keys));
    }

    private KofreClient Client(StubHandler stub) => new(stub.Client(), new ServerSession("access", _clock.Now.AddMinutes(15), "refresh"), _clock);
}
