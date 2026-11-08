using Kofrenot.Core.Fields;
using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Imports;

/// <summary>
/// A mapping as loaded to run or edit it: its current versions, and the definition used (the
/// latest one's; null before the first). Several versions are a conflict: edited on two devices
/// before they synced.
/// </summary>
public sealed record MappingState(IReadOnlyList<MappingEdited> Versions, MappingDefinition? Definition)
{
    public bool IsConflict => Versions.Count > 1;
}

/// <summary>
/// Lists the mappings, loads one's definition, and turns an edit into the changes that save it, as
/// <see cref="Views.ViewPlanner"/> does for a view: a version replaces every version loaded, so
/// saving resolves a conflict.
/// </summary>
public sealed class MappingPlanner(KnowledgeGraph graph, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    /// <summary>The mapping nodes, by name.</summary>
    public IReadOnlyList<Node> Mappings => graph.Nodes
        .Where(node => node.Type == TypeCatalog.Mapping.Id)
        .OrderBy(node => node.Name, StringComparer.CurrentCultureIgnoreCase)
        .ThenBy(node => node.Id)
        .ToList();

    /// <summary>
    /// The field rules suggested for a new mapping: each small field of <paramref name="type"/> gets
    /// the first of <paramref name="keys"/> whose name, or its last part (<c>publisher.name</c> → <c>name</c>),
    /// is the field's (case and accents ignored).
    /// </summary>
    public static IReadOnlyList<FieldRule> Suggest(NodeType type, IReadOnlyList<string> keys) =>
        type.Fields
            .Where(field => field.Kind.IsSmall())
            .Select(field => keys.FirstOrDefault(key => NameKey.Of(key) == NameKey.Of(field.Name) || NameKey.Of(key.Split('.')[^1]) == NameKey.Of(field.Name)) is { } key
                ? new FieldRule(key, field.Id)
                : null)
            .OfType<FieldRule>()
            .ToList();

    public MappingState Load(Node mapping)
    {
        var versions = graph.MappingVersionsOf(mapping.Id);
        return new MappingState(versions, versions is [.., var latest] ? latest.Definition : null);
    }

    /// <summary>A new mapping node named <paramref name="name"/> (trimmed), with its first version.</summary>
    public IReadOnlyList<Change> Add(string name, MappingDefinition definition)
    {
        var now = _clock.GetUtcNow();
        var node = new Node(Guid.NewGuid(), TypeCatalog.Mapping.Id, name.Trim(), now);
        return [new NodeAdded(Guid.NewGuid(), now, node), new MappingEdited(Guid.NewGuid(), now, node.Id, definition, [])];
    }

    /// <summary>
    /// The changes saving an edit: the rename when <paramref name="name"/> differs, and the version
    /// when <paramref name="definition"/> is not the one loaded or there is a conflict. Empty when nothing changed.
    /// </summary>
    public IReadOnlyList<Change> Save(Node mapping, string name, MappingDefinition definition, MappingState loaded)
    {
        var now = _clock.GetUtcNow();
        var changes = new List<Change>();
        if (name.Trim() != mapping.Name)
        {
            changes.Add(new NodeRenamed(Guid.NewGuid(), now, mapping.Id, name.Trim(), graph.NamesOf(mapping.Id).Select(current => current.Id).ToList()));
        }

        if (definition != loaded.Definition || loaded.IsConflict)
        {
            changes.Add(new MappingEdited(Guid.NewGuid(), now, mapping.Id, definition, loaded.Versions.Select(version => version.Id).ToList()));
        }

        return changes;
    }
}
