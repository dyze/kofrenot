using Kofrenot.Core.Graph;
using Kofrenot.Core.Views;

namespace Kofrenot.Core.Datasets;

/// <summary>A dataset's file in an export: where it goes (relative to the export's folder) and the version written there.</summary>
public sealed record ExportedFile(string Path, DatasetEdited Version);

/// <summary>
/// Nodes dragged out, as files and folders: <see cref="Top"/> are the names dropped (relative to the
/// export's folder), <see cref="Folders"/> every folder to create, <see cref="Files"/> every file.
/// Left out: datasets whose file is not on this device yet (<see cref="NotDownloaded"/>), and nodes
/// with neither a file nor children (<see cref="NotFiles"/>: notes, empty datasets...).
/// </summary>
public sealed record FolderExport(IReadOnlyList<string> Top, IReadOnlyList<string> Folders, IReadOnlyList<ExportedFile> Files, int NotDownloaded, int NotFiles)
{
    public bool IsEmpty => Top.Count == 0;

    /// <summary>Creates the folders under <paramref name="folder"/>, then writes each file; the full paths of <see cref="Top"/>.</summary>
    /// <param name="export">Writes a dataset version's file to the path given.</param>
    public IReadOnlyList<string> WriteTo(string folder, Action<Guid, string> export)
    {
        foreach (var path in Folders)
        {
            Directory.CreateDirectory(Path.Combine(folder, path));
        }

        foreach (var file in Files)
        {
            export(file.Version.Id, Path.Combine(folder, file.Path));
        }

        return Top.Select(name => Path.Combine(folder, name)).ToList();
    }
}

/// <summary>
/// Turns nodes selected in a view into files and folders, the reverse of <see cref="FileImportPlanner"/>:
/// a dataset becomes its latest file; a node with children in the tree (by <c>linkType</c>; none in a
/// list) a folder named after it, holding its children the same way. Names are made safe for
/// Windows, and the same name twice in a folder becomes "name (2).ext". A node met twice (selected
/// with its parent, or in a cycle) is written once.
/// </summary>
/// <param name="linkType">The tree's link type; null in a list, where nodes have no children.</param>
public sealed class FolderExportPlanner(KnowledgeGraph graph, string? linkType)
{
    private readonly TreeLinks? _tree = linkType is null ? null : new TreeLinks(graph, linkType);

    /// <param name="hasContent">Whether a dataset version's file is on this device.</param>
    public FolderExport Plan(IEnumerable<Node> nodes, Func<Guid, bool> hasContent)
    {
        var walk = new Walk(graph, _tree, hasContent);
        var top = new Names();
        var names = nodes.SelectMany(node => walk.Add(node, folder: string.Empty, top)).ToList();
        return new FolderExport(names, walk.Folders, walk.Files, walk.NotDownloaded, walk.NotFiles);
    }

    private sealed class Walk(KnowledgeGraph graph, TreeLinks? tree, Func<Guid, bool> hasContent)
    {
        private readonly HashSet<Guid> _seen = [];

        public List<string> Folders { get; } = [];

        public List<ExportedFile> Files { get; } = [];

        public int NotDownloaded { get; private set; }

        public int NotFiles { get; private set; }

        /// <summary>Adds the node into <paramref name="folder"/>: the names it takes there (its file, its folder, or both).</summary>
        public IEnumerable<string> Add(Node node, string folder, Names names)
        {
            if (!_seen.Add(node.Id))
            {
                return [];
            }

            var added = new List<string>();
            var children = tree is null ? [] : NodeOrder.ByName(tree.ChildrenOf(node.Id));
            if (graph.DatasetVersionsOf(node.Id) is [.., var latest])
            {
                if (hasContent(latest.Id))
                {
                    var name = names.Take(latest.File.SafeName);
                    Files.Add(new ExportedFile(Path.Combine(folder, name), latest));
                    added.Add(name);
                }
                else
                {
                    NotDownloaded++;
                }
            }
            else if (children.Count == 0)
            {
                NotFiles++;
            }

            if (children.Count > 0)
            {
                var name = names.Take(DatasetFile.SafeNameOf(node.Name));
                var path = Path.Combine(folder, name);
                Folders.Add(path);
                added.Add(name);
                var inside = new Names();
                foreach (var child in children)
                {
                    _ = Add(child, path, inside);
                }
            }

            return added;
        }
    }

    /// <summary>The names taken in a folder, as Windows compares them (case ignored).</summary>
    private sealed class Names
    {
        private readonly HashSet<string> _taken = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>The name, or "name (2).ext", "name (3).ext"... when taken.</summary>
        public string Take(string name)
        {
            var stem = Path.GetFileNameWithoutExtension(name);
            var extension = Path.GetExtension(name);
            var free = name;
            for (var number = 2; !_taken.Add(free); number++)
            {
                free = $"{stem} ({number}){extension}";
            }

            return free;
        }
    }
}
