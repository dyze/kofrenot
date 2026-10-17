namespace Kofrenot.Core.Graph;

/// <summary>
/// An alias the user added to a link type. <see cref="IsUsed"/> is false when its phrase is taken
/// already, by a built-in phrase or an older alias (the same phrase added on two devices before
/// they synced): it is then ignored.
/// </summary>
public sealed record LinkAlias(Guid Id, LinkVerb Verb, string Phrase, bool IsUsed);

/// <summary>A link type read one way, with its built-in phrases (its name first) and the user's aliases.</summary>
public sealed record VerbPhrases(LinkVerb Verb, IReadOnlyList<string> BuiltIn, IReadOnlyList<LinkAlias> Aliases);

/// <summary>
/// The aliases of a vault, on top of the built-in link types: <see cref="Catalog"/> is what the
/// input box reads. Built again after each change. An alias of a link type this version does not
/// know is kept but not used.
/// </summary>
public sealed class LinkAliases
{
    private readonly TypeCatalog _builtIn;
    private readonly RemovableChanges<AliasAdded> _added;
    private (TypeCatalog Catalog, List<LinkAlias> All)? _built;

    internal LinkAliases(TypeCatalog builtIn, ISet<Guid> removed)
    {
        _builtIn = builtIn;
        _added = new RemovableChanges<AliasAdded>(removed);
    }

    /// <summary>The built-in types with the aliases in use added to their phrases.</summary>
    public TypeCatalog Catalog => Build().Catalog;

    /// <summary>The aliases not removed, oldest first.</summary>
    public IReadOnlyList<LinkAlias> All => Build().All;

    /// <summary>Each link type, each way it reads, with its phrases: what the link types screen lists.</summary>
    public IReadOnlyList<VerbPhrases> ByVerb() =>
        _builtIn.Verbs.Select(verb => new VerbPhrases(verb, verb.Phrases, All.Where(alias => alias.Verb == verb).ToList())).ToList();

    internal bool Add(AliasAdded alias)
    {
        var added = _added.Add(alias);
        if (added)
        {
            Changed();
        }

        return added;
    }

    /// <summary>Called when something was removed: the aliases are built again.</summary>
    internal void Changed() => _built = null;

    private (TypeCatalog Catalog, List<LinkAlias> All) Build()
    {
        if (_built is { } built)
        {
            return built;
        }

        var taken = _builtIn.Verbs.SelectMany(verb => verb.Phrases).Select(NameKey.Of).ToHashSet();
        var phrases = _builtIn.Verbs.ToDictionary(verb => verb, verb => verb.Phrases.ToList());
        var all = new List<LinkAlias>();
        foreach (var alias in _added.Current)
        {
            if (VerbOf(alias) is not { } verb)
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

        var types = _builtIn.LinkTypes.Select(type => all.Any(alias => alias.IsUsed && alias.Verb.Type == type)
            ? type with { Phrases = phrases[type.Forward], ReversePhrases = type.Reverse is { } reverse ? phrases[reverse] : null }
            : type);
        var catalog = all.Count == 0 ? _builtIn : new TypeCatalog(_builtIn.NodeTypes, types.ToList());
        _built = (catalog, all);
        return (catalog, all);
    }

    private LinkVerb? VerbOf(AliasAdded alias) =>
        _builtIn.FindLinkType(alias.LinkType) is { } type
            ? alias.IsReverse ? type.Reverse : type.Forward
            : null;
}
