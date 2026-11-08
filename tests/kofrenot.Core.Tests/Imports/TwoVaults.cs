namespace Kofrenot.Core.Tests.Imports;

/// <summary>Two graphs built by hand for the imports from a vault: "Work" (the source) and the open one (the target), with their content in memory.</summary>
internal sealed class TwoVaults
{
    public static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    public static readonly FieldDefinition Published = new("published", "Published", FieldKind.Date);

    public static readonly FieldDefinition Cover = new("cover", "Cover", FieldKind.Image);

    public ManualClock Clock { get; } = new() { Now = Start.AddHours(1) };

    public KnowledgeGraph Source { get; } = new();

    public KnowledgeGraph Target { get; } = new();

    /// <summary>The notes' texts and the files present, in both vaults: versions are never shared.</summary>
    public Dictionary<Guid, string> Notes { get; } = [];

    public HashSet<Guid> Files { get; } = [];

    /// <summary>Whether the source is a server vault: what is not here can be downloaded.</summary>
    public bool CanDownload { get; set; }

    public VaultContent Content => new(version => version is NoteEdited ? Notes.ContainsKey(version.Id) : Files.Contains(version.Id), Notes.GetValueOrDefault, CanDownload);

    public static void Apply(KnowledgeGraph graph, IEnumerable<Change> changes)
    {
        foreach (var change in changes)
        {
            graph.Apply(change);
        }
    }

    public void DefineBook(KnowledgeGraph graph, string name = "Book") =>
        graph.Apply(new NodeTypeDefined(Guid.NewGuid(), Start, "book", name, null, null, [Published, Cover], []));

    public static Node Add(KnowledgeGraph graph, string name, string type = "generic", Guid? id = null)
    {
        var node = new Node(id ?? Guid.NewGuid(), type, name, Start);
        graph.Apply(new NodeAdded(Guid.NewGuid(), Start, node));
        return node;
    }

    public static Link Link(KnowledgeGraph graph, Node from, Node to, string type = "contains")
    {
        var link = new Link(Guid.NewGuid(), type, from.Id, to.Id, Start);
        graph.Apply(new LinkAdded(Guid.NewGuid(), Start, link));
        return link;
    }

    public static FieldSet Set(KnowledgeGraph graph, FieldHolder holder, FieldDefinition field, string? value, DatasetFile? file = null, string owner = "book")
    {
        var set = new FieldSet(Guid.NewGuid(), Start, holder, owner, field.Id, field.Name, field.Kind, value, [], file);
        graph.Apply(set);
        return set;
    }

    public NoteEdited Note(KnowledgeGraph graph, Node node, string? text)
    {
        var version = new NoteEdited(Guid.NewGuid(), Start, node.Id, graph.NoteVersionsOf(node.Id).Select(note => note.Id).ToList());
        graph.Apply(version);
        if (text is not null)
        {
            Notes[version.Id] = text;
        }

        return version;
    }

    public DatasetEdited File(KnowledgeGraph graph, Node node, string name, string sha, bool isHere = true)
    {
        var version = new DatasetEdited(Guid.NewGuid(), Start, node.Id, new DatasetFile(name, 10, sha), graph.DatasetVersionsOf(node.Id).Select(file => file.Id).ToList());
        graph.Apply(version);
        if (isHere)
        {
            Files.Add(version.Id);
        }

        return version;
    }

    public VaultImportPlan Plan(IReadOnlyList<Node> hits, string? subtree = null) =>
        new VaultImportPlanner(Target, Content, Clock).Plan(Source, Content, "Work", hits, subtree);

    /// <summary>Writes the plan as chosen (all by default) into the target, its content copied as the vault does.</summary>
    public VaultImportWrite Write(VaultImportPlan plan, Func<VaultImportItem, RecordChoice>? choose = null)
    {
        var write = plan.WriteFor(choose ?? (_ => RecordChoice.Import));
        foreach (var copy in write.Contents)
        {
            if (copy.To is NoteEdited)
            {
                Notes[copy.To.Id] = Notes[copy.From.Id];
            }
            else
            {
                Files.Add(copy.To.Id);
            }
        }

        Apply(Target, write.Changes);
        return write;
    }
}
