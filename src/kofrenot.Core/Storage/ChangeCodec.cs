using System.Text.Json;
using System.Text.Json.Serialization;
using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Storage;

/// <summary>
/// Turns a change into the content of its file and back: JSON (<see cref="ChangeData"/>), then
/// sealed with the vault's key (<see cref="SealKind.Change"/>), bound to the change id (the file's name), so a file
/// renamed or copied under another id no longer opens.
/// </summary>
public sealed class ChangeCodec(KeyedBox box)
{
    internal static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public byte[] Encode(Change change) =>
        box.Seal(SealKind.Change, JsonSerializer.SerializeToUtf8Bytes(ChangeData.From(change), Json), change.Id.ToByteArray());

    /// <exception cref="InvalidDataException">Damaged, another key or id, or an unknown change.</exception>
    /// <exception cref="NewerVaultFormatException">Written by a newer kofrenot.</exception>
    public Change Decode(Guid id, byte[] data) => DecodeWithFormat(id, data).Change;

    /// <summary>As <see cref="Decode"/>, with the vault format the change was written in.</summary>
    /// <exception cref="InvalidDataException">Damaged, another key or id, or an unknown change.</exception>
    /// <exception cref="NewerVaultFormatException">Written by a newer kofrenot.</exception>
    public (Change Change, int Format) DecodeWithFormat(Guid id, byte[] data)
    {
        var json = box.Open(SealKind.Change, data, id.ToByteArray());
        ChangeData? change;
        try
        {
            change = JsonSerializer.Deserialize<ChangeData>(json, Json);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The change is not valid JSON", ex);
        }

        return change is null ? throw new InvalidDataException("The change is empty") : (change.ToChange(id), change.Format);
    }
}
