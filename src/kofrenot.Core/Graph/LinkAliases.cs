namespace Kofrenot.Core.Graph;

/// <summary>
/// An alias the user added to a link type. <see cref="IsUsed"/> is false when its phrase is taken
/// already, by a built-in phrase or an older alias (the same phrase added on two devices before
/// they synced): it is then ignored.
/// </summary>
public sealed record LinkAlias(Guid Id, LinkVerb Verb, string Phrase, bool IsUsed);

/// <summary>A link type read one way, with its own phrases (its name first) and the user's aliases.</summary>
public sealed record VerbPhrases(LinkVerb Verb, IReadOnlyList<string> BuiltIn, IReadOnlyList<LinkAlias> Aliases);

/// <summary>A link type with its phrases each way it reads; no <see cref="Reverse"/> when it reads the same both ways.</summary>
public sealed record LinkTypePhrases(LinkType Type, VerbPhrases Forward, VerbPhrases? Reverse);

/// <summary>
/// The aliases of a vault, on top of its link types (the built-in ones and the user's):
/// <see cref="Catalog"/> is what the input box reads. Built again after each change. An alias of a
/// link type this version does not know, or that was removed, is kept but not used.
/// </summary>
public sealed class LinkAliases
{
    private readonly Func<TypeCatalog> _types;
    private readonly RemovableChanges<AliasAdded> _added;
    private (TypeCatalog Catalog, List<LinkAlias> All)? _built;

    internal LinkAliases(Func<TypeCatalog> types, ISet<Guid> removed)
    {
        _types = types;
        _added = new RemovableChanges<AliasAdded>(removed);
    }

    /// <summary>The link types with the aliases in use added to their phrases.</summary>
    public TypeCatalog Catalog => Build().Catalog;

    /// <summary>The aliases not removed, oldest first.</summary>
    public IReadOnlyList<LinkAlias> All => Build().All;

    /// <summary>Each link type, the built-in ones first, with its phrases each way: what the link types screen lists.</summary>
    public IReadOnlyList<LinkTypePhrases> ByType() =>
        _types().LinkTypes
            .Select(type => new LinkTypePhrases(type, PhrasesOf(type.Forward), type.Reverse is { } reverse ? PhrasesOf(reverse) : null))
            .ToList();

    private VerbPhrases PhrasesOf(LinkVerb verb) => new(verb, verb.Phrases, All.Where(alias => alias.Verb == verb).ToList());

    internal bool Add(AliasAdded alias)
    {
        var added = _added.Add(alias);
        if (added)
        {
            Changed();
        }

        return added;
    }

    /// <summary>Called when something was removed, or a link type changed: the aliases are built again.</summary>
    internal void Changed() => _built = null;

    private (TypeCatalog Catalog, List<LinkAlias> All) Build()
    {
        if (_built is { } built)
        {
            return built;
        }

        var types = _types();
        var taken = types.Verbs.SelectMany(verb => verb.Phrases).Select(NameKey.Of).ToHashSet();
        var phrases = types.Verbs.ToDictionary(verb => verb, verb => verb.Phrases.ToList());
        var all = new List<LinkAlias>();
        foreach (var alias in _added.Current)
        {
            if (VerbOf(types, alias) is not { } verb)
            {
                continue;
            }

            var isUsed = taken.Add(NameKey.Of(alias.Phrase));
            if (isUsed)
            {
                phrases[verb].Add(alias.Phrase);
            }

            all.Add(new LinkAlias(alias.Id, verb, alias.Phrase, isUsed));
        }

        var withAliases = types.LinkTypes.Select(type => all.Any(alias => alias.IsUsed && alias.Verb.Type == type)
            ? type with { Phrases = phrases[type.Forward], ReversePhrases = type.Reverse is { } reverse ? phrases[reverse] : null }
            : type);
        var catalog = all.Count == 0 ? types : new TypeCatalog(types.NodeTypes, withAliases.ToList());
        _built = (catalog, all);
        return (catalog, all);
    }

    private static LinkVerb? VerbOf(TypeCatalog types, AliasAdded alias) =>
        types.FindLinkType(alias.LinkType) is { } type
            ? alias.IsReverse ? type.Reverse : type.Forward
            : null;
}
