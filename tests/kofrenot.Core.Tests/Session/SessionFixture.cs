using System.Net;
using Kofrenot.Core.Tests.Server;
using static Kofrenot.Core.Tests.Server.StubHandler;

namespace Kofrenot.Core.Tests.Session;

/// <summary>
/// What a session runs on, in a test: its files in a temporary folder, a server answering from
/// <see cref="Answer"/> (or offline), a UI thread whose posts run when told, and timers fired by hand.
/// </summary>
internal sealed class SessionFixture : IDisposable
{
    public const string SignedIn = """{"accessToken":"access-1","accessExpires":"2030-01-01T00:00:00Z","refreshToken":"refresh-1"}""";

    private readonly TempFolder _temp = new();

    public SessionFixture()
    {
        Files = new AppFiles(_temp.Sub("data"), _temp.Sub("program"));
        Settings = new SettingsStore(Files.Settings);
        Http = new StubHandler(request => Offline ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) : Answer(request));
    }

    public AppFiles Files { get; }

    public SettingsStore Settings { get; }

    public RingLog Log { get; } = new();

    public QueuedUiThread Ui { get; } = new();

    public ManualTimers Timers { get; } = new();

    public StubHandler Http { get; }

    public bool Offline { get; set; }

    /// <summary>The server's version, as <c>GET /v1/version</c> says it; null: a 0.1 server, which answers 404.</summary>
    public string? Version { get; set; } = "0.2";

    /// <summary>Holds each feed request until released, when set.</summary>
    public SemaphoreSlim? FeedGate { get; set; }

    public ServerSessionMemory Sessions => new(Files.ServerSession, new PlainProtector(), Log);

    public ServerConnection Connection() =>
        new(Settings, Sessions, new AccountIdentities(Files.AccountIdentity, Log), new Kofrenot.Core.Sharing.VaultOwners(Files.VaultOwners, Log), Ui, Log, Http);

    /// <summary>A server vault registered in the settings, on <see cref="StubHandler.Server"/>.</summary>
    public StoreAddress AddServerVault()
    {
        var store = new StoreAddress(StubHandler.Server, Guid.NewGuid());
        Settings.SetVaults(Settings.Vaults.Add(new RegisteredVault(new VaultLocation(Files.MirrorOf(store.Id), store))));
        return store;
    }

    public void Dispose() => _temp.Dispose();

    private HttpResponseMessage Answer(HttpRequestMessage request)
    {
        var path = request.RequestUri!.AbsolutePath;
        if (path.EndsWith("/feed", StringComparison.Ordinal))
        {
            FeedGate?.Wait();
            return Json(HttpStatusCode.OK, """{"entries":[],"more":false}""");
        }

        return path switch
        {
            "/v1/sessions" => Json(HttpStatusCode.OK, SignedIn),
            "/v1/accounts" => Json(HttpStatusCode.Accepted, """{"id":"7e1c5ad2-3c1b-4a7e-9a39-6f1b2f6a1d00"}"""),
            "/v1/version" when Version is not null => Json(HttpStatusCode.OK, $$"""{"version":"{{Version}}"}"""),
            "/v1/accounts/me/data" => Json(HttpStatusCode.OK, """{"version":0,"public":null,"private":null}"""),
            _ when path.EndsWith("/members", StringComparison.Ordinal) => Json(HttpStatusCode.OK, "[]"),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        };
    }

    private sealed class PlainProtector : ISecretProtector
    {
        public byte[] Protect(byte[] secret) => secret;

        public byte[] Unprotect(byte[] protectedSecret) => protectedSecret;
    }
}

/// <summary>A UI thread whose posted actions wait until <see cref="RunAll"/>.</summary>
internal sealed class QueuedUiThread : IUiThread
{
    private readonly Queue<Action> _posted = new();

    public int Waiting => _posted.Count;

    public void Post(Action action) => _posted.Enqueue(action);

    public void RunAll()
    {
        while (_posted.TryDequeue(out var action))
        {
            action();
        }
    }
}

/// <summary>Timers that never fire on their own: <see cref="ManualTimer.Fire"/> does.</summary>
internal sealed class ManualTimers : TimeProvider
{
    public List<ManualTimer> Created { get; } = [];

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(callback, state, dueTime, period);
        Created.Add(timer);
        return timer;
    }
}

internal sealed class ManualTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) : ITimer
{
    public TimeSpan DueTime { get; private set; } = dueTime;

    public TimeSpan Period { get; private set; } = period;

    public bool IsDisposed { get; private set; }

    public bool Change(TimeSpan dueTime, TimeSpan period)
    {
        (DueTime, Period) = (dueTime, period);
        return true;
    }

    public void Fire() => callback(state);

    public void Dispose() => IsDisposed = true;

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
