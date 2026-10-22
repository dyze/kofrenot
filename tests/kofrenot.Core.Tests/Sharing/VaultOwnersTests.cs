using System.Net;
using System.Text.Json;
using Kofrenot.Core.Sharing;
using Kofrenot.Core.Tests.Server;
using static Kofrenot.Core.Tests.Server.StubHandler;

namespace Kofrenot.Core.Tests.Sharing;

public sealed class VaultOwnersTests : IDisposable
{
    private readonly TempFolder _temp = new();
    private readonly ManualClock _clock = new();
    private readonly Guid _store = Guid.NewGuid();
    private readonly Guid _owner = Guid.NewGuid();
    private readonly Identity _identity = Identity.Create("owner", iterations: 10).Identity;

    [Fact]
    public async Task TheOwner_IsReadFromTheServerOnce_ThenKept()
    {
        var stub = Server(new AccountKey(_identity.Id, _identity.PublicKey, _identity.SigningKey));

        var owner = await Owners().OwnerOfAsync(Client(stub), _store);
        var again = await Owners().OwnerOfAsync(Client(stub), _store);

        Assert.Equal((_owner, _identity.Id), (owner?.Account, owner?.Key.Id));
        Assert.Equal(_identity.SigningKey, again?.Key.SigningKey);
        Assert.Equal(2, stub.Requests.Count);
        Assert.Equal(["/v1/stores/" + _store + "/members", "/v1/accounts/" + _owner + "/public"], stub.Requests.Select(request => request.Path));
        Assert.NotNull(Owners().Kept(_store));
    }

    [Fact]
    public async Task AnOwnerWithoutSigningKey_IsNotKept()
    {
        var stub = Server(new AccountKey(_identity.Id, _identity.PublicKey));

        Assert.Null(await Owners().OwnerOfAsync(Client(stub), _store));
        Assert.Null(Owners().Kept(_store));
    }

    [Fact]
    public async Task AnOwnerNotFound_IsNone()
    {
        var stub = new StubHandler(request => request.RequestUri!.AbsolutePath.EndsWith("/members")
            ? Json(HttpStatusCode.OK, $$"""[{"account":"{{_owner}}","role":"owner"}]""")
            : Json(HttpStatusCode.NotFound, """{"error":"no-account"}"""));

        Assert.Null(await Owners().OwnerOfAsync(Client(stub), _store));
        Assert.Null(await Owners().OwnerOfAsync(Client(new StubHandler(_ => Json(HttpStatusCode.OK, "[]"))), _store));
    }

    public void Dispose() => _temp.Dispose();

    private StubHandler Server(AccountKey key) => new(request => request.RequestUri!.AbsolutePath.EndsWith("/members")
        ? Json(HttpStatusCode.OK, $$"""[{"account":"{{Guid.NewGuid()}}","role":"writer"},{"account":"{{_owner}}","role":"owner"}]""")
        : Json(HttpStatusCode.OK, JsonSerializer.Serialize(new { @public = JsonSerializer.SerializeToUtf8Bytes(key) })));

    private VaultOwners Owners() => new(_temp.Sub("vault-owners.json"));

    private KofreClient Client(StubHandler stub) => new(stub.Client(), new ServerSession("access", _clock.Now.AddMinutes(15), "refresh"), _clock);
}
