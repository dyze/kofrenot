using System.Net;
using System.Text;
using static Kofrenot.Core.Tests.Server.StubHandler;

namespace Kofrenot.Core.Tests.Server;

public sealed class KofreClientTests
{
    private const string Session = """{"accessToken":"access-2","accessExpires":"2026-10-01T12:15:00Z","refreshToken":"refresh-2"}""";

    private readonly ManualClock _clock = new();
    private readonly RingLog _log = new();

    private ServerSession Valid => new("access-1", _clock.Now.AddMinutes(15), "refresh-1");

    [Fact]
    public async Task SigningIn_KeepsTheSession_AndTellsIt()
    {
        var stub = new StubHandler(_ => Json(HttpStatusCode.OK, Session));
        var client = Client(stub, session: null);
        ServerSession? told = null;
        client.SessionChanged += session => told = session;

        await client.SignInAsync("ada@example.com", "secret password");

        Assert.Equal("access-2", client.Session!.AccessToken);
        Assert.Equal(client.Session, told);
        Assert.Equal("/v1/sessions", stub.Requests[0].Path);
        Assert.Contains("\"email\":\"ada@example.com\"", stub.Requests[0].Body);
        Assert.Contains(_log.Entries, entry => entry.Level == LogLevel.Info && entry.Message.Contains("Signed in"));
        Assert.DoesNotContain(_log.Entries, entry => entry.Message.Contains("secret password") || entry.Message.Contains("access-2"));
    }

    [Fact]
    public async Task WrongCredentials_AskToSignInAgain_WithTheServersCode()
    {
        var client = Client(new StubHandler(_ => Json(HttpStatusCode.Unauthorized, """{"error":"wrong-credentials"}""")), session: null);

        var error = await Assert.ThrowsAsync<ServerException>(() => client.SignInAsync("ada@example.com", "wrong"));

        Assert.Equal(ServerFailure.SignInNeeded, error.Failure);
        Assert.Equal("wrong-credentials", error.Code);
        Assert.Null(client.Session);
    }

    [Fact]
    public async Task SigningUp_WaitsForApproval_OrIsRefused()
    {
        var status = HttpStatusCode.Accepted;
        var client = Client(new StubHandler(_ => Json(status, status == HttpStatusCode.Accepted ? """{"id":"7e1c5ad2-3c1b-4a7e-9a39-6f1b2f6a1d00"}""" : """{"error":"email-taken"}""")), session: null);

        await client.SignUpAsync("ada@example.com", "secret password");
        status = HttpStatusCode.Conflict;
        var error = await Assert.ThrowsAsync<ServerException>(() => client.SignUpAsync("ada@example.com", "secret password"));

        Assert.Contains(_log.Entries, entry => entry.Message.Contains("waiting for approval"));
        Assert.Equal((ServerFailure.Conflict, "email-taken"), (error.Failure, error.Code));
    }

    [Theory]
    [InlineData(HttpStatusCode.BadGateway, ServerFailure.Offline)]
    [InlineData(HttpStatusCode.ServiceUnavailable, ServerFailure.Offline)]
    [InlineData(HttpStatusCode.GatewayTimeout, ServerFailure.Offline)]
    [InlineData(HttpStatusCode.InternalServerError, ServerFailure.Failed)]
    public async Task AServerDownBehindItsProxy_IsOffline(HttpStatusCode status, ServerFailure failure)
    {
        var client = Client(new StubHandler(_ => new HttpResponseMessage(status)));

        var refused = await Assert.ThrowsAsync<ServerException>(() => client.ListStoresAsync());

        Assert.Equal(failure, refused.Failure);
    }

    [Fact]
    public async Task SigningOut_ForgetsTheSession()
    {
        var client = Client(new StubHandler(_ => throw new InvalidOperationException()));
        ServerSession? told = Valid;
        client.SessionChanged += session => told = session;

        client.SignOut();

        Assert.Null(client.Session);
        Assert.Null(told);
        await Assert.ThrowsAsync<ServerException>(() => client.ListStoresAsync());
    }

