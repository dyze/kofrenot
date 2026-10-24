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

    private byte[] OwnerKey => Signing.PublicKeyOf(_owner.Unlocked);

    [Fact]
    public async Task AnAccountWithAKey_IsFound_WithItsFingerprint()
    {
        var publicPart = JsonSerializer.SerializeToUtf8Bytes(new AccountKey(_member.Identity.Id, _member.Identity.PublicKey, _member.Identity.SigningKey));
        var account = Guid.NewGuid();
        var stub = new StubHandler(_ => Json(HttpStatusCode.OK, JsonSerializer.Serialize(new { id = account, @public = publicPart })));

        var (result, candidate) = await VaultSharing.FindAsync(Client(stub), " bob@example.com ");

        Assert.Equal(Lookup.Found, result);
        Assert.Equal((account, "bob@example.com", _member.Identity.Id), (candidate!.Account, candidate.Email, candidate.Identity));
        Assert.Equal(Fingerprint.Of(_member.Identity.PublicKey, _member.Identity.SigningKey), candidate.Fingerprint);
        Assert.Equal(_member.Identity.SigningKey, candidate.As(MemberRole.Reader).SigningKey);
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
    public void Sharing_WrapsEveryKeyForTheMember_AndRecordsTheOwnerTheFirstTime_AllSigned()
    {
        var (access, vault) = OwnersVault();

        var changes = VaultSharing.Share(Folder, access, vault.Graph, Owner, Candidate, MemberRole.Writer, Now);

        Assert.Equal([MemberRole.Owner, MemberRole.Writer], changes.Cast<MemberAdded>().Select(change => change.Member.Role));
        Assert.Equal(access.Keys.CurrentKey, VaultKeys.Unlock(Folder, access.Header, _member.Unlocked, signer: new KeySigner(OwnerKey))?.Keys.CurrentKey);
        Append(vault, changes);
        Assert.Equal(MemberRole.Writer, VaultSharing.RoleOf(Members(vault), _member.Identity.Id));
        Assert.Null(VaultSharing.RoleOf(Members(vault), Guid.NewGuid()));
        Assert.Empty(VaultSharing.Share(Folder, access, vault.Graph, Owner, Candidate, MemberRole.Writer, Now));
        Append(vault, VaultSharing.Share(Folder, access, vault.Graph, Owner, Candidate, MemberRole.Reader, Now.AddMinutes(1)));
        Assert.Equal(MemberRole.Reader, VaultSharing.RoleOf(Members(vault), _member.Identity.Id));
        Assert.Equal(2, Members(vault).Count);
    }

    [Fact]
    public void TheMembers_AreReadBack_FromTheVaultsFiles()
    {
        var (access, vault) = OwnersVault();
        Append(vault, VaultSharing.Share(Folder, access, vault.Graph, Owner, Candidate, MemberRole.Reader, Now));

        var members = MemberRecords.Members(Vault.Open(Folder, access.Header, access.Keys).Graph, access.Header.Id, OwnerKey);

        var bob = Assert.Single(members, member => member.Email == "bob@example.com");
        Assert.Equal((MemberRole.Reader, _member.Identity.Id), (bob.Role, bob.Identity));
        Assert.Equal(_member.Identity.PublicKey, bob.PublicKey);
    }

    [Fact]
    public void ARecordNotSignedByTheOwner_IsIgnored()
    {
        var (access, vault) = OwnersVault();
        Append(vault, VaultSharing.Share(Folder, access, vault.Graph, Owner, Candidate, MemberRole.Reader, Now));
        var mallory = Identity.Create("mallory", Iterations);

        // A writer adds a member with its own key, and drops the reader.
        vault.Append(MemberRecords.Added(access.Header.Id, new VaultMember(Guid.NewGuid(), mallory.Identity.Id, mallory.Identity.PublicKey, "mallory@example.com", MemberRole.Writer), Now.AddMinutes(1), mallory.Unlocked));
        vault.Append(MemberRecords.Removed(access.Header.Id, MemberAccount, Now.AddMinutes(1), mallory.Unlocked));
        var tampered = MemberRecords.Added(access.Header.Id, Candidate.As(MemberRole.Reader), Now.AddMinutes(2), _owner.Unlocked) with { Member = Candidate.As(MemberRole.Writer) };
        vault.Append(tampered);

        Assert.Equal(new[] { OwnerAccount, MemberAccount }.Order(), Members(vault).Select(member => member.Account).Order());
        Assert.Equal(MemberRole.Reader, VaultSharing.RoleOf(Members(vault), _member.Identity.Id));
    }

    [Fact]
    public void ARecordOfAnotherVault_IsIgnored()
    {
        var (access, vault) = OwnersVault();

        vault.Append(MemberRecords.Added(Guid.NewGuid(), Candidate.As(MemberRole.Writer), Now, _owner.Unlocked));

        Assert.Empty(Members(vault));
    }

    [Fact]
    public void AnAccessBehind_OrWithoutIdentity_CannotShare()
    {
        var (access, vault) = OwnersVault();
        VaultKeys.Rotate(Folder, access, [(_owner.Identity.Id, _owner.Identity.PublicKey)]);

        Assert.Throws<InvalidOperationException>(() => VaultSharing.Share(Folder, access, vault.Graph, Owner, Candidate, MemberRole.Reader, Now));
    }

    [Theory]
    [InlineData(MemberRole.Reader, StoreRole.Reader)]
    [InlineData(MemberRole.Writer, StoreRole.Writer)]
    [InlineData(MemberRole.Owner, StoreRole.Owner)]
    public void ARole_IsTheStoresRole(MemberRole role, StoreRole store) =>
        Assert.Equal(store, VaultSharing.ToStoreRole(role));

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void ChangingARole_ReplacesTheMembersRecord_WithoutTouchingTheKeys()
    {
        var (access, vault) = OwnersVault();
        Append(vault, VaultSharing.Share(Folder, access, vault.Graph, Owner, Candidate, MemberRole.Writer, Now));
        var keyFiles = Directory.GetFiles(Path.Combine(Folder, VaultFolder.KeysFolder), "*", SearchOption.AllDirectories).Length;

        var changed = VaultSharing.ChangeRole(access, vault.Graph, MemberAccount, MemberRole.Reader, Now.AddMinutes(1))!;
        vault.Append(changed);

        Assert.Equal(MemberRole.Reader, VaultSharing.RoleOf(Members(vault), _member.Identity.Id));
        Assert.Equal(2, Members(vault).Count);
        Assert.Equal(keyFiles, Directory.GetFiles(Path.Combine(Folder, VaultFolder.KeysFolder), "*", SearchOption.AllDirectories).Length);
        Assert.Equal(MemberRole.Reader, MemberRecords.Members(Vault.Open(Folder, access.Header, access.Keys).Graph, access.Header.Id, OwnerKey).Single(member => member.Account == MemberAccount).Role);
        Assert.Null(VaultSharing.ChangeRole(access, vault.Graph, MemberAccount, MemberRole.Reader, Now.AddMinutes(2)));
        vault.Append(VaultSharing.ChangeRole(access, vault.Graph, MemberAccount, MemberRole.Writer, Now.AddMinutes(2))!);
        Assert.Equal(MemberRole.Writer, VaultSharing.RoleOf(Members(vault), _member.Identity.Id));
    }

    [Fact]
    public void TheOwner_ANonMember_AndTheOwnersRole_CannotBeGiven()
    {
        var (access, vault) = OwnersVault();
        Append(vault, VaultSharing.Share(Folder, access, vault.Graph, Owner, Candidate, MemberRole.Writer, Now));

        Assert.Throws<InvalidOperationException>(() => VaultSharing.ChangeRole(access, vault.Graph, OwnerAccount, MemberRole.Reader, Now));
        Assert.Throws<InvalidOperationException>(() => VaultSharing.ChangeRole(access, vault.Graph, Guid.NewGuid(), MemberRole.Reader, Now));
        Assert.Throws<InvalidOperationException>(() => VaultSharing.ChangeRole(access, vault.Graph, MemberAccount, MemberRole.Owner, Now));
        Assert.Throws<InvalidOperationException>(() => VaultSharing.ChangeRole(access with { Identity = null }, vault.Graph, MemberAccount, MemberRole.Reader, Now));
    }

    private VaultMember Owner => new(OwnerAccount, _owner.Identity.Id, _owner.Identity.PublicKey, "ada@example.com", MemberRole.Owner, _owner.Identity.SigningKey);

    private ShareCandidate Candidate => new(MemberAccount, "bob@example.com", _member.Identity.Id, _member.Identity.PublicKey, _member.Identity.SigningKey);

    private IReadOnlyList<VaultMember> Members(Vault vault) => MemberRecords.Members(vault.Graph, vault.Id, OwnerKey);

    private static void Append(Vault vault, IEnumerable<Change> changes)
    {
        foreach (var change in changes)
        {
            vault.Append(change);
        }
    }

    private (VaultAccess Access, Vault Vault) OwnersVault()
    {
        var access = VaultFolder.Create(Folder, _owner.Identity, _owner.Unlocked);
        return (access, Vault.Open(Folder, access.Header, access.Keys));
    }

    private KofreClient Client(StubHandler stub) => new(stub.Client(), new ServerSession("access", _clock.Now.AddMinutes(15), "refresh"), _clock);
}
