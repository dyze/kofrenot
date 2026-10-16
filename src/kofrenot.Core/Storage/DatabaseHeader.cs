using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Kofrenot.Core.Storage;

/// <summary>
/// The plain part of a database (<c>kofrenot-db.json</c>): its format, its id and its wrapped
/// master key. Nothing in it is secret.
/// </summary>
public sealed record DatabaseHeader(int Format, Guid Id, WrappedKey Key)
{
    public const int CurrentFormat = 1;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>The master key, or null when the pass phrase is wrong.</summary>
    public byte[]? Unlock(string passPhrase) => KeyVault.Unlock(Key, passPhrase, Id.ToByteArray());

    /// <exception cref="InvalidDataException">Damaged.</exception>
    /// <exception cref="NotSupportedException">Created by a newer kofrenot.</exception>
    /// <exception cref="IOException">Cannot be read.</exception>
    public static DatabaseHeader Read(string path)
    {
        DatabaseHeader? header;
        try
        {
            // As text: copes with a byte order mark, if an editor added one.
            header = JsonSerializer.Deserialize<DatabaseHeader>(File.ReadAllText(path), Json);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The database header is damaged", ex);
        }

        if (header is { Format: > CurrentFormat })
        {
            throw new NotSupportedException($"The database has format {header.Format}: it was created by a newer kofrenot");
        }

        return IsComplete(header) ? header : throw new InvalidDataException("The database header is damaged");
    }

    internal void Write(string path) =>
        AtomicFile.Write(path, JsonSerializer.SerializeToUtf8Bytes(this, Json), overwrite: false);

    // JSON can leave out any field, whatever the types say.
    private static bool IsComplete([NotNullWhen(true)] DatabaseHeader? header) =>
        header is { Format: >= 1, Key: { Salt: not null, Sealed: not null, Iterations: > 0 } } && header.Id != Guid.Empty;
}
