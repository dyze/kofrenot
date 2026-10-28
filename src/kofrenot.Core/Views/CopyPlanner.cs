using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Views;

/// <summary>The content of a copied note, dataset or image field: the file of <see cref="From"/>, written again for <see cref="To"/>.</summary>
public sealed record ContentCopy(ContentEdited From, ContentEdited To);

/// <summary>
/// What copying a node does, shown before: the nodes copied (it and its whole subtree, by name).
/// <see cref="Contents"/> are the files to write first, then <see cref="Changes"/> are appended:
/// the new nodes, their links, and their versions. <see cref="Missing"/> lists the nodes whose
/// content is not synced yet: nothing can be copied then.
/// </summary>
public sealed record CopyPlan(IReadOnlyList<Node> Nodes, IReadOnlyList<Change> Changes, IReadOnlyList<ContentCopy> Contents, IReadOnlyList<Node> Missing)
{
    public bool CanCopy => Missing.Count == 0;
}

/// <summary>
/// Copies a node of a tree with its whole subtree under a target node, as new nodes with the same
/// names, types and colour, linked as the originals are. Each note, dataset and view starts with
/// the latest version of its original, each field with the value shown; an image's file is written
/// again, as a dataset's.
/// </summary>
public sealed class CopyPlanner(KnowledgeGraph graph, string linkType, TimeProvider? clock = null)
{
    private readonly TreeLinks _links = new(graph, linkType);
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    /// <param name="isAvailable">Whether the file of a note's or dataset's version is in the vault.</param>
    public CopyPlan Plan(Node node, Node target, Func<ContentEdited, bool> isAvailable)
    {
        var now = _clock.GetUtcNow();
        var subtree = _links.Subtree(node.Id);
        var copyOf = subtree.ToDictionary(id => id, _ => Guid.NewGuid());
        var nodes = subtree.Select(id => id == node.Id ? node : graph.FindNode(id)!).ToList();

        var changes = nodes.SelectMany(original => Copied(original, copyOf[original.Id], now)).ToList();
        changes.Add(NewLink(linkType, target.Id, copyOf[node.Id], now));
        changes.AddRange(subtree
            .SelectMany(_links.ToChildren)
            .DistinctBy(link => link.Id)
            .Select(link => NewLink(link.Type, copyOf[link.From], copyOf[link.To], now)));

        var contents = new List<ContentCopy>();
        var missing = new List<Node>();
        foreach (var original in nodes)
        {
            var copy = copyOf[original.Id];
            if (!CopyFields(original, copy, now, isAvailable, changes, contents))
            {
                missing.Add(original);
                continue;
            }

            if (graph.ViewVersionsOf(original.Id) is [.., var view])
            {
                changes.Add(new ViewEdited(Guid.NewGuid(), now, copy, view.Definition, []));
            }

            ContentEdited? latest = graph.NoteVersionsOf(original.Id) is [.., var note] ? note
                : graph.DatasetVersionsOf(original.Id) is [.., var dataset] ? dataset
                : null;
            if (latest is null)
            {
                continue;
            }

            if (!isAvailable(latest))
            {
                missing.Add(original);
                continue;
            }

            ContentEdited version = latest switch
            {
                DatasetEdited file => new DatasetEdited(Guid.NewGuid(), now, copy, file.File, []),
                _ => new NoteEdited(Guid.NewGuid(), now, copy, []),
            };
            contents.Add(new ContentCopy(latest, version));
            changes.Add(version);
        }

        return new CopyPlan(NodeOrder.ByName(nodes), changes, contents, missing);
    }

    // The node added with its first type, then its other types and its colour.
    private IEnumerable<Change> Copied(Node original, Guid copy, DateTimeOffset now)
    {
        var types = graph.TypeIdsOf(original.Id);
        yield return new NodeAdded(Guid.NewGuid(), now, original with { Id = copy, Type = types.FirstOrDefault() ?? TypeCatalog.Generic.Id, Created = now });
        // A tick apart, so the types keep their order: types given at one time are ordered by id.
        foreach (var (type, index) in types.Skip(1).Select((type, index) => (type, index)))
        {
            yield return new NodeTypeAdded(Guid.NewGuid(), now.AddTicks(index + 1), copy, type);
        }

        if (graph.ColorOf(original.Id) is var color && color != NodeColor.None)
        {
            yield return new NodeColored(Guid.NewGuid(), now, copy, NodeColorNames.Of(color), []);
        }
    }

    // Each field's value shown, an image's file to write again; false when an image's file is not here (nothing is added then).
    private bool CopyFields(Node original, Guid copy, DateTimeOffset now, Func<ContentEdited, bool> isAvailable, List<Change> changes, List<ContentCopy> contents)
    {
        var values = graph.FieldsOf(original.Id).Select(field => field.Shown).Where(value => value.Value is not null || value.File is not null).ToList();
        if (values.Any(value => value.Image is { } image && !isAvailable(image)))
        {
            return false;
        }

        foreach (var value in values)
        {
            var copied = value with { Id = Guid.NewGuid(), At = now, Node = copy, Replaces = [], Content = null };
            if (value.Image is { } image)
            {
                contents.Add(new ContentCopy(image, copied.Image!));
            }

            changes.Add(copied);
        }

        return true;
    }

    private static LinkAdded NewLink(string type, Guid from, Guid to, DateTimeOffset at) =>
        new(Guid.NewGuid(), at, new Link(Guid.NewGuid(), type, from, to, at));
}
