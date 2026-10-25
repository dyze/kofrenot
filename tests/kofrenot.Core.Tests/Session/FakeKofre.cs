using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kofrenot.Core.Tests.Session;

/// <summary>
/// A Kofre server in memory, enough for the session: accounts (approved at once), their data,
/// stores with members, files, feed and fence, and the trash. Shared by the devices of a test.
/// </summary>
internal sealed class FakeKofre : HttpMessageHandler
{
    public static readonly Uri Address = new("https://kofre.test/");

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly Lock _lock = new();
    private readonly List<Account> _accounts = [];

    public List<Store> Stores { get; } = [];

    /// <summary>Every request fails as a server behind a stopped proxy would.</summary>
    public bool Offline { get; set; }

    /// <summary>What <c>GET /v1/version</c> says; null: a 0.1 server (404).</summary>
    public string? Version { get; set; } = "0.2";

    /// <summary>The paths of requests that fail as if the server erred: method and path start.</summary>
    public HashSet<string> Failing { get; } = [];

    public Account AccountOf(string email) => _accounts.Single(account => account.Email == email);

    public Store StoreOf(Guid id) => Stores.Single(store => store.Id == id);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken);
        lock (_lock)
        {
            if (Offline)
            {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }

            var path = request.RequestUri!.AbsolutePath.TrimStart('/');
            if (Failing.Any(failing => $"{request.Method} {path}".StartsWith(failing, StringComparison.Ordinal)))
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }

            return Answer(request, path.Split('/'), body);
        }
    }

    private HttpResponseMessage Answer(HttpRequestMessage request, string[] parts, byte[]? body)
    {
        var method = request.Method.Method;
        switch (parts)
        {
            case ["v1", "version"]:
                return Version is null ? Status(HttpStatusCode.NotFound) : Ok(new { version = Version });
            case ["v1", "accounts"] when method == "POST":
                return SignUp(Read<Credentials>(body));
            case ["v1", "sessions"] when method == "POST":
                return SignIn(Read<Credentials>(body));
        }

        if (Caller(request) is not { } caller)
        {
            return Error(HttpStatusCode.Unauthorized, "no-session");
        }

        return parts switch
        {
            ["v1", "accounts", "me", "data"] when method == "GET" => Ok(new { version = caller.DataVersion, @public = caller.Public, @private = caller.Private }),
            ["v1", "accounts", "me", "data"] => WriteData(caller, Read<DataBody>(body)),
            ["v1", "accounts"] => _accounts.FirstOrDefault(account => string.Equals(account.Email, Query(request, "email"), StringComparison.OrdinalIgnoreCase)) is { } found
                ? Ok(new { id = found.Id, @public = found.Public })
                : Status(HttpStatusCode.NotFound),
            ["v1", "accounts", var id, "public"] => _accounts.FirstOrDefault(account => account.Id.ToString() == id) is { } found
                ? Ok(new { @public = found.Public })
                : Status(HttpStatusCode.NotFound),
            ["v1", "stores"] when method == "GET" => Ok(Stores.Where(store => !store.IsTrashed && store.Members.ContainsKey(caller.Id))
                .Select(store => new ServerStore(store.Id, store.Members[caller.Id], store.Files.Count, store.Files.Sum(file => file.Bytes.Length), store.Fence))),
            ["v1", "stores"] => CreateStore(caller),
            ["v1", "stores", var id, ..] when StoreFor(caller, id) is null => Error(HttpStatusCode.Forbidden, "not-a-member"),
            ["v1", "stores", var id, .. var rest] => AnswerStore(request, method, caller, StoreFor(caller, id)!, rest, body),
            _ => Status(HttpStatusCode.NotFound),
        };
    }

    private HttpResponseMessage AnswerStore(HttpRequestMessage request, string method, Account caller, Store store, string[] rest, byte[]? body)
    {
        var role = store.Members[caller.Id];
        return (method, rest) switch
        {
            ("DELETE", []) when request.Headers.GetValues(KofreClient.PasswordHeader).Single() != caller.Password => Error(HttpStatusCode.Forbidden, "wrong-password"),
            ("DELETE", []) => Done(() => store.IsTrashed = true),
            ("GET", ["members"]) => Ok(store.Members.Select(member => new StoreMember(member.Key, member.Value))),
            ("PUT", ["members", var account]) when role == StoreRole.Owner => Done(() => store.Members[Guid.Parse(account)] = Read<RoleBody>(body).Role),
            ("DELETE", ["members", "me"]) => Done(() => store.Members.Remove(caller.Id)),
            ("DELETE", ["members", var account]) when role == StoreRole.Owner => Done(() => store.Members.Remove(Guid.Parse(account))),
            ("PUT", ["fence"]) when role == StoreRole.Owner => Done(() => store.Fence = Read<FenceBody>(body).Value),
            ("GET", ["feed"]) => Feed(store, long.Parse(Query(request, "since")!)),
            ("GET", ["files", .. var file]) => store.Files.FirstOrDefault(stored => stored.Path == string.Join('/', file)) is { } found
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(found.Bytes) }
                : Status(HttpStatusCode.NotFound),
            ("PUT", ["files", ..]) when role == StoreRole.Reader => Error(HttpStatusCode.Forbidden, "read-only"),
            ("PUT", ["files", .. var file]) => Upload(request, store, string.Join('/', file), body!),
            _ => Error(HttpStatusCode.Forbidden, "not-allowed"),
        };
    }

    private HttpResponseMessage SignUp(Credentials credentials)
    {
        if (_accounts.Any(account => account.Email == credentials.Email))
        {
            return Error(HttpStatusCode.Conflict, "email-taken");
        }

        var account = new Account(Guid.NewGuid(), credentials.Email, credentials.Password);
        _accounts.Add(account);
        return new HttpResponseMessage(HttpStatusCode.Accepted) { Content = JsonContent.Create(new { id = account.Id }, options: Json) };
    }

    private HttpResponseMessage SignIn(Credentials credentials) =>
        _accounts.FirstOrDefault(account => account.Email == credentials.Email && account.Password == credentials.Password) is { } account
            ? Ok(new ServerSession(TokenOf(account), DateTimeOffset.UtcNow.AddDays(1), "refresh-" + account.Id))
            : Error(HttpStatusCode.Unauthorized, "wrong-credentials");

    private static HttpResponseMessage WriteData(Account caller, DataBody data)
    {
        if (data.Version != caller.DataVersion)
        {
            return Error(HttpStatusCode.Conflict, "data-changed");
        }

        (caller.DataVersion, caller.Public, caller.Private) = (caller.DataVersion + 1, data.Public, data.Private);
        return Ok(new { version = caller.DataVersion });
    }

    private HttpResponseMessage CreateStore(Account caller)
    {
        var store = new Store(Guid.NewGuid());
        store.Members[caller.Id] = StoreRole.Owner;
        Stores.Add(store);
        return Ok(new { id = store.Id });
    }

    private static HttpResponseMessage Feed(Store store, long since)
    {
        var entries = store.Files.Select((file, index) => new FeedEntry(index + 1, file.Path, file.Bytes.Length)).Where(entry => entry.Seq > since).ToList();
        return Ok(new FeedPage(entries, More: false));
    }

    private static HttpResponseMessage Upload(HttpRequestMessage request, Store store, string path, byte[] bytes)
    {
        if (store.Files.Any(file => file.Path == path))
        {
            return Error(HttpStatusCode.Conflict, "exists");
        }

        if (store.Fence is { } fence && long.Parse(request.Headers.GetValues(KofreClient.FenceHeader).Single()) < fence)
        {
            return Error(HttpStatusCode.Conflict, "fence");
        }

        store.Files.Add((path, bytes));
        return Status(HttpStatusCode.Created);
    }

    private Account? Caller(HttpRequestMessage request) =>
        request.Headers.Authorization?.Parameter is { } token ? _accounts.FirstOrDefault(account => TokenOf(account) == token) : null;

    private Store? StoreFor(Account caller, string id) =>
        Stores.FirstOrDefault(store => store.Id.ToString() == id && !store.IsTrashed && store.Members.ContainsKey(caller.Id));

    private static string TokenOf(Account account) => "access-" + account.Id;

    private static string? Query(HttpRequestMessage request, string name) =>
        System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query)[name];

    private static T Read<T>(byte[]? body) => JsonSerializer.Deserialize<T>(body!, Json)!;

    private static HttpResponseMessage Ok(object body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body, options: Json) };

    private static HttpResponseMessage Done(Action action)
    {
        action();
        return Status(HttpStatusCode.NoContent);
    }

    private static HttpResponseMessage Status(HttpStatusCode status) => new(status);

    private static HttpResponseMessage Error(HttpStatusCode status, string code) =>
        new(status) { Content = JsonContent.Create(new { error = code }, options: Json) };

    public sealed class Account(Guid id, string email, string password)
    {
        public Guid Id => id;

        public string Email => email;

        public string Password => password;

        public long DataVersion { get; set; }

        public byte[]? Public { get; set; }

        public byte[]? Private { get; set; }
    }

    public sealed class Store(Guid id)
    {
        public Guid Id => id;

        public Dictionary<Guid, StoreRole> Members { get; } = [];

        public List<(string Path, byte[] Bytes)> Files { get; } = [];

        public long? Fence { get; set; }

        public bool IsTrashed { get; set; }
    }

    private sealed record Credentials(string Email, string Password);

    private sealed record DataBody(long Version, byte[]? Public, byte[]? Private);

    private sealed record RoleBody(StoreRole Role);

    private sealed record FenceBody(long Value);
}
