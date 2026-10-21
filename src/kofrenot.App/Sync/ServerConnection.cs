using System.Net.Http;
using System.Windows.Threading;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Server;
using Kofrenot.Core.Settings;

namespace Kofrenot.UI.Sync;

/// <summary>
/// The server account of this kofrenot (one for all its server vaults): its client, its session
/// kept across restarts (protected by Windows), never its password, and its identity, copied on
/// this computer so server vaults unlock offline.
/// </summary>
public sealed class ServerConnection : IDisposable
{
    // Long enough to upload a large dataset; a server that cannot be reached fails much sooner.
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(30);

    private readonly SettingsStore _settings;
    private readonly ServerSessionMemory _sessions;
    private readonly ILog _log;
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private HttpClient? _http;

    public ServerConnection(SettingsStore settings, ServerSessionMemory sessions, AccountIdentities identities, ILog log)
    {
        _settings = settings;
        _sessions = sessions;
        Identities = identities;
        _log = log;
        if (settings.Server is { } account)
        {
            Connect(account.Server, sessions.Recall(account.Server));
        }
    }

    /// <summary>Raised on the UI thread when kofrenot signs in or out, its session ends, or the server is found too old.</summary>
    public event Action? Changed;

    public ServerAccount? Account => _settings.Server;

    /// <summary>The account's client; null before an account is set.</summary>
    public KofreClient? Client { get; private set; }

    public bool IsSignedIn => Client?.Session is not null;

    /// <summary>Why the server is too old for kofrenot (it keeps no account data); null when it is not, or not known yet.</summary>
    public string? TooOld { get; private set; }

    public AccountIdentities Identities { get; }

    /// <summary>The account's identity kept on this computer; null before it has one, or before it was read.</summary>
    public AccountIdentity? Identity => Account is { } account ? Identities.Kept(account.Server, account.Email) : null;

    /// <summary>Creates an account on <paramref name="server"/>; it signs in once the server's admin approves it.</summary>
    /// <exception cref="ServerException">The server refused, or cannot be reached.</exception>
    public async Task SignUpAsync(Uri server, string email, string password)
    {
        await Use(server, email).SignUpAsync(email, password);
    }

    /// <exception cref="ServerException">Wrong credentials, an account not approved yet, or a server that cannot be reached.</exception>
    public async Task SignInAsync(Uri server, string email, string password)
    {
        await Use(server, email).SignInAsync(email, password);
        await RefreshAsync();
    }

    /// <summary>Forgets the session on this computer; the server vaults stay, synced again once signed in.</summary>
    public void SignOut() => Client?.SignOut();

    /// <summary>
    /// Reads the server's version, then, when it is recent enough, the account's identity, kept for
    /// offline use. A server that cannot be reached leaves what was known.
    /// </summary>
    public async Task RefreshAsync()
    {
        if (Client is not { Session: not null } client || Account is not { } account)
        {
            return;
        }

        try
        {
            var version = await client.VersionAsync();
            var tooOld = KofreVersion.IsSupported(version) ? null : KofreVersion.TooOld(version);
            if (tooOld != TooOld)
            {
                TooOld = tooOld;
                _log.Info("Server", tooOld is null ? $"Server version {version}" : $"Server too old: {version ?? "0.1"}");
                _ = _dispatcher.BeginInvoke(() => Changed?.Invoke());
            }

            if (tooOld is null)
            {
                var before = Identity?.Version;
                if ((await Identities.RefreshAsync(client, account.Server, account.Email))?.Version != before)
                {
                    _ = _dispatcher.BeginInvoke(() => Changed?.Invoke());
                }
            }
        }
        catch (Exception ex) when (ex is ServerException or InvalidDataException)
        {
            _log.Debug("Server", $"The server's version or account data cannot be read: {ex.Message}");
        }
    }

    public void Dispose() => _http?.Dispose();

    /// <summary>The client for this account, a new one (signed out) when the server or email changed.</summary>
    private KofreClient Use(Uri server, string email)
    {
        if (Account is not { } account || account.Server != server || account.Email != email || Client is null)
        {
            Client?.SignOut();
            TooOld = null;
            _settings.SetServer(new ServerAccount(server, email));
            _log.Info("Settings", $"Server account set: {server}");
            Connect(server, session: null);
        }

        return Client!;
    }

    private void Connect(Uri server, ServerSession? session)
    {
        _http?.Dispose();
        _http = new HttpClient { BaseAddress = server, Timeout = Timeout };
        Client = new KofreClient(_http, session, log: _log);
        Client.SessionChanged += changed =>
        {
            // Raised on the thread of the call that renewed it: a sync runs in the background.
            _sessions.Remember(server, changed);
            _dispatcher.BeginInvoke(() => Changed?.Invoke());
        };
    }
}
