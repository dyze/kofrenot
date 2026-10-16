using System.Security.Cryptography;
using System.Text.Json;
using Kofrenot.Core.Logging;

namespace Kofrenot.Core.Storage;

/// <summary>
/// The master keys the user asked to remember (<i>Remember pass phrase</i>), by database id, each
/// protected by <see cref="ISecretProtector"/>. The key is kept rather than the pass phrase: it
/// opens the database at once, without the slow key derivation, and stays valid when the pass
/// phrase changes. A missing or damaged file means nothing remembered.
/// </summary>
public sealed class KeyMemory(string path, ISecretProtector protector, ILog? log = null)
{
    private const string Category = "Database";

    private readonly ILog _log = log ?? NullLog.Instance;

    public bool Has(Guid database) => Load().ContainsKey(Name(database));

    /// <summary>The remembered key, or null (none, or it cannot be read on this account).</summary>
    public byte[]? Recall(Guid database)
    {
        if (!Load().TryGetValue(Name(database), out var protectedKey))
        {
            return null;
        }

        try
        {
            return protector.Unprotect(protectedKey);
        }
        catch (CryptographicException ex)
        {
            _log.Warn(Category, $"The remembered pass phrase of database {database} cannot be read: {ex.Message}");
            return null;
        }
    }

    public void Remember(Guid database, byte[] masterKey)
    {
        var keys = Load();
        keys[Name(database)] = protector.Protect(masterKey);
        Save(keys);
        _log.Info(Category, $"Pass phrase of database {database} remembered");
    }

    public void Forget(Guid database)
    {
        var keys = Load();
        if (keys.Remove(Name(database)))
        {
            Save(keys);
            _log.Info(Category, $"Remembered pass phrase of database {database} forgotten");
        }
    }

    private static string Name(Guid database) => database.ToString("N");

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
