using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kofrenot.Core.Logging;

namespace Kofrenot.Core.Server;

/// <summary>
/// The REST API of a Kofre server (<c>/v1</c>), for one account. Renews the access token on its
/// own, and tells <see cref="SessionChanged"/> so the new refresh token is kept. Thread-safe.
/// </summary>
/// <param name="http">Its <see cref="HttpClient.BaseAddress"/> is the server's address.</param>
public sealed class KofreClient(HttpClient http, ServerSession? session = null, TimeProvider? clock = null, ILog? log = null, ExchangeStats? exchanges = null) : IStoreFiles
{
    public const string PasswordHeader = "X-Password";
    public const string FenceHeader = "Store-Fence";

    private const string Category = "Server";
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(1);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly ILog _log = log ?? NullLog.Instance;
    private readonly SemaphoreSlim _refreshing = new(1, 1);
    private ServerSession? _session = session;

    /// <summary>Raised when the session is renewed, started or ended (null: signed out).</summary>
    public event Action<ServerSession?>? SessionChanged;

    public ServerSession? Session => _session;

    public Uri? Server => http.BaseAddress;

    /// <summary>Creates an account; it can sign in once the server's admin approves it.</summary>
    /// <exception cref="ServerException">Refused: email taken (<see cref="ServerFailure.Conflict"/>), invalid email or weak password (<see cref="ServerFailure.Failed"/>).</exception>
    public async Task SignUpAsync(string email, string password, CancellationToken cancel = default)
    {
        using var _ = await SendAsync(() => Post("v1/accounts", new { email, password }), authorized: false, cancel);
        _log.Info(Category, $"Signed up on {Server}, waiting for approval");
    }

    /// <exception cref="ServerException">Wrong credentials (<see cref="ServerFailure.SignInNeeded"/>), or an account not approved or disabled (<see cref="ServerFailure.Refused"/>).</exception>
    public async Task SignInAsync(string email, string password, CancellationToken cancel = default)
    {
        using var response = await SendAsync(() => Post("v1/sessions", new { email, password }), authorized: false, cancel);
        SetSession(await Read<ServerSession>(response, cancel));
        _log.Info(Category, $"Signed in on {Server}");
    }

