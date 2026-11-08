using Kofrenot.Core.Fields;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Views;

namespace Kofrenot.Core.Imports;

/// <summary>A node of the other vault planned: what it writes and what the preview lists; <see cref="Missing"/> says which content of it is not here, then nothing is written.</summary>
internal sealed record PlannedNode(NodeWrite Write, IReadOnlyList<string> Details, string? Missing = null);

/// <summary>
/// What a node of another vault writes here: added with its id there, its types, colour, field values
/// (images included) and latest note or file; or, over the node found here, only what differs. Nothing
/// is cleared, no type removed. Its content must be in the other vault (downloaded there).
/// </summary>
internal sealed class VaultNodeWrites(KnowledgeGraph source, VaultContent sourceContent, string sourceName, KnowledgeGraph target, VaultContent targetContent, DateTimeOffset at)
{
    private DateTimeOffset At { get; } = at;

    private VaultContent SourceContent { get; } = sourceContent;

    private string SourceName { get; } = sourceName;

    public PlannedNode New(Node node)
    {
        var plan = new Builder(this);
        var types = source.TypeIdsOf(node.Id);
        plan.Changes.Add(new NodeAdded(Guid.NewGuid(), At, node with { Type = types.FirstOrDefault() ?? TypeCatalog.Generic.Id, Created = At }));
        // A tick apart, so the types keep their order: types given at one time are ordered by id.
        plan.Changes.AddRange(types.Skip(1).Select((type, index) => new NodeTypeAdded(Guid.NewGuid(), At.AddTicks(index + 1), node.Id, type)));
        if (source.ColorOf(node.Id) is var color && color != NodeColor.None)
        {
            plan.Changes.Add(new NodeColored(Guid.NewGuid(), At, node.Id, NodeColorNames.Of(color), []));
        }

        if (types.Where(type => type != TypeCatalog.Generic.Id).ToList() is { Count: > 0 } named)
        {
            plan.Details.Add(string.Join(", ", named.Select(source.Types.NodeTypeName)));
        }

        foreach (var value in ValuesOf(node))
        {
            plan.AddValue(value, node.Id, []);
            plan.Details.Add($"{value.Name}: {Shown(value)}");
        }

        if (LatestOf(node) is { } latest)
        {
            plan.AddContent(latest, node.Id, []);
            plan.Details.Add(latest is DatasetEdited dataset ? $"file: {dataset.File.Name}" : "note");
        }

        AddDefinition(plan, node, node.Id, []);

        return plan.Result;
    }

    public PlannedNode Update(Node node, Node found)
    {
        var plan = new Builder(this);
        if (node.Name != found.Name)
        {
            plan.Changes.Add(new NodeRenamed(Guid.NewGuid(), At, found.Id, node.Name, target.NamesOf(found.Id).Select(name => name.Id).ToList()));
            plan.Details.Add($"name: {found.Name} → {node.Name}");
        }

        var added = source.TypeIdsOf(node.Id).Where(type => type != TypeCatalog.Generic.Id && !target.HasType(found.Id, type)).ToList();
        plan.Changes.AddRange(added.Select((type, index) => new NodeTypeAdded(Guid.NewGuid(), At.AddTicks(index), found.Id, type)));
        plan.Details.AddRange(added.Select(type => $"+ type {source.Types.NodeTypeName(type)}"));
        var (color, here) = (source.ColorOf(node.Id), target.ColorOf(found.Id));
        if (color != NodeColor.None && color != here)
        {
            plan.Changes.Add(new NodeColored(Guid.NewGuid(), At, found.Id, NodeColorNames.Of(color), target.ColorsOf(found.Id).Select(version => version.Id).ToList()));
            plan.Details.Add($"colour: {NodeColorNames.Of(here) ?? "none"} → {NodeColorNames.Of(color)}");
        }

        var fields = target.FieldsOf(found.Id);
        foreach (var value in ValuesOf(node))
        {
            var current = fields.FirstOrDefault(field => field.Owner == value.Owner && field.Field == value.Field);
            if (current is { IsConflict: false } && (current.Shown.Value, current.Shown.File?.Sha256) == (value.Value, value.File?.Sha256))
            {
                continue;
            }

            plan.AddValue(value, found.Id, current?.Current.Select(version => version.Id).ToList() ?? []);
            plan.Details.Add($"{value.Name}: {(current is null ? "(empty)" : Shown(current.Shown))} → {Shown(value)}");
        }

        if (LatestOf(node) is { } latest)
        {
            UpdateContent(plan, latest, found);
        }

        AddDefinition(plan, node, found.Id, target.MappingVersionsOf(found.Id));

        return plan.Result;
    }