    [Fact]
    public async Task Stores_AreListed_WithTheAccessToken()
    {
        var stub = new StubHandler(_ => Json(HttpStatusCode.OK, """[{"id":"7e1c5ad2-3c1b-4a7e-9a39-6f1b2f6a1d00","role":"owner","files":3,"bytes":120}]"""));

        var stores = await Client(stub).ListStoresAsync();

        Assert.Equal([new ServerStore(Guid.Parse("7e1c5ad2-3c1b-4a7e-9a39-6f1b2f6a1d00"), StoreRole.Owner, 3, 120)], stores);
        Assert.Equal("access-1", stub.Requests[0].Bearer);
    }

    [Fact]
    public async Task WithoutASession_NothingIsSent()
    {
        var stub = new StubHandler(_ => throw new InvalidOperationException());

        var error = await Assert.ThrowsAsync<ServerException>(() => Client(stub, session: null).ListStoresAsync());

        Assert.Equal(ServerFailure.SignInNeeded, error.Failure);
        Assert.Empty(stub.Requests);
    }

    [Fact]
    public async Task AnAccessTokenAboutToExpire_IsRenewedFirst()
    {
        var stub = new StubHandler(request => request.RequestUri!.AbsolutePath == "/v1/sessions/refresh" ? Json(HttpStatusCode.OK, Session) : Json(HttpStatusCode.OK, "[]"));
        var client = Client(stub);
        ServerSession? told = null;
        client.SessionChanged += session => told = session;

        _clock.Advance(TimeSpan.FromMinutes(14.5));
        await client.ListStoresAsync();

        Assert.Equal(["/v1/sessions/refresh", "/v1/stores"], stub.Requests.Select(request => request.Path));
        Assert.Contains("refresh-1", stub.Requests[0].Body);
        Assert.Equal("access-2", stub.Requests[1].Bearer);
        Assert.Equal("refresh-2", told!.RefreshToken);
    }

    [Fact]
    public async Task ARefusedAccessToken_IsRenewed_AndTheCallSentAgain()
    {
        var stub = new StubHandler(request => request.RequestUri!.AbsolutePath == "/v1/sessions/refresh" ? Json(HttpStatusCode.OK, Session)
            : request.Headers.Authorization!.Parameter == "access-1" ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
            : Json(HttpStatusCode.OK, "[]"));

        await Client(stub).ListStoresAsync();

        Assert.Equal(["/v1/stores", "/v1/sessions/refresh", "/v1/stores"], stub.Requests.Select(request => request.Path));
    }

    [Fact]
    public async Task ARefusedRefresh_EndsTheSession()
    {
        var stub = new StubHandler(request => request.RequestUri!.AbsolutePath == "/v1/sessions/refresh"
            ? Json(HttpStatusCode.Unauthorized, """{"error":"session-ended"}""")
            : new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var client = Client(stub);
        var told = 0;
        client.SessionChanged += session => told += session is null ? 1 : 0;

        var error = await Assert.ThrowsAsync<ServerException>(() => client.ListStoresAsync());

        Assert.Equal((ServerFailure.SignInNeeded, "session-ended"), (error.Failure, error.Code));
        Assert.Null(client.Session);
        Assert.Equal(1, told);
        Assert.Contains(_log.Entries, entry => entry.Level == LogLevel.Info && entry.Message.Contains("sign in again"));
    }

    [Fact]
    public async Task ARefreshTheServerFails_KeepsTheSession()
    {
        var stub = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway));
        var session = Valid;
        var client = Client(stub, session);

        _clock.Advance(TimeSpan.FromMinutes(15));
        var error = await Assert.ThrowsAsync<ServerException>(() => client.ListStoresAsync());

