using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Storage;

/// <summary>
/// Turns a change into the content of its file and back: JSON, then sealed with the master key
/// ("KFC1"), bound to the change id (the file's name), so a file renamed or copied under another
/// id no longer opens.
/// </summary>
public sealed class ChangeCodec(SecretBox box)
{
    private static ReadOnlySpan<byte> Magic => "KFC1"u8;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public byte[] Encode(Change change) =>
        box.Seal(Magic, JsonSerializer.SerializeToUtf8Bytes(ChangeData.From(change), Json), change.Id.ToByteArray());

    /// <exception cref="InvalidDataException">Damaged, another key or id, or an unknown change.</exception>
    /// <exception cref="NewerVaultFormatException">Written by a newer kofrenot.</exception>
    public Change Decode(Guid id, byte[] data)
    {
        var json = box.Open(Magic, data, id.ToByteArray());
        ChangeData? change;
        try
        {
            change = JsonSerializer.Deserialize<ChangeData>(json, Json);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The change is not valid JSON", ex);
        }

        return change?.ToChange(id) ?? throw new InvalidDataException("The change is empty");
    }

    internal sealed record ChangeData(int Format, string Op, DateTimeOffset At, Node? Node = null, Link? Link = null)
    {
        private const string NodeAddedOp = "addNode";
        private const string LinkAddedOp = "addLink";

        public static ChangeData From(Change change) => change switch
        {
            NodeAdded added => new(VaultFormat.Current, NodeAddedOp, added.At, Node: added.Node),
            LinkAdded added => new(VaultFormat.Current, LinkAddedOp, added.At, Link: added.Link),
            _ => throw new ArgumentOutOfRangeException(nameof(change), change.GetType().Name, "Unknown change"),
        };

        public Change ToChange(Guid id)
        {
            if (Format < 1)
            {
                throw new InvalidDataException($"Change format {Format} is not valid");
            }

            VaultFormat.EnsureSupported(Format);

            return Op switch
            {
                NodeAddedOp when IsComplete(Node) => new NodeAdded(id, At, Node),
                LinkAddedOp when IsComplete(Link) => new LinkAdded(id, At, Link),
                _ => throw new InvalidDataException($"Unknown or incomplete change '{Op}'"),
            };
        }

        // JSON can leave out any field, whatever the types say.
        private static bool IsComplete([NotNullWhen(true)] Node? node) =>
            node is not null && node.Id != Guid.Empty && !string.IsNullOrEmpty(node.Type) && !string.IsNullOrWhiteSpace(node.Name);

        private static bool IsComplete([NotNullWhen(true)] Link? link) =>
            link is not null && link.Id != Guid.Empty && !string.IsNullOrEmpty(link.Type) && link.From != Guid.Empty && link.To != Guid.Empty;
    }
}
