using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Kofrenot.Core.Storage;

/// <summary>
/// The plain part of a vault (<c>kofrenot-vault.json</c>): its format and its id; up to format 5,
/// its master key wrapped by the pass phrase too (since 6, the keys are in <c>keys/</c>:
/// <see cref="VaultKeys"/>). Nothing in it is secret.
/// </summary>
public sealed record VaultHeader(int Format, Guid Id, WrappedKey? Key = null)
{
    /// <summary>The first format whose header holds no key.</summary>
    public const int KeysApart = 6;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>The master key of a format 5 vault, or null when the pass phrase is wrong (or the header holds no key).</summary>
    public byte[]? Unlock(string passPhrase) => Key is null ? null : KeyWrap.Unlock(Key, passPhrase, Id.ToByteArray());

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

    internal void Write(string path, bool overwrite = false) =>
        AtomicFile.Write(path, JsonSerializer.SerializeToUtf8Bytes(this, Json), overwrite);

    // JSON can leave out any field, whatever the types say.
    private static bool IsComplete([NotNullWhen(true)] VaultHeader? header) =>
        header is { Format: >= 1 }
        && header.Id != Guid.Empty
        && (header.Key is { Salt: not null, Sealed: not null, Iterations: > 0 } || (header.Key is null && header.Format >= KeysApart));
}
