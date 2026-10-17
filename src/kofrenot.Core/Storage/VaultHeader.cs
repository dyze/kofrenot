using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Kofrenot.Core.Storage;

/// <summary>
/// The plain part of a vault (<c>kofrenot-vault.json</c>): its format, its id and its wrapped
/// master key. Nothing in it is secret.
/// </summary>
public sealed record VaultHeader(int Format, Guid Id, WrappedKey Key)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>The master key, or null when the pass phrase is wrong.</summary>
    public byte[]? Unlock(string passPhrase) => KeyWrap.Unlock(Key, passPhrase, Id.ToByteArray());

    /// <exception cref="InvalidDataException">Damaged.</exception>
    /// <exception cref="NewerVaultFormatException">Of a format newer than this kofrenot reads.</exception>
    /// <exception cref="IOException">Cannot be read.</exception>
    public static VaultHeader Read(string path)
    {
        VaultHeader? header;
        try
        {
            // As text: copes with a byte order mark, if an editor added one.
            header = JsonSerializer.Deserialize<VaultHeader>(File.ReadAllText(path), Json);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The vault header is damaged", ex);
        }

        if (header is not null)
        {
            VaultFormat.EnsureSupported(header.Format);
        }

        return IsComplete(header) ? header : throw new InvalidDataException("The vault header is damaged");
    }

    internal void Write(string path) =>
        AtomicFile.Write(path, JsonSerializer.SerializeToUtf8Bytes(this, Json), overwrite: false);

    // JSON can leave out any field, whatever the types say.
    private static bool IsComplete([NotNullWhen(true)] VaultHeader? header) =>
        header is { Format: >= 1, Key: { Salt: not null, Sealed: not null, Iterations: > 0 } } && header.Id != Guid.Empty;
}
