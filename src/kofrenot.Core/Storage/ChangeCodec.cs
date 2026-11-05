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

    /// <exception cref="InvalidDataException">Damaged, another key or id, or an unknown change, of a newer kofrenot too.</exception>
    /// <exception cref="NewerVaultFormatException">Written by a newer kofrenot, which this one may not read.</exception>
    public Change Decode(Guid id, byte[] data) =>
        DecodeWithFormat(id, data).Change ?? throw new InvalidDataException("A change of a newer kofrenot, unknown to this one");

    /// <summary>
    /// As <see cref="Decode"/>, with the vault format the change was written in and the oldest that
    /// may write to its vault; no change when a newer kofrenot wrote one this version does not know (skipped).
    /// </summary>
    /// <exception cref="InvalidDataException">Damaged, another key or id, or an unknown change of a format this kofrenot writes.</exception>
    /// <exception cref="NewerVaultFormatException">Written by a newer kofrenot, which this one may not read.</exception>
    public DecodedChange DecodeWithFormat(Guid id, byte[] data)
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

        return change is null ? throw new InvalidDataException("The change is empty") : new(change.ToChange(id), change.Format, change.OldestWriterOrFormat);
    }
}

/// <summary>A change file read: its change (null when skipped, <see cref="ChangeCodec.DecodeWithFormat"/>), its format, and the oldest format that may write to its vault.</summary>
public sealed record DecodedChange(Change? Change, int Format, int OldestWriter);
