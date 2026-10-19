using System.Security.Cryptography;
using System.Text.Json;
using Kofrenot.Core.Logging;

namespace Kofrenot.Core.Storage;

/// <summary>
/// Secrets kept on this computer, by vault id, in one JSON file, each protected by
/// <see cref="ISecretProtector"/>. A missing or damaged file means nothing kept.
/// </summary>
/// <param name="what">What is kept, for the log ("pass phrase").</param>
internal sealed class ProtectedStore(string path, ISecretProtector protector, string what, ILog log)
{
    private const string Category = "Vault";

    public bool Has(Guid vault) => Load().ContainsKey(Name(vault));

    /// <summary>The secret kept, or null (none, or it cannot be read on this account).</summary>
    public byte[]? Recall(Guid vault)
    {
        if (!Load().TryGetValue(Name(vault), out var protectedSecret))
        {
            return null;
        }

        try
        {
            return protector.Unprotect(protectedSecret);
        }
        catch (CryptographicException ex)
        {
            log.Warn(Category, $"The remembered {what} of vault {vault} cannot be read: {ex.Message}");
            return null;
        }
    }

    public void Remember(Guid vault, byte[] secret)
    {
        var secrets = Load();
        secrets[Name(vault)] = protector.Protect(secret);
        Save(secrets);
    }

    /// <summary>False when nothing was kept for this vault.</summary>
    public bool Forget(Guid vault)
    {
        var secrets = Load();
        if (!secrets.Remove(Name(vault)))
        {
            return false;
        }

        Save(secrets);
        return true;
    }

    private static string Name(Guid vault) => vault.ToString("N");

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
