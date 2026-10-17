using System.Security.Cryptography;
using System.Text.Json;
using Kofrenot.Core.Logging;

namespace Kofrenot.Core.Storage;

/// <summary>
/// The master keys the user asked to remember (<i>Remember pass phrase</i>), by vault id, each
/// protected by <see cref="ISecretProtector"/>. The key is kept rather than the pass phrase: it
/// opens the vault at once, without the slow key derivation, and stays valid when the pass
/// phrase changes. A missing or damaged file means nothing remembered.
/// </summary>
public sealed class KeyMemory(string path, ISecretProtector protector, ILog? log = null)
{
    private const string Category = "Vault";

    private readonly ILog _log = log ?? NullLog.Instance;

    public bool Has(Guid vault) => Load().ContainsKey(Name(vault));

    /// <summary>The remembered key, or null (none, or it cannot be read on this account).</summary>
    public byte[]? Recall(Guid vault)
    {
        if (!Load().TryGetValue(Name(vault), out var protectedKey))
        {
            return null;
        }

        try
        {
            return protector.Unprotect(protectedKey);
        }
        catch (CryptographicException ex)
        {
            _log.Warn(Category, $"The remembered pass phrase of vault {vault} cannot be read: {ex.Message}");
            return null;
        }
    }

    public void Remember(Guid vault, byte[] masterKey)
    {
        var keys = Load();
        keys[Name(vault)] = protector.Protect(masterKey);
        Save(keys);
        _log.Info(Category, $"Pass phrase of vault {vault} remembered");
    }

    public void Forget(Guid vault)
    {
        var keys = Load();
        if (keys.Remove(Name(vault)))
        {
            Save(keys);
            _log.Info(Category, $"Remembered pass phrase of vault {vault} forgotten");
        }
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
            _log.Warn(Category, $"The remembered pass phrases cannot be read: {ex.Message}");
            return [];
        }
    }

    private void Save(Dictionary<string, byte[]> keys)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        AtomicFile.Write(path, JsonSerializer.SerializeToUtf8Bytes(keys), overwrite: true);
    }
}
