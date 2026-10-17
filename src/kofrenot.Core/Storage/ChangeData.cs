using System.Diagnostics.CodeAnalysis;
using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Storage;

/// <summary>
/// A change as JSON, in a change file (<see cref="ChangeCodec"/>) or in the snapshot
/// (<see cref="SnapshotStore"/>): its format, its operation, and the part that operation uses.
/// </summary>
internal sealed record ChangeData(
    int Format,
    string Op,
    DateTimeOffset At,
    Node? Node = null,
    Link? Link = null,
    ChangeData.RenameData? Rename = null,
    ChangeData.AliasData? Alias = null,
    ChangeData.PinData? Pin = null,
    List<Guid>? Removes = null)
{
    private const string NodeAddedOp = "addNode";
    private const string LinkAddedOp = "addLink";
    private const string NodeRenamedOp = "renameNode";
    private const string AliasAddedOp = "addAlias";
    private const string RemoveOp = "remove";
    private const string NodePinnedOp = "pinNode";

    public static ChangeData From(Change change) => change switch
    {
        NodeAdded added => new(VaultFormat.Current, NodeAddedOp, added.At, Node: added.Node),
        LinkAdded added => new(VaultFormat.Current, LinkAddedOp, added.At, Link: added.Link),
        NodeRenamed renamed => new(VaultFormat.Current, NodeRenamedOp, renamed.At, Rename: new RenameData(renamed.Node, renamed.Name, [.. renamed.Replaces])),
        AliasAdded alias => new(VaultFormat.Current, AliasAddedOp, alias.At, Alias: new AliasData(alias.LinkType, alias.IsReverse, alias.Phrase)),
        ChangesRemoved removal => new(VaultFormat.Current, RemoveOp, removal.At, Removes: [.. removal.Removes]),
        NodePinned pin => new(VaultFormat.Current, NodePinnedOp, pin.At, Pin: new PinData(pin.Node)),
        _ => throw new ArgumentOutOfRangeException(nameof(change), change.GetType().Name, "Unknown change"),
    };

    /// <exception cref="InvalidDataException">Not valid, or an unknown or incomplete change.</exception>
    /// <exception cref="NewerVaultFormatException">Written by a newer kofrenot.</exception>
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
            NodeRenamedOp when IsComplete(Rename) => new NodeRenamed(id, At, Rename.Node, Rename.Name!, Rename.Replaces!),
            AliasAddedOp when IsComplete(Alias) => new AliasAdded(id, At, Alias.LinkType!, Alias.Reverse, Alias.Phrase!),
            RemoveOp when Removes is { Count: > 0 } => new ChangesRemoved(id, At, Removes),
            NodePinnedOp when Pin is not null && Pin.Node != Guid.Empty => new NodePinned(id, At, Pin.Node),
            _ => throw new InvalidDataException($"Unknown or incomplete change '{Op}'"),
        };
    }

    // JSON can leave out any field, whatever the types say.
    private static bool IsComplete([NotNullWhen(true)] Node? node) =>
        node is not null && node.Id != Guid.Empty && !string.IsNullOrEmpty(node.Type) && !string.IsNullOrWhiteSpace(node.Name);

    private static bool IsComplete([NotNullWhen(true)] Link? link) =>
        link is not null && link.Id != Guid.Empty && !string.IsNullOrEmpty(link.Type) && link.From != Guid.Empty && link.To != Guid.Empty;

    private static bool IsComplete([NotNullWhen(true)] RenameData? rename) =>
        rename is { Name: not null, Replaces.Count: > 0 } && rename.Node != Guid.Empty && !string.IsNullOrWhiteSpace(rename.Name);

    private static bool IsComplete([NotNullWhen(true)] AliasData? alias) =>
        alias is not null && !string.IsNullOrEmpty(alias.LinkType) && !string.IsNullOrWhiteSpace(alias.Phrase);

    /// <summary><c>"rename": { "node": "…", "name": "…", "replaces": ["…"] }</c></summary>
    internal sealed record RenameData(Guid Node, string? Name, List<Guid>? Replaces);

    /// <summary><c>"alias": { "linkType": "contains", "reverse": true, "phrase": "is in" }</c></summary>
    internal sealed record AliasData(string? LinkType, bool Reverse, string? Phrase);

    /// <summary><c>"pin": { "node": "…" }</c></summary>
    internal sealed record PinData(Guid Node);
}
