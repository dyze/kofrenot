using Kofrenot.Core.Fields;

namespace Kofrenot.Core.Graph;

/// <summary>
/// A link type of the user's, shown with its latest current definition, or a built-in one given
/// fields. <see cref="Names"/> holds several after changes made apart (a conflict).
/// <see cref="Definitions"/> are all its definitions: what removes it. <see cref="TakenBy"/> is an
/// older link type that has one of its phrases already (the same phrase added on two devices before
/// they synced): the input box then reads that one.
/// </summary>
public sealed record CustomLinkType(LinkType Type, IReadOnlyList<LinkTypeDefined> Names, IReadOnlyList<Guid> Definitions, LinkType? TakenBy)
{
    public bool IsConflict => Names.Count > 1;

    /// <summary>The fields of its links, their qualifiers, in order: the latest current definition's.</summary>
    public IReadOnlyList<FieldDefinition> Fields => Names[^1].Fields;
}

/// <summary>
/// The link types of a vault: the built-in ones, then the user's, oldest first, so a phrase
/// added twice is read as the older type's. A built-in type may be given fields, its names kept.
/// Built again after each change.
/// </summary>
public sealed class CustomLinkTypes
{
    private readonly TypeCatalog _builtIn;
    private readonly ISet<Guid> _removed;
    private readonly Dictionary<Guid, LinkTypeDefined> _definitions = [];
    private Built? _built;

    internal CustomLinkTypes(TypeCatalog builtIn, ISet<Guid> removed)
    {
        _builtIn = builtIn;
        _removed = removed;
    }

    /// <summary>The built-in link types and the user's, without aliases.</summary>
    public TypeCatalog Catalog => Build().Catalog;

    /// <summary>The user's link types not removed, oldest first.</summary>
    public IReadOnlyList<CustomLinkType> All => Build().All;

    /// <summary>The built-in link types given fields, in the built-in order.</summary>
    public IReadOnlyList<CustomLinkType> BuiltInDefined => Build().BuiltIn;

    /// <summary>The user's link type with this id, or null.</summary>
    public CustomLinkType? Find(string id) => All.FirstOrDefault(custom => custom.Type.Id == id);

    /// <summary>The definitions of the link type with this id, the user's or a built-in one given fields; null when it has none.</summary>
    public CustomLinkType? Defined(string id) => Find(id) ?? BuiltInDefined.FirstOrDefault(custom => custom.Type.Id == id);

    /// <summary>The fields of the links of a type, their qualifiers; none for a type not defined.</summary>
    public IReadOnlyList<FieldDefinition> FieldsOf(string id) => Defined(id)?.Fields ?? [];

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

    private Built Build()
    {
        if (_built is { } built)
        {
            return built;
        }

        var current = _definitions.Values.Where(definition => !_removed.Contains(definition.Id)).ToList();
        var builtIn = _builtIn.LinkTypes
            .Select(type => (Type: type, Definitions: current.Where(definition => definition.LinkType == type.Id).ToList()))
            .Where(type => type.Definitions.Count > 0)
            .Select(type => new CustomLinkType(type.Type, CurrentVersions.Of(type.Definitions), type.Definitions.Select(definition => definition.Id).ToList(), TakenBy: null))
            .ToList();

        var taken = new Dictionary<string, LinkType>();
        foreach (var verb in _builtIn.Verbs)
        {
            foreach (var phrase in verb.Phrases)
            {
                taken.TryAdd(NameKey.Of(phrase), verb.Type);
            }
        }

        var all = new List<CustomLinkType>();
        var byType = current
            .Where(definition => _builtIn.FindLinkType(definition.LinkType) is null)
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
        _built = new Built(catalog, all, builtIn);
        return _built;
    }

    private sealed record Built(TypeCatalog Catalog, List<CustomLinkType> All, List<CustomLinkType> BuiltIn);
}