    // A mapping's latest definition, as a new version over those here, unless the one here is the same.
    private void AddDefinition(Builder plan, Node node, Guid nodeHere, IReadOnlyList<MappingEdited> here)
    {
        if (source.MappingVersionsOf(node.Id) is not [.., var latest] || (here is [var current] && current.Definition.Equals(latest.Definition)))
        {
            return;
        }

        plan.Changes.Add(new MappingEdited(Guid.NewGuid(), At, nodeHere, latest.Definition, here.Select(version => version.Id).ToList()));
        plan.Details.Add(here.Count == 0 ? "definition" : "definition changed");
    }

    // A new note or file version when the latest here is another; a note not here is replaced.
    private void UpdateContent(Builder plan, ContentEdited latest, Node found)
    {
        IReadOnlyList<ContentEdited> versions = latest is DatasetEdited ? target.DatasetVersionsOf(found.Id) : target.NoteVersionsOf(found.Id);
        var isSame = (latest, versions) switch
        {
            (DatasetEdited file, [DatasetEdited here]) => file.File.Sha256 == here.File.Sha256,
            (NoteEdited, [NoteEdited here]) => targetContent.NoteText(here.Id) is { } text && SourceContent.Has(latest) && text == SourceContent.NoteText(latest.Id),
            _ => false,
        };
        if (isSame)
        {
            return;
        }

        plan.AddContent(latest, found.Id, versions.Select(version => version.Id).ToList());
        plan.Details.Add((latest, versions) switch
        {
            (DatasetEdited file, [.., DatasetEdited here]) => $"file: {here.File.Name} → {file.File.Name}",
            (DatasetEdited file, _) => $"+ file: {file.File.Name}",
            (_, []) => "+ note",
            _ => "note changed",
        });
    }

    private IEnumerable<FieldSet> ValuesOf(Node node) =>
        source.FieldsOf(node.Id).Select(field => field.Shown).Where(value => value.Value is not null || value.File is not null);

    private ContentEdited? LatestOf(Node node) =>
        source.NoteVersionsOf(node.Id) is [.., var note] ? note
        : source.DatasetVersionsOf(node.Id) is [.., var dataset] ? dataset
        : null;

    private static string Shown(FieldSet value) =>
        value.File is { } file ? file.Name : value.Value is { } text ? FieldValue.Display(value.Kind, text) : "(empty)";

    // The changes and content of one node: what is downloaded there on import, or the first content not there that cannot be.
    private sealed class Builder(VaultNodeWrites writes)
    {
        private readonly List<ContentCopy> _contents = [];
        private readonly List<string> _downloads = [];
        private string? _missing;

        public List<Change> Changes { get; } = [];

        public List<string> Details { get; } = [];

        public PlannedNode Result => _missing is null
            ? new(new NodeWrite(Changes, _contents), _downloads.Count == 0 ? Details : [.. Details, $"downloaded on import: {string.Join(", ", _downloads)}"])
            : new(NodeWrite.None, Details, _missing);

        public void AddValue(FieldSet value, Guid node, IReadOnlyList<Guid> replaces)
        {
            var copied = value with { Id = Guid.NewGuid(), At = writes.At, Holder = FieldHolder.OfNode(node), Replaces = replaces, Content = null };
            if (value.Image is { } image)
            {
                Copy(image, copied.Image!, $"image “{value.Name}”");
            }

            Changes.Add(copied);
        }

        public void AddContent(ContentEdited latest, Guid node, IReadOnlyList<Guid> replaces)
        {
            ContentEdited version = latest is DatasetEdited file
                ? new DatasetEdited(Guid.NewGuid(), writes.At, node, file.File, replaces)
                : new NoteEdited(Guid.NewGuid(), writes.At, node, replaces);
            Copy(latest, version, latest is DatasetEdited ? "file" : "note");
            Changes.Add(version);
        }

        private void Copy(ContentEdited from, ContentEdited to, string what)
        {
            if (!writes.SourceContent.Has(from) && writes.SourceContent.CanDownload)
            {
                _downloads.Add(what);
            }
            else if (!writes.SourceContent.Has(from))
            {
                _missing ??= $"its {what} is not in “{writes.SourceName}” on this computer: sync it, then import again";
            }

            _contents.Add(new ContentCopy(from, to));
        }
    }
}
