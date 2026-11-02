using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;

namespace Kofrenot.Core.Datasets;

/// <summary>A file to import as a new dataset: its name, its size, and how to read it (once to describe it, once to seal it).</summary>
public sealed record ImportFile(string Name, long Size, Func<Stream> Open)
{
    /// <exception cref="IOException">The file cannot be read.</exception>
    public static ImportFile FromPath(string path) => new(Path.GetFileName(path), new FileInfo(path).Length, () => File.OpenRead(path));
}

/// <summary>
/// An item of an import, as previewed. A file becomes a dataset, or the new version of
/// <see cref="Existing"/> (a dataset of that name under the same parent); a folder (no
/// <see cref="File"/>) a generic node, or <see cref="Existing"/> (a node of that name under the same
/// parent) holds its content. <see cref="IsHomonym"/>: a new node whose name is taken. <see cref="Folder"/>
/// is the folder row it is in, null at the top.
/// </summary>
public sealed record ImportRow(ImportFile? File, string NodeName, bool IsHomonym, ImportRow? Folder = null, Node? Existing = null)
{
    public bool IsFolder => File is null;

    /// <summary>A file imported as the new version of an existing dataset.</summary>
    public bool IsUpdate => !IsFolder && Existing is not null;

    /// <summary>The folders above it: 0 at the top.</summary>
    public int Depth => Folder is null ? 0 : Folder.Depth + 1;

    /// <summary>The dataset's type; null for a folder.</summary>
    public DatasetType? Type => File is null ? null : DatasetType.Of(File.Name);

    /// <summary>The dataset's type name ("PDF file"), or "Folder".</summary>
    public string TypeName => Type?.Name ?? "Folder";

    /// <summary>The file's size; 0 for a folder.</summary>
    public long Size => File?.Size ?? 0;
}

/// <summary>
/// What an import adds, shown before it is accepted: one dataset per new file, one node per new
/// folder, each linked under its folder's node, the top ones under <see cref="Under"/> when given;
/// the datasets updated; the items left out (<see cref="Skipped"/>: missing, unreadable, folder links).
/// </summary>
public sealed record ImportPreview(IReadOnlyList<ImportRow> Rows, IReadOnlyList<string> Skipped, ParentLink? Under)
{
    /// <summary>What is written: files and folders added, datasets updated.</summary>
    public int Count => FileCount + FolderCount + UpdateCount;

    public int FileCount => Rows.Count(row => !row.IsFolder && row.Existing is null);

    public int FolderCount => Rows.Count(row => row.IsFolder && row.Existing is null);

    public int UpdateCount => Rows.Count(row => row.IsUpdate);

    public long TotalSize => Rows.Sum(row => row.Size);

    /// <summary>"Add 2 files · 3.4 MB", "Add 3 files, 1 folder, update 2 files · 12 KB", "Update 1 file · 5 bytes", or "Nothing to add".</summary>
    public string Heading
    {
        get
        {
            var added = string.Join(", ", new[] { Counted(FileCount, "file"), Counted(FolderCount, "folder") }.Where(count => count.Length > 0));
            var updated = Counted(UpdateCount, "file");
            var said = (added.Length > 0, updated.Length > 0) switch
            {
                (true, true) => $"Add {added}, update {updated}",
                (true, false) => $"Add {added}",
                (false, true) => $"Update {updated}",
                _ => string.Empty,
            };
            return said.Length == 0 ? "Nothing to add" : $"{said} · {ByteSize.Format(TotalSize)}";
        }
    }

    private static string Counted(int count, string what) => count switch
    {
        0 => string.Empty,
        1 => $"1 {what}",
        _ => $"{count} {what}s",
    };
}

/// <summary>A dataset node of an import (added, or updated), and the file its new version is read from.</summary>
public sealed record ImportedFile(Node Node, ImportFile File);

/// <summary>
/// An import accepted: the changes adding the nodes and their links, then the files to seal: the
/// first version of each new dataset (<see cref="Files"/>), a new version of each existing one (<see cref="Updates"/>).
/// </summary>
public sealed record ImportPlan(IReadOnlyList<Change> Changes, IReadOnlyList<ImportedFile> Files, IReadOnlyList<ImportedFile> Updates);

/// <summary>
/// Turns dropped or chosen files and folders into nodes, without asking anything: a dataset per
/// file, named after it without the extension; a generic node per folder, named after it. Inside a
/// folder, each item is linked under the folder's node by the tree's link type (<c>contains</c>
/// without a tree); the top items have no link unless dropped on a tree. Under a node already
/// there, an item of the same name updates it (<see cref="ImportRow.Existing"/>). The preview says
/// what will be written; nothing is before <see cref="Plan"/>.
/// </summary>
public sealed class FileImportPlanner(KnowledgeGraph graph, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    /// <summary>
    /// The preview of importing the files and folders at <paramref name="paths"/>, each folder with all
    /// it holds (<see cref="FileImportWalk"/>).
    /// </summary>
    public ImportPreview Preview(IEnumerable<string> paths, ParentLink? under = null)
    {
        var walk = new FileImportWalk(graph, under);
        walk.Add(paths.Select(Path.TrimEndingDirectorySeparator), folder: null);
        return new ImportPreview(walk.Rows, walk.Skipped, under);
    }

    /// <summary>The preview of importing <paramref name="files"/>, each a new dataset.</summary>
    public ImportPreview Preview(IReadOnlyList<ImportFile> files, ParentLink? under = null)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var rows = files.Select(file =>
        {
            var name = NodeNameOf(file.Name);
            return new ImportRow(file, name, graph.NodesNamed(name).Count > 0 || !names.Add(NameKey.Of(name)));
        }).ToList();
        return new ImportPreview(rows, [], under);
    }

    /// <summary>The changes adding the preview's new nodes and links (new ids at each call: call it once, when accepted).</summary>
    public ImportPlan Plan(ImportPreview preview)
    {
        var now = _clock.GetUtcNow();
        var linkType = preview.Under?.LinkType ?? TypeCatalog.Contains.Id;
        var nodes = new Dictionary<ImportRow, Node>(ReferenceEqualityComparer.Instance);
        var changes = new List<Change>();
        var files = new List<ImportedFile>();
        var updates = new List<ImportedFile>();
        foreach (var row in preview.Rows)
        {
            if (row.Existing is { } existing)
            {
                nodes[row] = existing;
                if (row.File is { } update)
                {
                    updates.Add(new ImportedFile(existing, update));
                }

                continue;
            }

            var node = new Node(Guid.NewGuid(), row.IsFolder ? TypeCatalog.Generic.Id : TypeCatalog.Dataset.Id, row.NodeName, now);
            nodes[row] = node;
            changes.Add(new NodeAdded(Guid.NewGuid(), now, node));
            if ((row.Folder is null ? preview.Under?.Parent : nodes[row.Folder]) is { } parent)
            {
                changes.Add(new LinkAdded(Guid.NewGuid(), now, new Link(Guid.NewGuid(), linkType, parent.Id, node.Id, now)));
            }

            if (row.File is { } file)
            {
                files.Add(new ImportedFile(node, file));
            }
        }

        return new ImportPlan(changes, files, updates);
    }

    /// <summary>The file name without its extension; the whole name when that leaves nothing (".gitignore").</summary>
    public static string NodeNameOf(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName).Trim();
        return name.Length > 0 ? name : fileName.Trim();
    }
}
