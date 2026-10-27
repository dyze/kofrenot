using System.Diagnostics.CodeAnalysis;
using Kofrenot.Core.Fields;
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
    ChangeData.NoteData? Note = null,
    ChangeData.DatasetData? Dataset = null,
    ChangeData.ViewData? View = null,
    List<Guid>? Removes = null,
    ChangeData.VaultNameData? Vault = null,
    ChangeData.RetypeData? Retype = null,
    ChangeData.MemberData? Member = null,
    ChangeData.MemberRemovalData? MemberRemoval = null,
    ChangeData.LinkTypeData? LinkType = null,
    ChangeData.ColorData? Color = null,
    ChangeData.NodeTypeData? NodeType = null,
    ChangeData.AddTypeData? AddType = null,
    ChangeData.RemoveTypeData? RemoveType = null,
    ChangeData.FieldData? Field = null)
{
    private const string NodeAddedOp = "addNode";
    private const string LinkAddedOp = "addLink";
    private const string NodeRenamedOp = "renameNode";
    private const string AliasAddedOp = "addAlias";
    private const string RemoveOp = "remove";
    private const string NodePinnedOp = "pinNode";
    private const string NoteEditedOp = "editNote";
    private const string DatasetEditedOp = "editDataset";
    private const string ViewEditedOp = "editView";
    private const string VaultNamedOp = "nameVault";
    private const string NodeRetypedOp = "retypeNode";
    private const string MemberAddedOp = "addMember";
    private const string MemberRemovedOp = "removeMember";
    private const string LinkTypeDefinedOp = "defineLinkType";
    private const string NodeColoredOp = "colorNode";
    private const string NodeTypeDefinedOp = "defineNodeType";
    private const string NodeTypeAddedOp = "addNodeType";
    private const string NodeTypeRemovedOp = "removeNodeType";
    private const string FieldSetOp = "setField";

    public static ChangeData From(Change change) => change switch
    {
        NodeAdded added => new(VaultFormat.Current, NodeAddedOp, added.At, Node: added.Node),
        LinkAdded added => new(VaultFormat.Current, LinkAddedOp, added.At, Link: added.Link),
        NodeRenamed renamed => new(VaultFormat.Current, NodeRenamedOp, renamed.At, Rename: new RenameData(renamed.Node, renamed.Name, [.. renamed.Replaces])),
        AliasAdded alias => new(VaultFormat.Current, AliasAddedOp, alias.At, Alias: new AliasData(alias.LinkType, alias.IsReverse, alias.Phrase)),
        ChangesRemoved removal => new(VaultFormat.Current, RemoveOp, removal.At, Removes: [.. removal.Removes]),
        NodePinned pin => new(VaultFormat.Current, NodePinnedOp, pin.At, Pin: new PinData(pin.Node)),
        NoteEdited note => new(VaultFormat.Current, NoteEditedOp, note.At, Note: new NoteData(note.Node, [.. note.Replaces])),
        DatasetEdited dataset => new(VaultFormat.Current, DatasetEditedOp, dataset.At, Dataset: new DatasetData(dataset.Node, FileData.From(dataset.File), [.. dataset.Replaces])),
        ViewEdited view => new(VaultFormat.Current, ViewEditedOp, view.At, View: ViewData.From(view)),
        VaultNamed name => new(VaultFormat.Current, VaultNamedOp, name.At, Vault: new VaultNameData(name.Name, [.. name.Replaces])),
        NodeRetyped retype => new(VaultFormat.Current, NodeRetypedOp, retype.At, Retype: new RetypeData(retype.Node, retype.Type, [.. retype.Replaces])),
        MemberAdded added => new(VaultFormat.Current, MemberAddedOp, added.At, Member: MemberData.From(added)),
        MemberRemoved removed => new(VaultFormat.Current, MemberRemovedOp, removed.At, MemberRemoval: new MemberRemovalData(removed.Account, removed.Signature)),
        LinkTypeDefined defined => new(VaultFormat.Current, LinkTypeDefinedOp, defined.At, LinkType: new LinkTypeData(defined.LinkType, defined.Name, defined.ReverseName, [.. defined.Replaces])),
        NodeColored color => new(VaultFormat.Current, NodeColoredOp, color.At, Color: new ColorData(color.Node, color.Color, [.. color.Replaces])),
        NodeTypeDefined defined => new(VaultFormat.Current, NodeTypeDefinedOp, defined.At, NodeType: NodeTypeData.From(defined)),
        NodeTypeAdded added => new(VaultFormat.Current, NodeTypeAddedOp, added.At, AddType: new AddTypeData(added.Node, added.Type)),
        NodeTypeRemoved removed => new(VaultFormat.Current, NodeTypeRemovedOp, removed.At, RemoveType: new RemoveTypeData(removed.Node, [.. removed.Removes])),
        FieldSet field => new(VaultFormat.Current, FieldSetOp, field.At, Field: FieldData.From(field)),
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
            NoteEditedOp when Note is { Replaces: not null } && Note.Node != Guid.Empty => new NoteEdited(id, At, Note.Node, Note.Replaces),
            DatasetEditedOp when Dataset is { File: { } file, Replaces: { } replaces } && Dataset.Node != Guid.Empty && IsComplete(file)
                => new DatasetEdited(id, At, Dataset.Node, file.ToFile(), replaces),
            ViewEditedOp when View is { Replaces: { } replaces } && View.Node != Guid.Empty && View.ToDefinition() is { } definition
                => new ViewEdited(id, At, View.Node, definition, replaces),
            VaultNamedOp when Vault is { Name: { } name, Replaces: { } replaces } && !string.IsNullOrWhiteSpace(name)
                => new VaultNamed(id, At, name, replaces),
            NodeRetypedOp when Retype is { Type: { Length: > 0 } type, Replaces: { Count: > 0 } replaces } && Retype.Node != Guid.Empty
                => new NodeRetyped(id, At, Retype.Node, type, replaces),
            MemberAddedOp when Member?.ToMember() is { } member && Member.Signature is { Length: > 0 } signature => new MemberAdded(id, At, member, signature),
            MemberRemovedOp when MemberRemoval is { Signature: { Length: > 0 } signature } && MemberRemoval.Account != Guid.Empty
                => new MemberRemoved(id, At, MemberRemoval.Account, signature),
            LinkTypeDefinedOp when LinkType is { Id: { Length: > 0 } type, Name: { } name, Replaces: { } replaces }
                && !string.IsNullOrWhiteSpace(name) && LinkType.Reverse is null or { Length: > 0 }
                => new LinkTypeDefined(id, At, type, name, LinkType.Reverse, replaces),
            NodeColoredOp when Color is { Replaces: { } replaces } && Color.Node != Guid.Empty && Color.Color is null or { Length: > 0 }
                => new NodeColored(id, At, Color.Node, Color.Color, replaces),
            NodeTypeDefinedOp when NodeType?.ToChange(id, At) is { } defined => defined,
            NodeTypeAddedOp when AddType is { Type: { Length: > 0 } type } && AddType.Node != Guid.Empty => new NodeTypeAdded(id, At, AddType.Node, type),
            NodeTypeRemovedOp when RemoveType is { Removes: { Count: > 0 } removes } && RemoveType.Node != Guid.Empty
                => new NodeTypeRemoved(id, At, RemoveType.Node, removes),
            FieldSetOp when Field?.ToChange(id, At) is { } field => field,
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

    private static bool IsComplete(FileData file) =>
        file is { Name: not null, Sha256: not null }
        && !string.IsNullOrWhiteSpace(file.Name)
        && file.Size >= 0
        && file.Sha256.Length == 64 && file.Sha256.All(char.IsAsciiHexDigitLower);

    /// <summary><c>"rename": { "node": "…", "name": "…", "replaces": ["…"] }</c></summary>
    internal sealed record RenameData(Guid Node, string? Name, List<Guid>? Replaces);

    /// <summary><c>"alias": { "linkType": "contains", "reverse": true, "phrase": "is in" }</c></summary>
    internal sealed record AliasData(string? LinkType, bool Reverse, string? Phrase);

    /// <summary><c>"pin": { "node": "…" }</c></summary>
    internal sealed record PinData(Guid Node);

    /// <summary><c>"note": { "node": "…", "replaces": ["…"] }</c>; the content is in the file named by the change id.</summary>
    internal sealed record NoteData(Guid Node, List<Guid>? Replaces);

    /// <summary>
    /// <c>"dataset": { "node": "…", "file": { "name": "…", "size": 1, "sha256": "…" }, "replaces": ["…"] }</c>;
    /// the file's content is in the file named by the change id.
    /// </summary>
    internal sealed record DatasetData(Guid Node, FileData? File, List<Guid>? Replaces);

    /// <summary>
    /// <c>"view": { "node": "…", "display": "tree", "query": { "root": "…", "linkType": "contains" }, "replaces": ["…"] }</c>,
    /// or <c>"display": "flat", "query": { "nodeType": "note" }</c>.
    /// </summary>
    internal sealed record ViewData(Guid Node, string? Display, QueryData? Query, List<Guid>? Replaces)
    {
        private const string Tree = "tree";
        private const string Flat = "flat";

        public static ViewData From(ViewEdited view) => view.Definition switch
        {
            TreeDefinition tree => new(view.Node, Tree, new QueryData(tree.Root, tree.LinkType), [.. view.Replaces]),
            FlatDefinition flat => new(view.Node, Flat, new QueryData(NodeType: flat.NodeType), [.. view.Replaces]),
            _ => throw new ArgumentOutOfRangeException(nameof(view), view.Definition.GetType().Name, "Unknown view definition"),
        };

        /// <summary>The definition, or null when incomplete.</summary>
        public ViewDefinition? ToDefinition() => (Display, Query) switch
        {
            (Tree, { Root: { } root, LinkType: { } linkType }) when root != Guid.Empty && linkType.Length > 0 => new TreeDefinition(root, linkType),
            (Flat, { NodeType: { } nodeType }) when nodeType.Length > 0 => new FlatDefinition(nodeType),
            _ => null,
        };
    }

    /// <summary><c>"retype": { "node": "…", "type": "note", "replaces": ["…"] }</c></summary>
    internal sealed record RetypeData(Guid Node, string? Type, List<Guid>? Replaces);

    /// <summary><c>"color": { "node": "…", "color": "yellow", "replaces": [] }</c>; a null colour takes it away.</summary>
    internal sealed record ColorData(Guid Node, string? Color, List<Guid>? Replaces);

    /// <summary><c>"linkType": { "id": "…", "name": "wrote", "reverse": "written by", "replaces": [] }</c>; no reverse when symmetric.</summary>
    internal sealed record LinkTypeData(string? Id, string? Name, string? Reverse, List<Guid>? Replaces);

    /// <summary>
    /// <c>"nodeType": { "id": "…", "name": "Person", "icon": "Contact", "color": "blue", "fields": [{ "id": "…", "name": "Born", "kind": "date" }],
    /// "replaces": [] }</c>; a choice's field has its <c>"choices"</c>.
    /// </summary>
    internal sealed record NodeTypeData(string? Id, string? Name, string? Icon, string? Color, List<FieldDefinitionData>? Fields, List<Guid>? Replaces)
    {
        public static NodeTypeData From(NodeTypeDefined defined) =>
            new(defined.NodeType, defined.Name, defined.Icon, defined.Color, [.. defined.Fields.Select(FieldDefinitionData.From)], [.. defined.Replaces]);

        /// <summary>The change, or null when incomplete: a field incomplete, or two fields with one id.</summary>
        public NodeTypeDefined? ToChange(Guid id, DateTimeOffset at)
        {
            if (Id is not { Length: > 0 } || string.IsNullOrWhiteSpace(Name) || Fields is null || Replaces is null
                || Icon is { Length: 0 } || Color is { Length: 0 })
            {
                return null;
            }

            var fields = Fields.Select(field => field?.ToDefinition()).ToList();
            return fields.Any(field => field is null) || fields.DistinctBy(field => field!.Id).Count() < fields.Count
                ? null
                : new NodeTypeDefined(id, at, Id, Name, Icon, Color, fields!, Replaces);
        }
    }

    /// <summary><c>{ "id": "…", "name": "Format", "kind": "choice", "choices": ["Novel", "Short story"] }</c></summary>
    internal sealed record FieldDefinitionData(string? Id, string? Name, string? Kind, List<string>? Choices = null)
    {
        public static FieldDefinitionData From(FieldDefinition field) =>
            new(field.Id, field.Name, FieldKinds.NameOf(field.Kind), field.Kind == FieldKind.Choice ? [.. field.Choices] : null);

        /// <summary>The field, or null when incomplete.</summary>
        public FieldDefinition? ToDefinition() =>
            Id is { Length: > 0 } && !string.IsNullOrWhiteSpace(Name) && FieldKinds.Parse(Kind) is { } kind
            && (kind == FieldKind.Choice ? Choices is not null && Choices.All(choice => !string.IsNullOrWhiteSpace(choice)) : Choices is null)
                ? new FieldDefinition(Id, Name, kind, Choices ?? [])
                : null;
    }

    /// <summary><c>"addType": { "node": "…", "type": "…" }</c></summary>
    internal sealed record AddTypeData(Guid Node, string? Type);

    /// <summary><c>"removeType": { "node": "…", "removes": ["…"] }</c></summary>
    internal sealed record RemoveTypeData(Guid Node, List<Guid>? Removes);

    /// <summary>
    /// <c>"field": { "node": "…", "owner": "&lt;type id&gt;", "id": "…", "name": "Born", "kind": "date", "value": "1920-10-08", "replaces": [] }</c>;
    /// <c>"owner": "own"</c> for a field of the node's own, no value to clear it.
    /// </summary>
    internal sealed record FieldData(Guid Node, string? Owner, string? Id, string? Name, string? Kind, string? Value, List<Guid>? Replaces)
    {
        public static FieldData From(FieldSet field) =>
            new(field.Node, field.Owner, field.Field, field.Name, FieldKinds.NameOf(field.Kind), field.Value, [.. field.Replaces]);

        /// <summary>The change, or null when incomplete, or with a value its kind does not write.</summary>
        public FieldSet? ToChange(Guid id, DateTimeOffset at) =>
            Node != Guid.Empty && Owner is { Length: > 0 } && Id is { Length: > 0 } && !string.IsNullOrWhiteSpace(Name)
            && FieldKinds.Parse(Kind) is { } kind && Replaces is not null && (Value is null || FieldValue.IsValid(kind, Value))
                ? new FieldSet(id, at, Node, Owner, Id, Name, kind, Value, Replaces)
                : null;
    }

    /// <summary><c>"vault": { "name": "Personal", "replaces": ["…"] }</c></summary>
    internal sealed record VaultNameData(string? Name, List<Guid>? Replaces);

    /// <summary><c>"memberRemoval": { "account": "…", "signature": "…" }</c></summary>
    internal sealed record MemberRemovalData(Guid Account, byte[]? Signature);

    /// <summary><c>"member": { "account": "…", "identity": "…", "publicKey": "…", "email": "…", "role": "writer", "signingKey": "…", "signature": "…" }</c></summary>
    internal sealed record MemberData(Guid Account, Guid Identity, byte[]? PublicKey, string? Email, string? Role, byte[]? SigningKey = null, byte[]? Signature = null)
    {
        public static MemberData From(MemberAdded added) =>
            new(added.Member.Account, added.Member.Identity, added.Member.PublicKey, added.Member.Email, added.Member.Role.ToString().ToLowerInvariant(), added.Member.SigningKey, added.Signature);

        /// <summary>The member, or null when incomplete.</summary>
        public VaultMember? ToMember() =>
            Account != Guid.Empty && Identity != Guid.Empty && PublicKey is { Length: > 0 } && !string.IsNullOrWhiteSpace(Email)
            && Enum.TryParse<MemberRole>(Role, ignoreCase: true, out var role) && Enum.IsDefined(role)
                ? new VaultMember(Account, Identity, PublicKey, Email, role, SigningKey)
                : null;
    }

    /// <summary><c>"query": { "root": "…", "linkType": "contains" }</c> or <c>"query": { "nodeType": "note" }</c></summary>
    internal sealed record QueryData(Guid? Root = null, string? LinkType = null, string? NodeType = null);

    /// <summary><c>"file": { "name": "survey.csv", "size": 52430, "sha256": "9f86…08" }</c></summary>
    internal sealed record FileData(string? Name, long Size, string? Sha256)
    {
        public static FileData From(DatasetFile file) => new(file.Name, file.Size, file.Sha256);

        public DatasetFile ToFile() => new(Name!, Size, Sha256!);
    }
}
