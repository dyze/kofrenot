namespace Kofrenot.Core.Graph;

/// <summary>
/// A link type of the user's, shown with its latest current name. <see cref="Names"/> holds
/// several after renames made apart (a conflict). <see cref="TakenBy"/> is an older link type
/// that has one of its phrases already (the same phrase added on two devices before they synced):
/// the input box then reads that one.
/// </summary>
public sealed record CustomLinkType(LinkType Type, IReadOnlyList<LinkTypeDefined> Names, IReadOnlyList<Guid> Definitions, LinkType? TakenBy)
{
    public bool IsConflict => Names.Count > 1;
}

/// <summary>
/// The link types of a vault: the built-in ones, then the user's, oldest first, so a phrase
/// added twice is read as the older type's. Built again after each change.
/// </summary>
public sealed class CustomLinkTypes
{
    private readonly TypeCatalog _builtIn;
    private readonly ISet<Guid> _removed;
    private readonly Dictionary<Guid, LinkTypeDefined> _definitions = [];
    private (TypeCatalog Catalog, List<CustomLinkType> All)? _built;

    internal CustomLinkTypes(TypeCatalog builtIn, ISet<Guid> removed)
    {
        _builtIn = builtIn;
        _removed = removed;
    }

    /// <summary>The built-in link types and the user's, without aliases.</summary>
    public TypeCatalog Catalog => Build().Catalog;

    /// <summary>The user's link types not removed, oldest first.</summary>
    public IReadOnlyList<CustomLinkType> All => Build().All;

    /// <summary>The user's link type with this id, or null.</summary>
    public CustomLinkType? Find(string id) => All.FirstOrDefault(custom => custom.Type.Id == id);

    internal bool Add(LinkTypeDefined definition)
    {
        var added = _definitions.TryAdd(definition.Id, definition);
        if (added)
        {
            Changed();
        }

        return added;
    }

    /// <summary>The link types that some of these ids name.</summary>
    internal IReadOnlyList<string> NamedBy(IEnumerable<Guid> ids) =>
        ids.Select(id => _definitions.GetValueOrDefault(id)?.LinkType).OfType<string>().Distinct().ToList();

    /// <summary>Called when something was removed: the types are built again.</summary>
    internal void Changed() => _built = null;

    private (TypeCatalog Catalog, List<CustomLinkType> All) Build()
    {
        if (_built is { } built)
        {
            return built;
        }

        var taken = new Dictionary<string, LinkType>();
        foreach (var verb in _builtIn.Verbs)
        {
            foreach (var phrase in verb.Phrases)
            {
                taken.TryAdd(NameKey.Of(phrase), verb.Type);
            }
        }

        var all = new List<CustomLinkType>();
        var byType = _definitions.Values
            .Where(definition => !_removed.Contains(definition.Id) && _builtIn.FindLinkType(definition.LinkType) is null)
            .GroupBy(definition => definition.LinkType)
            .OrderBy(definitions => definitions.Min(definition => definition.At))
            .ThenBy(definitions => definitions.Key, StringComparer.Ordinal);
        foreach (var definitions in byType)
        {
            var names = CurrentVersions.Of(definitions);
            var shown = names[^1];
            var type = new LinkType(definitions.Key, [shown.Name], shown.ReverseName is { } reverse ? [reverse] : null);
            LinkType? takenBy = null;
            foreach (var verb in type.Verbs)
            {
                if (!taken.TryAdd(NameKey.Of(verb.Name), type))
                {
                    takenBy ??= taken[NameKey.Of(verb.Name)];
                }
            }

            all.Add(new CustomLinkType(type, names, definitions.Select(definition => definition.Id).ToList(), takenBy));
        }

        var catalog = all.Count == 0
            ? _builtIn
            : new TypeCatalog(_builtIn.NodeTypes, [.. _builtIn.LinkTypes, .. all.Select(custom => custom.Type)]);
        _built = (catalog, all);
        return (catalog, all);
    }
}
