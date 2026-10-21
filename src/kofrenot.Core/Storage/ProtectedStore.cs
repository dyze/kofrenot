using System.Security.Cryptography;
using System.Text.Json;
using Kofrenot.Core.Logging;

namespace Kofrenot.Core.Storage;

/// <summary>
/// Secrets kept on this computer, by key (a vault id, a server), in one JSON file, each protected by
/// <see cref="ISecretProtector"/>. A missing or damaged file means nothing kept.
/// </summary>
/// <param name="what">What is kept, for the log ("pass phrase").</param>
internal sealed class ProtectedStore(string path, ISecretProtector protector, string what, ILog log)
{
    private const string Category = "Vault";

    /// <summary>The key of a vault's secret: its id.</summary>
    public static string KeyOf(Guid vault) => vault.ToString("N");

    public bool Has(string key) => Load().ContainsKey(key);

    /// <summary>The secret kept, or null (none, or it cannot be read on this account).</summary>
    public byte[]? Recall(string key)
    {
        if (!Load().TryGetValue(key, out var protectedSecret))
        {
            return null;
        }

        try
        {
            return protector.Unprotect(protectedSecret);
        }
        catch (CryptographicException ex)
        {
            log.Warn(Category, $"The remembered {what} of {key} cannot be read: {ex.Message}");
            return null;
        }
    }

    public void Remember(string key, byte[] secret)
    {
        var secrets = Load();
        secrets[key] = protector.Protect(secret);
        Save(secrets);
    }

    /// <summary>False when nothing was kept for this key.</summary>
    public bool Forget(string key)
    {
        var secrets = Load();
        if (!secrets.Remove(key))
        {
            return false;
        }

        Save(secrets);
        return true;
    }

    private Dictionary<string, byte[]> Load()
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<Dictionary<string, byte[]>>(File.ReadAllText(path)) ?? []
                : [];
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            log.Warn(Category, $"The remembered {what}s cannot be read: {ex.Message}");
            return [];
        }
    }

    private void Save(Dictionary<string, byte[]> secrets)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        AtomicFile.Write(path, JsonSerializer.SerializeToUtf8Bytes(secrets), overwrite: true);
    }
}
