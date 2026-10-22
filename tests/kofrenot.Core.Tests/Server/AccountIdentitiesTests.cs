using System.Net;
using System.Text.Json;
using static Kofrenot.Core.Tests.Server.StubHandler;

namespace Kofrenot.Core.Tests.Server;

public sealed class AccountIdentitiesTests : IDisposable
{
    private const string Email = "ada@example.com";
    private const int Iterations = 10;

    private static readonly Uri Address = StubHandler.Server;

    private readonly TempFolder _temp = new();
    private readonly RingLog _log = new();
    private readonly ManualClock _clock = new();

    private string FilePath => _temp.Sub("account-identity.json");

    [Fact]
    public void NothingIsKept_AtFirst() =>
        Assert.Null(Identities().Kept(Address, Email));

    [Fact]
    public async Task ACreatedIdentity_IsWrittenOnTheServer_AndKeptHere()
    {
        var stub = new StubHandler(_ => Json(HttpStatusCode.OK, """{"version":1}"""));

        var (created, unlocked, recovery) = await Identities().CreateAsync(Client(stub), Address, Email, "account phrase", Iterations);

        Assert.Equal(1, created.Version);
        Assert.Equal((HttpMethod.Put, "/v1/accounts/me/data"), (stub.Requests[0].Method, stub.Requests[0].Path));
        var body = JsonDocument.Parse(stub.Requests[0].Body!).RootElement;
        Assert.Equal(0, body.GetProperty("version").GetInt64());
        var publicPart = JsonDocument.Parse(body.GetProperty("public").GetBytesFromBase64()).RootElement;
        Assert.Equal(created.Identity.Id, publicPart.GetProperty("Id").GetGuid());
        Assert.Equal(unlocked.PrivateKey, Identity.FromJson(body.GetProperty("private").GetBytesFromBase64()).Unlock("account phrase")?.PrivateKey);
        Assert.NotNull(created.Identity.Unlock(recovery));
        var kept = Identities().Kept(Address, Email);
        Assert.Equal((created.Identity.Id, 1L), (kept?.Identity.Id, kept?.Version));
        Assert.DoesNotContain("account phrase", File.ReadAllText(FilePath));
        Assert.Contains(_log.Entries, entry => entry.Level == LogLevel.Info && entry.Message.Contains("identity"));
    }

    [Fact]
    public async Task AnIdentityCreatedElsewhere_Conflicts()
    {
        var stub = new StubHandler(_ => Json(HttpStatusCode.Conflict, """{"error":"data-changed","version":1}"""));

        var error = await Assert.ThrowsAsync<ServerException>(() => Identities().CreateAsync(Client(stub), Address, Email, "account phrase", Iterations));

        Assert.Equal(ServerFailure.Conflict, error.Failure);
        Assert.Null(Identities().Kept(Address, Email));
    }

    [Fact]
    public async Task TheServersIdentity_IsReadAndKept_OnlyForItsAccount()
    {
        var (identity, _, _) = Identity.Create("account phrase", Iterations);
        var data = JsonSerializer.Serialize(new { version = 4, @public = new byte[] { 1 }, @private = identity.ToJson() });
        var stub = new StubHandler(_ => Json(HttpStatusCode.OK, data));

        var read = await Identities().RefreshAsync(Client(stub), Address, Email);

        Assert.Equal((identity.Id, 4L), (read?.Identity.Id, read?.Version));
        Assert.Equal(identity.Id, Identities().Kept(Address, Email)?.Identity.Id);
        Assert.Null(Identities().Kept(Address, "bob@example.com"));
        Assert.Null(Identities().Kept(new Uri("https://other.example/"), Email));
    }

    [Fact]
    public async Task AnAccountWithoutIdentity_ReadsAsNone()
    {
        var stub = new StubHandler(_ => Json(HttpStatusCode.OK, """{"version":0,"public":null,"private":null}"""));

        Assert.Null(await Identities().RefreshAsync(Client(stub), Address, Email));
    }

    [Fact]
    public async Task ANewPassPhrase_IsWrittenOverTheVersionKept()
    {
        var created = new StubHandler(_ => Json(HttpStatusCode.OK, """{"version":1}"""));
        var (identity, unlocked, recovery) = await Identities().CreateAsync(Client(created), Address, Email, "account phrase", Iterations);
        var changed = new StubHandler(_ => Json(HttpStatusCode.OK, """{"version":2}"""));

        var now = await Identities().ChangePassPhraseAsync(Client(changed), identity, unlocked, "new phrase", Iterations);

        Assert.Equal(2, now.Version);
        Assert.Contains("\"version\":1", changed.Requests[0].Body);
        Assert.Null(now.Identity.Unlock("account phrase"));
        Assert.NotNull(Identities().Kept(Address, Email)?.Identity.Unlock("new phrase"));
        Assert.NotNull(now.Identity.Unlock(recovery));
    }

    [Fact]
    public void AnAccountsKey_IsReadFromItsPublicPart_OrNone()
    {
        var id = Guid.NewGuid();

        Assert.Equal(id, AccountIdentities.KeyOf(JsonSerializer.SerializeToUtf8Bytes(new AccountKey(id, [1, 2])))?.Id);
        Assert.Null(AccountIdentities.KeyOf(null));
        Assert.Null(AccountIdentities.KeyOf("{ not json"u8.ToArray()));
        Assert.Null(AccountIdentities.KeyOf(JsonSerializer.SerializeToUtf8Bytes(new AccountKey(Guid.Empty, [1]))));
        Assert.Null(AccountIdentities.KeyOf(JsonSerializer.SerializeToUtf8Bytes(new { Id = id })));
    }

    [Fact]
    public void ADamagedCopy_IsNone_AndLogged()
    {
        Directory.CreateDirectory(_temp.Path);
        File.WriteAllText(FilePath, $$"""{"Server":"{{Address}}","Email":"{{Email}}","Version":1,"Identity":"AAAA"}""");

        Assert.Null(Identities().Kept(Address, Email));
        Assert.Contains(_log.Entries, entry => entry.Level == LogLevel.Warning);
    }

    public void Dispose() => _temp.Dispose();

    private AccountIdentities Identities() => new(FilePath, _log);

    private KofreClient Client(StubHandler stub) => new(stub.Client(), new ServerSession("access", _clock.Now.AddMinutes(15), "refresh"), _clock, _log);
}
