using System.Text.Json;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Storage;

namespace Kofrenot.Core.Server;

/// <summary>A server account's identity, as read from the version <paramref name="Version"/> of its data.</summary>
public sealed record AccountIdentity(Uri Server, string Email, long Version, Identity Identity);

/// <summary>
/// The identity of the server account (one for all its server vaults), kept in its data on the
/// server, and copied on this device so server vaults unlock offline. Its public part is the
/// identity's id and public key; its private part the identity itself, sealed by the account's
/// pass phrase and recovery key. Each write names the version it replaces: two devices never
/// overwrite each other.
/// </summary>
public sealed class AccountIdentities(string path, ILog? log = null)
{
    private const string Category = "Server";

    private readonly ILog _log = log ?? NullLog.Instance;
    private readonly LocalJsonFile<KeptFile> _file = new(path, Category, "server account identity", log);

    /// <summary>The identity kept on this device for this account; null when none is.</summary>
    public AccountIdentity? Kept(Uri server, string email)
    {
        var kept = _file.Load();
        if (kept.Server != server || kept.Email != email || kept.Identity is null)
        {
            return null;
        }

        try
        {
            return new AccountIdentity(server, email, kept.Version, Identity.FromJson(kept.Identity));
        }
        catch (InvalidDataException ex)
        {
            _log.Warn(Category, $"The server account identity kept here is damaged: {ex.Message}");
            return null;
        }
    }

    /// <summary>Reads the account's identity from the server and keeps it; null when the account has none yet.</summary>
    /// <exception cref="ServerException">The call failed.</exception>
    /// <exception cref="InvalidDataException">The account's data is not an identity.</exception>
    public async Task<AccountIdentity?> RefreshAsync(KofreClient client, Uri server, string email, CancellationToken cancel = default)
    {
        var data = await client.AccountDataAsync(cancel);
        if (data.Private is null)
        {
            return null;
        }

        var identity = new AccountIdentity(server, email, data.Version, Identity.FromJson(data.Private));
        Keep(identity);
        return identity;
    }

    /// <summary>
    /// Gives the account its identity, sealed by <paramref name="passPhrase"/>. Returns it unlocked,
    /// with its recovery key to show once.
    /// </summary>
    /// <exception cref="ServerException">Another device gave it one meanwhile (<see cref="ServerFailure.Conflict"/>), or the call failed.</exception>
    public async Task<(AccountIdentity Identity, UnlockedIdentity Unlocked, RecoveryKey Recovery)> CreateAsync(
        KofreClient client, Uri server, string email, string passPhrase, int iterations = KeyWrap.DefaultIterations, CancellationToken cancel = default)
    {
        var (identity, unlocked, recovery) = Identity.Create(passPhrase, iterations);
        var version = await WriteAsync(client, replaces: 0, identity, cancel);
        var created = new AccountIdentity(server, email, version, identity);
        Keep(created);
        _log.Info(Category, $"Server account identity {identity.Id} created");
        return (created, unlocked, recovery);
    }

    /// <summary>Seals the account's identity by another pass phrase; its recovery key stays the same.</summary>
    /// <exception cref="ServerException">Another device changed it meanwhile (<see cref="ServerFailure.Conflict"/>), or the call failed.</exception>
    public async Task<AccountIdentity> ChangePassPhraseAsync(
        KofreClient client, AccountIdentity current, UnlockedIdentity unlocked, string passPhrase, int iterations = KeyWrap.DefaultIterations, CancellationToken cancel = default)
    {
        var identity = current.Identity.WithPassPhrase(unlocked, passPhrase, iterations);
        var changed = current with { Version = await WriteAsync(client, current.Version, identity, cancel), Identity = identity };
        Keep(changed);
        _log.Info(Category, "Server account pass phrase changed");
        return changed;
    }

    /// <summary>
    /// Adds the public part of the signing key to an identity made before it had one, on the
    /// server and here: its owner then signs the keys and members of the vaults it shares.
    /// </summary>
    /// <exception cref="ServerException">Another device changed it meanwhile (<see cref="ServerFailure.Conflict"/>), or the call failed.</exception>
    public async Task<AccountIdentity> PublishSigningKeyAsync(KofreClient client, AccountIdentity current, UnlockedIdentity unlocked, CancellationToken cancel = default)
    {
        if (current.Identity.SigningKey is not null)
        {
            return current;
        }

        var identity = current.Identity.WithSigningKey(unlocked);
        var published = current with { Version = await WriteAsync(client, current.Version, identity, cancel), Identity = identity };
        Keep(published);
        _log.Info(Category, "Server account signing key published");
        return published;
    }

    private static Task<long> WriteAsync(KofreClient client, long replaces, Identity identity, CancellationToken cancel) =>
        client.WriteAccountDataAsync(replaces, JsonSerializer.SerializeToUtf8Bytes(new AccountKey(identity.Id, identity.PublicKey, identity.SigningKey)), identity.ToJson(), cancel);

    private void Keep(AccountIdentity identity) =>
        _file.Save(new KeptFile { Server = identity.Server, Email = identity.Email, Version = identity.Version, Identity = identity.Identity.ToJson() });

    /// <summary>The identity in the public part of an account's data (another account's, found to share a vault with); null when there is none.</summary>
    public static AccountKey? KeyOf(byte[]? publicPart)
    {
        try
        {
            return publicPart is null ? null
                : JsonSerializer.Deserialize<AccountKey>(publicPart) is { PublicKey.Length: > 0 } key && key.Id != Guid.Empty ? key
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed class KeptFile
    {
        public Uri? Server { get; set; }

        public string? Email { get; set; }

        public long Version { get; set; }

        public byte[]? Identity { get; set; }
    }
}

/// <summary>
/// The public part of a server account's data: its identity's id and public key, what another
/// account needs to share a vault with it.
/// </summary>
public sealed record AccountKey(Guid Id, byte[] PublicKey, byte[]? SigningKey = null)
{
    public string Fingerprint => Storage.Fingerprint.Of(PublicKey, SigningKey);
}
