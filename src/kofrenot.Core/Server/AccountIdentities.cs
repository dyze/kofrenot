using System.Text.Json;
using System.Text.Json.Nodes;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Storage;

namespace Kofrenot.Core.Server;

/// <summary>
/// A server account's identity, as read from the version <paramref name="Version"/> of its data, and
/// the names of its server vaults by store, each sealed for it (<see cref="AccountVaultNames"/>).
/// </summary>
public sealed record AccountIdentity(Uri Server, string Email, long Version, Identity Identity, IReadOnlyDictionary<Guid, byte[]> VaultNames)
{
    public AccountIdentity(Uri server, string email, long version, Identity identity)
        : this(server, email, version, identity, new Dictionary<Guid, byte[]>())
    {
    }

    /// <summary>The name kept for <paramref name="store"/>; null when there is none, or it cannot be read.</summary>
    public string? NameOf(Guid store, UnlockedIdentity unlocked) =>
        VaultNames.TryGetValue(store, out var sealedName) ? AccountVaultNames.Open(sealedName, unlocked, store) : null;
}

/// <summary>
/// The identity of the server account (one for all its server vaults), kept in its data on the
/// server, and copied on this device so server vaults unlock offline. Its public part is the
/// identity's id and public key; its private part the identity itself, sealed by the account's
/// pass phrase and recovery key, with the names of its vaults (<c>vaultNames</c>, ignored by a
/// kofrenot before 0.17). Each write names the version it replaces: two devices never overwrite
/// each other.
/// </summary>
public sealed class AccountIdentities(string path, ILog? log = null)
{
    private const string Category = "Server";
    private const string NamesProperty = "vaultNames";

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
            return new AccountIdentity(server, email, kept.Version, Identity.FromJson(kept.Identity), kept.VaultNames ?? []);
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

        var identity = new AccountIdentity(server, email, data.Version, Identity.FromJson(data.Private), NamesIn(data.Private));
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
        var created = new AccountIdentity(server, email, 0, identity);
        created = created with { Version = await WriteAsync(client, created, cancel) };
        Keep(created);
        _log.Info(Category, $"Server account identity {identity.Id} created");
        return (created, unlocked, recovery);
    }

    /// <summary>Seals the account's identity by another pass phrase; its recovery key stays the same.</summary>
    /// <exception cref="ServerException">Another device changed it meanwhile (<see cref="ServerFailure.Conflict"/>), or the call failed.</exception>
    public async Task<AccountIdentity> ChangePassPhraseAsync(
        KofreClient client, AccountIdentity current, UnlockedIdentity unlocked, string passPhrase, int iterations = KeyWrap.DefaultIterations, CancellationToken cancel = default)
    {
        var changed = await WrittenAsync(client, current with { Identity = current.Identity.WithPassPhrase(unlocked, passPhrase, iterations) }, cancel);
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

        var published = await WrittenAsync(client, current with { Identity = current.Identity.WithSigningKey(unlocked) }, cancel);
        _log.Info(Category, "Server account signing key published");
        return published;
    }

    /// <summary>
    /// Keeps <paramref name="name"/> as the name of the vault in <paramref name="store"/>, sealed for
    /// the account's identity (its public key is enough), or takes it out (null). Written over the
    /// latest data: when another device wrote meanwhile, it is read again, then written once more.
    /// </summary>
    /// <exception cref="ServerException">The call failed.</exception>
    /// <exception cref="InvalidDataException">The account's data read again is not an identity.</exception>
    public async Task<AccountIdentity> NameVaultAsync(KofreClient client, AccountIdentity current, Guid store, string? name, CancellationToken cancel = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            var names = new Dictionary<Guid, byte[]>(current.VaultNames);
            if (name is not null)
            {
                names[store] = AccountVaultNames.Seal(name, current.Identity, store);
            }
            else if (!names.Remove(store))
            {
                return current;
            }

            try
            {
                var named = await WrittenAsync(client, current with { VaultNames = names }, cancel);
                _log.Debug(Category, name is null ? $"Name of store {store} taken out of the account" : $"Name of store {store} kept in the account");
                return named;
            }
            catch (ServerException ex) when (ex.Failure == ServerFailure.Conflict && attempt == 1)
            {
                current = await RefreshAsync(client, current.Server, current.Email, cancel) ?? current;
            }
        }
    }

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

    /// <summary>Writes <paramref name="account"/> over the version it was read from, then keeps it here.</summary>
    private async Task<AccountIdentity> WrittenAsync(KofreClient client, AccountIdentity account, CancellationToken cancel)
    {
        var written = account with { Version = await WriteAsync(client, account, cancel) };
        Keep(written);
        return written;
    }

    private static Task<long> WriteAsync(KofreClient client, AccountIdentity account, CancellationToken cancel)
    {
        var identity = account.Identity;
        return client.WriteAccountDataAsync(account.Version, JsonSerializer.SerializeToUtf8Bytes(new AccountKey(identity.Id, identity.PublicKey, identity.SigningKey)), PrivatePart(account), cancel);
    }

    // The identity's JSON, with the vault names beside its own properties: an older kofrenot reads it as an identity.
    private static byte[] PrivatePart(AccountIdentity account)
    {
        var json = account.Identity.ToJson();
        if (account.VaultNames.Count == 0)
        {
            return json;
        }

        var root = JsonNode.Parse(json)!.AsObject();
        var names = new JsonObject();
        foreach (var (store, sealedName) in account.VaultNames)
        {
            names[store.ToString()] = Convert.ToBase64String(sealedName);
        }

        root[NamesProperty] = names;
        return JsonSerializer.SerializeToUtf8Bytes(root);
    }

    /// <summary>The vault names in the private part of an account's data; an entry that cannot be read is left out.</summary>
    private static Dictionary<Guid, byte[]> NamesIn(byte[] privatePart)
    {
        var names = new Dictionary<Guid, byte[]>();
        using var document = JsonDocument.Parse(privatePart);
        if (!document.RootElement.TryGetProperty(NamesProperty, out var property) || property.ValueKind != JsonValueKind.Object)
        {
            return names;
        }

        foreach (var entry in property.EnumerateObject())
        {
            if (Guid.TryParse(entry.Name, out var store) && entry.Value.ValueKind == JsonValueKind.String && entry.Value.TryGetBytesFromBase64(out var sealedName))
            {
                names[store] = sealedName;
            }
        }

        return names;
    }

    private void Keep(AccountIdentity identity) =>
        _file.Save(new KeptFile
        {
            Server = identity.Server,
            Email = identity.Email,
            Version = identity.Version,
            Identity = identity.Identity.ToJson(),
            VaultNames = identity.VaultNames.Count == 0 ? null : new Dictionary<Guid, byte[]>(identity.VaultNames),
        });

    private sealed class KeptFile
    {
        public Uri? Server { get; set; }

        public string? Email { get; set; }

        public long Version { get; set; }

        public byte[]? Identity { get; set; }

        public Dictionary<Guid, byte[]>? VaultNames { get; set; }
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
