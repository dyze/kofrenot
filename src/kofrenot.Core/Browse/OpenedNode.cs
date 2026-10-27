using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kofrenot.Core.Browse;

/// <summary>A node opened on this device, and when: null when opened before kofrenot 0.10 kept the time.</summary>
[JsonConverter(typeof(OpenedNodeJson))]
public sealed record OpenedNode(Guid Node, DateTimeOffset? At);

/// <summary>Reads an <see cref="OpenedNode"/> also as it was kept before 0.10: its id alone.</summary>
internal sealed class OpenedNodeJson : JsonConverter<OpenedNode>
{
    public override OpenedNode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.TryGetGuid(out var id) ? new OpenedNode(id, null) : throw new JsonException("Not a node id.");
        }

        var entry = JsonSerializer.Deserialize<Entry>(ref reader, options) ?? throw new JsonException("No node.");
        return new OpenedNode(entry.Node, entry.At);
    }

    public override void Write(Utf8JsonWriter writer, OpenedNode value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, new Entry(value.Node, value.At), options);

    private sealed record Entry(Guid Node, DateTimeOffset? At);
}