    /// <summary>
    /// Changes the account's password, the current one given again. The server ends the account's
    /// other sessions and starts a new one, kept here.
    /// </summary>
    /// <exception cref="ServerException">A wrong current password (<see cref="ServerFailure.Refused"/>), a weak new one (<see cref="ServerFailure.Failed"/>), or a failed call.</exception>
    public async Task ChangePasswordAsync(string current, string password, CancellationToken cancel = default)
    {
        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Put, "v1/accounts/me/password")
            {
                Content = JsonContent.Create(new { password }, options: Json),
                Headers = { { PasswordHeader, current } },
            },
            authorized: true,
            cancel);
        SetSession(await Read<ServerSession>(response, cancel));
        _log.Info(Category, $"Password changed on {Server}, the other sessions ended");
    }

    /// <summary>Forgets the session on this device; the server's refresh token expires on its own.</summary>
    public void SignOut()
    {
        SetSession(null);
        _log.Info(Category, $"Signed out of {Server}");
    }

    /// <summary>The server's version ("0.2"); null for a server older than 0.2, which does not say.</summary>
    /// <exception cref="ServerException">The server cannot be reached, or failed.</exception>
    public async Task<string?> VersionAsync(CancellationToken cancel = default)
    {
        try
        {
            using var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, "v1/version"), authorized: false, cancel);
            return (await Read<VersionBody>(response, cancel)).Version;
        }
        catch (ServerException ex) when (ex.Failure == ServerFailure.NotFound)
        {
            return null;
        }
    }

    /// <summary>The account's data, as the server keeps it (version 0: none yet).</summary>
    public async Task<AccountData> AccountDataAsync(CancellationToken cancel = default)
    {
        using var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, "v1/accounts/me/data"), authorized: true, cancel);
        return await Read<AccountData>(response, cancel);
    }

    /// <summary>Replaces the account's data, if its version is still <paramref name="replaces"/>. Returns the new version.</summary>
    /// <exception cref="ServerException">Another device wrote it meanwhile (<see cref="ServerFailure.Conflict"/>), or the call failed.</exception>
    public async Task<long> WriteAccountDataAsync(long replaces, byte[] publicPart, byte[] privatePart, CancellationToken cancel = default)
    {
        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Put, "v1/accounts/me/data") { Content = JsonContent.Create(new AccountData(replaces, publicPart, privatePart), options: Json) },
            authorized: true,
            cancel);
        var version = (await Read<VersionNumber>(response, cancel)).Version;
        _log.Info(Category, $"Account data written, version {version}");
        return version;
    }

    /// <summary>The active account with this exact email (any case); null when there is none.</summary>
    /// <exception cref="ServerException">Too many lookups (<see cref="ServerFailure.TooManyRequests"/>), or the call failed.</exception>
    public async Task<FoundAccount?> FindAccountAsync(string email, CancellationToken cancel = default)
    {
        try
        {
            using var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, $"v1/accounts?email={Uri.EscapeDataString(email)}"), authorized: true, cancel);
            return await Read<FoundAccount>(response, cancel);
        }
        catch (ServerException ex) when (ex.Failure == ServerFailure.NotFound)
        {
            return null;
        }
    }

    /// <summary>The public part of an active account's data; null when there is no such account.</summary>
    public async Task<byte[]?> PublicPartAsync(Guid account, CancellationToken cancel = default)
    {
        try
        {
            using var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, $"v1/accounts/{account}/public"), authorized: true, cancel);
            return (await Read<PublicBody>(response, cancel)).Public;
        }
        catch (ServerException ex) when (ex.Failure == ServerFailure.NotFound)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<StoreMember>> MembersAsync(Guid store, CancellationToken cancel = default)
    {
        using var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, $"v1/stores/{store}/members"), authorized: true, cancel);
        return await Read<List<StoreMember>>(response, cancel);
    }

    /// <summary>Makes an account a member of the store (the caller its owner), or changes its role.</summary>
    public async Task SetMemberAsync(Guid store, Guid account, StoreRole role, CancellationToken cancel = default)
    {
        using var _ = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Put, $"v1/stores/{store}/members/{account}") { Content = JsonContent.Create(new { role }, options: Json) },
            authorized: true,
            cancel);
        _log.Info(Category, $"Store {store}: {account} made {role}");
    }

    public async Task<IReadOnlyList<ServerStore>> ListStoresAsync(CancellationToken cancel = default)
    {
        using var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, "v1/stores"), authorized: true, cancel);
        return await Read<List<ServerStore>>(response, cancel);
    }

    public async Task<Guid> CreateStoreAsync(CancellationToken cancel = default)
    {
        using var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Post, "v1/stores"), authorized: true, cancel);
        var id = (await Read<CreatedStore>(response, cancel)).Id;
        _log.Info(Category, $"Store {id} created");
        return id;
    }

    /// <summary>Moves a store to the server's trash; the account's password is asked again.</summary>
    public async Task DeleteStoreAsync(Guid store, string password, CancellationToken cancel = default)
    {
        using var _ = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Delete, $"v1/stores/{store}") { Headers = { { PasswordHeader, password } } },
            authorized: true,
            cancel);
        _log.Info(Category, $"Store {store} moved to the trash");
    }

    public async Task<FeedPage> FeedAsync(Guid store, long since, CancellationToken cancel = default)
    {
        using var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, $"v1/stores/{store}/feed?since={since}"), authorized: true, cancel);
        return await Read<FeedPage>(response, cancel);
    }

    public async Task DownloadAsync(Guid store, string path, Stream destination, CancellationToken cancel = default)
    {
        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Get, $"v1/stores/{store}/files/{path}"),
            authorized: true,
            cancel,
            HttpCompletionOption.ResponseHeadersRead);
        await response.Content.CopyToAsync(destination, cancel);
    }

    public async Task<UploadResult> UploadAsync(Guid store, string path, Func<Stream> open, long fence, CancellationToken cancel = default)
    {
        try
        {
            using var _ = await SendAsync(
                () => new HttpRequestMessage(HttpMethod.Put, $"v1/stores/{store}/files/{path}")
                {
                    Content = new StreamContent(open()),
                    Headers = { { FenceHeader, fence.ToString(System.Globalization.CultureInfo.InvariantCulture) } },
                },
                authorized: true,
                cancel);
            return UploadResult.Uploaded;
        }
        catch (ServerException ex) when (ex.Failure == ServerFailure.Conflict)
        {
            return ex.Code == "fence" ? UploadResult.Fenced : UploadResult.Exists;
        }
    }

    /// <summary>Removes a member from the store (the caller its owner): refused at its next call.</summary>
    public async Task RemoveMemberAsync(Guid store, Guid account, CancellationToken cancel = default)
    {
        using var _ = await SendAsync(() => new HttpRequestMessage(HttpMethod.Delete, $"v1/stores/{store}/members/{account}"), authorized: true, cancel);
        _log.Info(Category, $"Store {store}: {account} removed");
    }

    /// <summary>Leaves a store shared with this account.</summary>
    public async Task LeaveAsync(Guid store, CancellationToken cancel = default)
    {
        using var _ = await SendAsync(() => new HttpRequestMessage(HttpMethod.Delete, $"v1/stores/{store}/members/me"), authorized: true, cancel);
        _log.Info(Category, $"Store {store} left");
    }

    /// <summary>Turns the store's fence on, or raises it (the caller its owner): uploads naming less are refused.</summary>
    /// <exception cref="ServerException">Lower than its fence (<see cref="ServerFailure.Conflict"/>), or the call failed.</exception>
    public async Task SetFenceAsync(Guid store, long value, CancellationToken cancel = default)
    {
        using var _ = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Put, $"v1/stores/{store}/fence") { Content = JsonContent.Create(new { value }, options: Json) },
            authorized: true,
            cancel);
        _log.Info(Category, $"Store {store}: fence set to {value}");
    }

    private static HttpRequestMessage Post(string url, object body) => new(HttpMethod.Post, url) { Content = JsonContent.Create(body, options: Json) };

    /// <summary>
    /// Sends a request built by <paramref name="build"/>. An authorized one renews the access token
    /// first when it is about to expire, and once more if the server refuses it.
    /// </summary>
    private async Task<HttpResponseMessage> SendAsync(
        Func<HttpRequestMessage> build,
        bool authorized,
        CancellationToken cancel,
        HttpCompletionOption completion = HttpCompletionOption.ResponseContentRead)
    {
        var used = authorized ? await FreshSessionAsync(cancel) : null;
        var response = await TrySendAsync(build(), used, completion, cancel);
        if (used is not null && response.StatusCode == HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            used = await RefreshAsync(used, cancel);
            response = await TrySendAsync(build(), used, completion, cancel);
        }

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        using (response)
        {
            throw await FailureOf(response, cancel);
        }
    }

    private async Task<HttpResponseMessage> TrySendAsync(HttpRequestMessage request, ServerSession? session, HttpCompletionOption completion, CancellationToken cancel)
    {
        using (request)
        {
            if (session is not null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
            }

            var (address, up) = (request.RequestUri?.OriginalString ?? string.Empty, request.Content?.Headers.ContentLength ?? 0);
            try
            {
                var response = await http.SendAsync(request, completion, cancel);
                exchanges?.Record(address, up, response.Content.Headers.ContentLength ?? 0, failed: !response.IsSuccessStatusCode);
                return response;
            }
            catch (HttpRequestException ex)
            {
                exchanges?.Record(address, up, 0, failed: true);
                throw new ServerException(ServerFailure.Offline, inner: ex);
            }
            catch (TaskCanceledException ex) when (!cancel.IsCancellationRequested)
            {
                exchanges?.Record(address, up, 0, failed: true);
                throw new ServerException(ServerFailure.Offline, inner: ex);
            }
        }
    }

    private async Task<ServerSession> FreshSessionAsync(CancellationToken cancel)
    {
        var current = _session ?? throw new ServerException(ServerFailure.SignInNeeded);
        return current.AccessExpires - RefreshMargin > _clock.GetUtcNow() ? current : await RefreshAsync(current, cancel);
    }

    /// <summary>Renews <paramref name="stale"/>, unless another call did meanwhile; ends the session when the server refuses.</summary>
    private async Task<ServerSession> RefreshAsync(ServerSession stale, CancellationToken cancel)
    {
        await _refreshing.WaitAsync(cancel);
        try
        {
            var current = _session ?? throw new ServerException(ServerFailure.SignInNeeded);
            if (!ReferenceEquals(current, stale))
            {
                return current;
            }

            using var response = await TrySendAsync(Post("v1/sessions/refresh", new { refreshToken = stale.RefreshToken }), null, HttpCompletionOption.ResponseContentRead, cancel);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                SetSession(null);
                _log.Info(Category, $"Session on {Server} ended: sign in again");
                throw new ServerException(ServerFailure.SignInNeeded, await CodeOf(response, cancel));
            }

            if (!response.IsSuccessStatusCode)
            {
                throw await FailureOf(response, cancel);
            }

            var renewed = await Read<ServerSession>(response, cancel);
            SetSession(renewed);
            _log.Debug(Category, $"Session on {Server} renewed");
            return renewed;
        }
        finally
        {
            _refreshing.Release();
        }
    }

    private void SetSession(ServerSession? session)
    {
        _session = session;
        SessionChanged?.Invoke(session);
    }

    private static async Task<T> Read<T>(HttpResponseMessage response, CancellationToken cancel) =>
        await response.Content.ReadFromJsonAsync<T>(Json, cancel) ?? throw new ServerException(ServerFailure.Failed, "empty-response");

    private async Task<ServerException> FailureOf(HttpResponseMessage response, CancellationToken cancel)
    {
        var code = await CodeOf(response, cancel);
        var failure = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => ServerFailure.SignInNeeded,
            HttpStatusCode.Forbidden => ServerFailure.Refused,
            HttpStatusCode.NotFound => ServerFailure.NotFound,
            HttpStatusCode.Conflict => ServerFailure.Conflict,
            HttpStatusCode.RequestEntityTooLarge => ServerFailure.TooLarge,
            HttpStatusCode.TooManyRequests => ServerFailure.TooManyRequests,

            // The proxy in front answers for a server that is stopped or restarting.
            HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout => ServerFailure.Offline,
            _ => ServerFailure.Failed,
        };
        _log.Debug(Category, $"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri?.AbsolutePath} refused: {(int)response.StatusCode} {code}");
        return new ServerException(failure, code);
    }

    /// <summary>The server's error code (<c>{"error": "..."}</c>); null for a body without one.</summary>
    private static async Task<string?> CodeOf(HttpResponseMessage response, CancellationToken cancel)
    {
        try
        {
            return (await response.Content.ReadFromJsonAsync<ErrorBody>(Json, cancel))?.Error;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record CreatedStore(Guid Id);

    private sealed record VersionBody(string? Version);

    private sealed record VersionNumber(long Version);

    private sealed record PublicBody(byte[]? Public);

    private sealed record ErrorBody(string? Error);
}