        Assert.Equal(ServerFailure.Offline, error.Failure);
        Assert.Null(error.Code);
        Assert.Equal(session, client.Session);
    }

    [Fact]
    public async Task EachRequest_IsCounted_WithItsBytes_OrAsFailed()
    {
        var exchanges = new ExchangeStats(clock: _clock);
        var offline = false;
        var stub = new StubHandler(_ => offline ? throw new HttpRequestException("down") : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[42]) });
        var client = new KofreClient(stub.Client(), Valid, _clock, _log, exchanges);
        var store = Guid.NewGuid();

        Assert.Equal(UploadResult.Uploaded, await client.UploadAsync(store, "content/ab/abcd.kfd", () => new MemoryStream(new byte[7]), fence: 0));
        await client.DownloadAsync(store, "content/ab/abcd.kft", new MemoryStream());
        offline = true;
        await Assert.ThrowsAsync<ServerException>(() => client.DownloadAsync(store, "content/ab/abcd.kft", new MemoryStream()));

        var day = Assert.Single(exchanges.Days);
        Assert.Equal(new ExchangeCount(1, 0, 7, 42), day.Kinds[ExchangeKind.Files]);
        Assert.Equal(new ExchangeCount(2, 1, 0, 42), day.Kinds[ExchangeKind.Thumbnails]);
    }

    [Fact]
    public async Task ASessionRenewedMeanwhile_IsNotRenewedTwice()
    {
        var refreshes = 0;
        var gate = new TaskCompletionSource();
        var stub = new AsyncStub(async request =>
        {
            if (request.RequestUri!.AbsolutePath == "/v1/sessions/refresh")
            {
                refreshes++;
                await gate.Task;
                return Json(HttpStatusCode.OK, Session);
            }

            return Json(HttpStatusCode.OK, "[]");
        });
        var client = new KofreClient(new HttpClient(stub) { BaseAddress = StubHandler.Server }, Valid, _clock, _log);

        _clock.Advance(TimeSpan.FromMinutes(15));
        var first = client.ListStoresAsync();
        var second = client.ListStoresAsync();
        gate.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(1, refreshes);
    }

    [Fact]
    public async Task AnUnreachableServer_IsOffline()
    {
        var unreachable = Client(new StubHandler(_ => throw new HttpRequestException("No route")));
        var slow = Client(new StubHandler(_ => throw new TaskCanceledException("Timeout")));

        Assert.Equal(ServerFailure.Offline, (await Assert.ThrowsAsync<ServerException>(() => unreachable.ListStoresAsync())).Failure);
        Assert.Equal(ServerFailure.Offline, (await Assert.ThrowsAsync<ServerException>(() => slow.ListStoresAsync())).Failure);
    }

    [Fact]
    public async Task ACancelledCall_IsCancelled_NotOffline()
    {
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();
        var client = Client(new StubHandler(_ => throw new TaskCanceledException()));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.ListStoresAsync(cancel.Token));
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, ServerFailure.Refused)]
    [InlineData(HttpStatusCode.NotFound, ServerFailure.NotFound)]
    [InlineData(HttpStatusCode.RequestEntityTooLarge, ServerFailure.TooLarge)]
    [InlineData(HttpStatusCode.TooManyRequests, ServerFailure.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError, ServerFailure.Failed)]
    public async Task ARefusal_SaysWhy(HttpStatusCode status, ServerFailure failure)
    {
        var client = Client(new StubHandler(_ => Json(status, """{"error":"some-code"}""")));

        var error = await Assert.ThrowsAsync<ServerException>(() => client.FeedAsync(Guid.NewGuid(), 0));

        Assert.Equal((failure, "some-code"), (error.Failure, error.Code));
        Assert.Equal($"{failure}: some-code", error.Message);
    }

    [Fact]
    public async Task AnEmptyAnswer_IsAFailure()
    {
        var client = Client(new StubHandler(_ => Json(HttpStatusCode.OK, "null")));

        var error = await Assert.ThrowsAsync<ServerException>(() => client.CreateStoreAsync());

        Assert.Equal((ServerFailure.Failed, "empty-response"), (error.Failure, error.Code));
    }

    [Fact]
    public async Task AStore_IsCreated_AndDeletedWithThePassword()
    {
        var id = Guid.NewGuid();
        var stub = new StubHandler(request => request.Method == HttpMethod.Post ? Json(HttpStatusCode.Created, $$"""{"id":"{{id}}"}""") : new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = Client(stub);

        Assert.Equal(id, await client.CreateStoreAsync());
        await client.DeleteStoreAsync(id, "secret password");

        Assert.Equal((HttpMethod.Delete, $"/v1/stores/{id}", "secret password"), (stub.Requests[1].Method, stub.Requests[1].Path, stub.Requests[1].Password));
        Assert.DoesNotContain(_log.Entries, entry => entry.Message.Contains("secret password"));
        Assert.Contains(_log.Entries, entry => entry.Level == LogLevel.Info && entry.Message.Contains("trash"));
    }

    [Fact]
    public async Task TheFeed_IsReadFromASequenceNumber()
    {
        var store = Guid.NewGuid();
        var stub = new StubHandler(_ => Json(HttpStatusCode.OK, """{"entries":[{"seq":4,"path":"changes/ab/ab.kfc","size":10}],"more":true}"""));

        var page = await Client(stub).FeedAsync(store, 3);

        Assert.Equal($"/v1/stores/{store}/feed?since=3", stub.Requests[0].Path);
        Assert.Equal([new FeedEntry(4, "changes/ab/ab.kfc", 10)], page.Entries);
        Assert.True(page.More);
    }

    [Fact]
    public async Task AFile_IsDownloaded()
    {
        var stub = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) });
        using var destination = new MemoryStream();

        await Client(stub).DownloadAsync(Guid.NewGuid(), "content/ab/ab.kfn", destination);

        Assert.Equal([1, 2, 3], destination.ToArray());
    }

    [Fact]
    public async Task AFile_IsUploaded_AndReopenedWhenSentAgain()
    {
        var opened = 0;
        static HttpResponseMessage answer(HttpRequestMessage request) =>
            request.RequestUri!.AbsolutePath == "/v1/sessions/refresh" ? Json(HttpStatusCode.OK, Session)
            : request.Headers.Authorization!.Parameter == "access-1" ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
            : new HttpResponseMessage(HttpStatusCode.Created);

        string? fence = null;
        var stub = new StubHandler(request =>
        {
            fence = request.Headers.TryGetValues(KofreClient.FenceHeader, out var values) ? values.Single() : null;
            return answer(request);
        });

        var added = await Client(stub).UploadAsync(Guid.NewGuid(), "a", () =>
        {
            opened++;
            return new MemoryStream(Encoding.UTF8.GetBytes("abc"));
        }, fence: 3);

        Assert.Equal(UploadResult.Uploaded, added);
        Assert.Equal(2, opened);
        Assert.Equal("abc", stub.Requests[^1].Body);
        Assert.Equal("3", fence);
    }

    [Theory]
    [InlineData("file-exists", UploadResult.Exists)]
    [InlineData("fence", UploadResult.Fenced)]
    public async Task AFileTheStoreHas_OrBelowItsFence_IsNotUploaded(string code, UploadResult result)
    {
        var client = Client(new StubHandler(_ => Json(HttpStatusCode.Conflict, $$"""{"error":"{{code}}"}""")));

        Assert.Equal(result, await client.UploadAsync(Guid.NewGuid(), "a", () => new MemoryStream(), fence: 0));
    }

    [Fact]
    public async Task AMemberIsRemoved_OrLeaves_AndTheFenceIsSet()
    {
        var store = Guid.NewGuid();
        var account = Guid.NewGuid();
        var stub = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = Client(stub);

        await client.RemoveMemberAsync(store, account);
        await client.LeaveAsync(store);
        await client.SetFenceAsync(store, 4);

        Assert.Equal(
            [(HttpMethod.Delete, $"/v1/stores/{store}/members/{account}"), (HttpMethod.Delete, $"/v1/stores/{store}/members/me"), (HttpMethod.Put, $"/v1/stores/{store}/fence")],
            stub.Requests.Select(request => (request.Method, request.Path)));
        Assert.Contains("\"value\":4", stub.Requests[2].Body);
    }

    [Fact]
    public async Task TheServersVersion_IsRead_WithoutSigningIn_AndNoneFromAnOlderServer()
    {
        var found = true;
        var stub = new StubHandler(_ => found ? Json(HttpStatusCode.OK, """{"version":"0.2"}""") : Json(HttpStatusCode.NotFound, "{}"));
        var client = Client(stub, session: null);

        Assert.Equal("0.2", await client.VersionAsync());
        found = false;
        Assert.Null(await client.VersionAsync());
        Assert.Null(stub.Requests[0].Bearer);
    }

    [Fact]
    public async Task AFailingServer_IsNotTakenForAnOlderOne()
    {
        var client = Client(new StubHandler(_ => Json(HttpStatusCode.InternalServerError, "{}")), session: null);

        Assert.Equal(ServerFailure.Failed, (await Assert.ThrowsAsync<ServerException>(() => client.VersionAsync())).Failure);
    }

    [Fact]
    public async Task TheAccountsData_IsRead()
    {
        var client = Client(new StubHandler(_ => Json(HttpStatusCode.OK, """{"version":3,"public":"AQ==","private":"Ag=="}""")));

        var data = await client.AccountDataAsync();

        Assert.Equal(3, data.Version);
        Assert.Equal([1], data.Public);
        Assert.Equal([2], data.Private);
    }

    [Fact]
    public async Task AChangedPassword_KeepsTheNewSession_TheCurrentOneGivenAgain()
    {
        var stub = new StubHandler(_ => Json(HttpStatusCode.OK, Session));
        var client = Client(stub);

        await client.ChangePasswordAsync("old password", "new password!");

        Assert.Equal((HttpMethod.Put, "/v1/accounts/me/password", "old password"), (stub.Requests[0].Method, stub.Requests[0].Path, stub.Requests[0].Password));
        Assert.Contains("\"password\":\"new password!\"", stub.Requests[0].Body);
        Assert.Equal("access-2", client.Session!.AccessToken);
        Assert.Contains(_log.Entries, entry => entry.Level == LogLevel.Info && entry.Message.Contains("Password changed"));
        Assert.DoesNotContain(_log.Entries, entry => entry.Message.Contains("password!") || entry.Message.Contains("old password"));
    }

    [Fact]
    public async Task AWrongCurrentPassword_IsRefused_AndTheSessionKept()
    {
        var client = Client(new StubHandler(_ => Json(HttpStatusCode.Forbidden, """{"error":"wrong-password"}""")));

        var error = await Assert.ThrowsAsync<ServerException>(() => client.ChangePasswordAsync("wrong", "new password!"));

        Assert.Equal((ServerFailure.Refused, "wrong-password"), (error.Failure, error.Code));
        Assert.Equal("access-1", client.Session!.AccessToken);
    }

    [Fact]
    public async Task AStoresMembers_AreReadAndSet()
    {
        var store = Guid.NewGuid();
        var account = Guid.NewGuid();
        var stub = new StubHandler(request => request.Method == HttpMethod.Get
            ? Json(HttpStatusCode.OK, $$"""[{"account":"{{account}}","role":"writer"}]""")
            : new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = Client(stub);

        var members = await client.MembersAsync(store);
        await client.SetMemberAsync(store, account, StoreRole.Reader);

        Assert.Equal([new StoreMember(account, StoreRole.Writer)], members);
        Assert.Equal((HttpMethod.Put, $"/v1/stores/{store}/members/{account}"), (stub.Requests[1].Method, stub.Requests[1].Path));
        Assert.Contains("\"role\":\"reader\"", stub.Requests[1].Body);
    }

    private KofreClient Client(StubHandler stub) => Client(stub, Valid);

    private KofreClient Client(StubHandler stub, ServerSession? session) => new(stub.Client(), session, _clock, _log);

    private sealed class AsyncStub(Func<HttpRequestMessage, Task<HttpResponseMessage>> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => answer(request);
    }
}
