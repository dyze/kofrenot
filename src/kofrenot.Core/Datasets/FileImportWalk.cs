using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.Core.Views;

namespace Kofrenot.Core.Datasets;

/// <summary>
/// The rows of an import of files and folders on disk: each folder walked whole, its folders first,
/// then its files, by name. Missing or unreadable items and folder links (junctions) are skipped, so
/// a walk never loops; hidden and system items inside a folder (desktop.ini) are left out.
/// <para>
/// Each item is matched with the nodes already under its parent, as the tree shows them: a file
/// updates the oldest dataset of its name, a folder reuses the oldest other node of its name, and
/// its content is matched under it in turn. Items of one folder whose names would be the same
/// (report.pdf, report.docx) keep their extension. A new node is a homonym when its name is taken
/// under its parent, or anywhere in the vault for a top item without a parent.
/// </para>
/// </summary>
internal sealed class FileImportWalk(KnowledgeGraph graph, ParentLink? under)
{
    private readonly TreeLinks _tree = new(graph, under?.LinkType ?? TypeCatalog.Contains.Id);

    public List<ImportRow> Rows { get; } = [];

    public List<string> Skipped { get; } = [];

    /// <summary>Adds the rows of <paramref name="paths"/>, the items of <paramref name="folder"/> (null at the top).</summary>
    public void Add(IEnumerable<string> paths, ImportRow? folder) => Add(paths.Select(Read).OfType<Item>().ToList(), folder);

    /// <summary>Adds the rows of <paramref name="files"/>, at the top: files given, not read from a folder (a share).</summary>
    public void Add(IEnumerable<ImportFile> files) =>
        Add(files.Select(file => new Item(FileImportPlanner.NodeNameOf(file.Name), file, null)).ToList(), folder: null);

    private void Add(List<Item> items, ImportRow? folder)
    {
        var stems = items.GroupBy(item => NameKey.Of(item.Name)).Where(same => same.Count() > 1).Select(same => same.Key).ToHashSet();
        var parent = folder is null ? under?.Parent : folder.Existing;
        var children = parent is null ? [] : _tree.ChildrenOf(parent.Id).OrderBy(child => child.Created).ToList();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            var name = item.File is { } file && stems.Contains(NameKey.Of(item.Name)) ? file.Name.Trim() : item.Name;
            var key = NameKey.Of(name);
            var named = children.Where(child => NameKey.Of(child.Name) == key).ToList();
            var isNew = names.Add(key);
            var existing = isNew ? named.FirstOrDefault(child => (child.Type == TypeCatalog.Dataset.Id) != item.IsFolder) : null;
            var taken = parent is null && folder is null ? graph.NodesNamed(name).Count > 0 : named.Count > 0;
            var row = new ImportRow(item.File, name, existing is null && (taken || !isNew), folder, existing);
            Rows.Add(row);
            if (item.Items is { } inside)
            {
                Add(inside, row);
            }
        }
    }

    /// <summary>A file, or a folder with its items; null when skipped.</summary>
    private Item? Read(string path)
    {
        if (Directory.Exists(path) && !IsLink(path) && TryList(path) is { } items)
        {
            return new Item(Path.GetFileName(path).Trim(), null, items);
        }

        if (TryRead(path) is { } file)
        {
            return new Item(FileImportPlanner.NodeNameOf(file.Name), file, null);
        }

        Skipped.Add(Path.GetFileName(path));
        return null;
    }

    private static bool IsLink(string folder) => new DirectoryInfo(folder).Attributes.HasFlag(FileAttributes.ReparsePoint);

    /// <summary>The folder's items to import, its folders first, then its files, by name; null when it cannot be listed.</summary>
    private static List<string>? TryList(string folder)
    {
        try
        {
            return new DirectoryInfo(folder).EnumerateFileSystemInfos()
                .Where(item => (item.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0)
                .OrderBy(item => item is FileInfo)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .Select(item => item.FullName)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
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

    /// <param name="Name">The node name it would have: a file's without its extension.</param>
    /// <param name="Items">A folder's items; null for a file.</param>
    private sealed record Item(string Name, ImportFile? File, List<string>? Items)
    {
        public bool IsFolder => Items is not null;
    }
}
