using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;

namespace Kofrenot.Core.Datasets;

/// <summary>A file to import as a new dataset: its name, its size, and how to read it (once to describe it, once to seal it).</summary>
public sealed record ImportFile(string Name, long Size, Func<Stream> Open)
{
    /// <exception cref="IOException">The file cannot be read.</exception>
    public static ImportFile FromPath(string path) => new(Path.GetFileName(path), new FileInfo(path).Length, () => File.OpenRead(path));
}

/// <summary>A file of an import, as previewed: the dataset node it becomes, and whether that name is taken (a homonym).</summary>
public sealed record ImportRow(ImportFile File, string NodeName, bool IsHomonym)
{
    public DatasetType Type => DatasetType.Of(File.Name);
}

/// <summary>
/// What an import adds, shown before it is accepted: one dataset per file, linked under
/// <see cref="Under"/> when given; the files left out (<see cref="Skipped"/>: folders, unreadable).
/// </summary>
public sealed record ImportPreview(IReadOnlyList<ImportRow> Rows, IReadOnlyList<string> Skipped, ParentLink? Under)
{
    public int Count => Rows.Count;

    public long TotalSize => Rows.Sum(row => row.File.Size);
}

/// <summary>A dataset node added by an import, and the file its first version is read from.</summary>
public sealed record ImportedFile(Node Node, ImportFile File);

/// <summary>An import accepted: the changes adding the nodes and their links, then the files to seal, one per node.</summary>
public sealed record ImportPlan(IReadOnlyList<Change> Changes, IReadOnlyList<ImportedFile> Files);

/// <summary>
/// Turns dropped or chosen files into new datasets, without asking anything: each node is named
/// after its file, without the extension, and has no link unless dropped on a tree. The preview
/// says what will be added; nothing is before <see cref="Plan"/>.
/// </summary>
public sealed class FileImportPlanner(KnowledgeGraph graph, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    /// <summary>The preview of importing the files and folders at <paramref name="paths"/>; folders and missing files are skipped.</summary>
    public ImportPreview Preview(IEnumerable<string> paths, ParentLink? under = null)
    {
        var files = new List<ImportFile>();
        var skipped = new List<string>();
        foreach (var path in paths)
        {
            if (TryRead(path) is { } file)
            {
                files.Add(file);
            }
            else
            {
                skipped.Add(Path.GetFileName(Path.TrimEndingDirectorySeparator(path)));
            }
        }

        return Preview(files, under) with { Skipped = skipped };
    }

    /// <summary>The preview of importing <paramref name="files"/>.</summary>
    public ImportPreview Preview(IReadOnlyList<ImportFile> files, ParentLink? under = null)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var rows = files.Select(file =>
        {
            var name = NodeNameOf(file.Name);
            var key = NameKey.Of(name);
            return new ImportRow(file, name, graph.NodesNamed(name).Count > 0 || !names.Add(key));
        }).ToList();
        return new ImportPreview(rows, [], under);
    }

    /// <summary>The changes adding the preview's datasets (new ids at each call: call it once, when accepted).</summary>
    public ImportPlan Plan(ImportPreview preview)
    {
        var now = _clock.GetUtcNow();
        var changes = new List<Change>();
        var files = new List<ImportedFile>();
        foreach (var row in preview.Rows)
        {
            var node = new Node(Guid.NewGuid(), TypeCatalog.Dataset.Id, row.NodeName, now);
            changes.Add(new NodeAdded(Guid.NewGuid(), now, node));
            if (preview.Under is { } under)
            {
                changes.Add(new LinkAdded(Guid.NewGuid(), now, new Link(Guid.NewGuid(), under.LinkType, under.Parent.Id, node.Id, now)));
            }

            files.Add(new ImportedFile(node, row.File));
        }

        return new ImportPlan(changes, files);
    }

    /// <summary>The file name without its extension; the whole name when that leaves nothing (".gitignore").</summary>
    public static string NodeNameOf(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName).Trim();
        return name.Length > 0 ? name : fileName.Trim();
    }

    private static ImportFile? TryRead(string path)
    {
        try
        {
            return File.Exists(path) ? ImportFile.FromPath(path) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
