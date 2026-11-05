namespace Kofrenot.Core.Graph;

/// <summary>
/// The links of a graph. Copies of a link (same type and nodes, either direction when symmetric:
/// added on two devices before they synced) count as one: the oldest is shown. A separate link
/// (<see cref="Link.IsSeparate"/>) is no copy: it is shown beside the others. A removed link is
/// not shown, and the next copy is, so a removal names every copy (<see cref="CopiesOf"/>). A
/// removal may arrive before its link. A link of a type not known yet reads one way until its type
/// arrives (<see cref="Refile"/>).
/// </summary>
internal sealed class LinkIndex(Func<TypeCatalog> types, ISet<Guid> removed)
{
    private readonly Dictionary<Guid, Link> _added = [];
    private readonly Dictionary<LinkKey, List<Link>> _copies = [];
    private readonly Dictionary<Guid, Link> _shown = [];
    private readonly Dictionary<Guid, List<Link>> _byNode = [];

    public IReadOnlyCollection<Link> Shown => _shown.Values;

    /// <summary>The links shown of a node, oldest first, whether its other node is known or not.</summary>
    public IReadOnlyList<Link> Of(Guid node) => _byNode.TryGetValue(node, out var links) ? links : [];

    /// <summary>The links shown of this type between these nodes, oldest first; either direction for a symmetric type.</summary>
    public IReadOnlyList<Link> Between(string type, Guid from, Guid to)
    {
        var symmetric = IsSymmetric(type);
        return Of(from).Where(link => link.Type == type && ((link.From == from && link.To == to) || (symmetric && link.From == to && link.To == from))).ToList();
    }

    /// <summary>The link shown for this link or copy id; null when removed, or not arrived.</summary>
    public Link? ShownOf(Guid id) =>
        _added.TryGetValue(id, out var link) && _copies.TryGetValue(KeyOf(link), out var copies) && copies.Any(copy => copy.Id == id)
            ? copies[0]
            : null;

    /// <summary>The ids of the link and of its copies not removed, oldest first.</summary>
    public IReadOnlyList<Guid> CopiesOf(Link link) =>
        _copies.TryGetValue(KeyOf(link), out var copies) ? copies.Select(copy => copy.Id).ToList() : [];

    /// <summary>False when it is already there, or changes no link shown (removed, or a newer copy).</summary>
    public bool Add(Link link)
    {
        if (!_added.TryAdd(link.Id, link) || removed.Contains(link.Id))
        {
            return false;
        }

        var key = KeyOf(link);
        if (!_copies.TryGetValue(key, out var copies))
        {
            _copies[key] = copies = [];
        }

        var shown = copies.FirstOrDefault();
        OldestFirst.Insert(copies, link, copy => (copy.Created, copy.Id));
        return Replace(shown, copies[0]);
    }

    /// <summary>Hides the links among <paramref name="ids"/>: those added already (the others are in <c>removed</c>).</summary>
    public void Remove(IEnumerable<Guid> ids)
    {
        foreach (var id in ids)
        {
            if (!_added.TryGetValue(id, out var link)
                || !_copies.TryGetValue(KeyOf(link), out var copies)
                || copies.RemoveAll(copy => copy.Id == id) == 0)
            {
                continue;
            }

            var shown = _shown.ContainsKey(id) ? link : copies[0];
            if (copies.Count == 0)
            {
                _copies.Remove(KeyOf(link));
            }

            Replace(shown, copies.FirstOrDefault());
        }
    }

    /// <summary>Files the links of these types again: whether they read the same both ways changed.</summary>
    public void Refile(ISet<string> linkTypes)
    {
        foreach (var key in _copies.Keys.Where(key => linkTypes.Contains(key.Type)).ToList())
        {
            _copies.Remove(key);
        }

        foreach (var shown in _shown.Values.Where(link => linkTypes.Contains(link.Type)).ToList())
        {
            Replace(shown, null);
        }

        foreach (var link in _added.Values.Where(link => linkTypes.Contains(link.Type)).ToList())
        {
            _added.Remove(link.Id);
            Add(link);
        }
    }

    private bool Replace(Link? shown, Link? next)
    {
        if (ReferenceEquals(shown, next))
        {
            return false;
        }

        if (shown is not null)
        {
            _shown.Remove(shown.Id);
            ForEachEnd(shown, links => links.Remove(shown));
        }

        if (next is not null)
        {
            _shown[next.Id] = next;
            ForEachEnd(next, links => OldestFirst.Insert(links, next, link => (link.Created, link.Id)));
        }

        return true;
    }

    private void ForEachEnd(Link link, Action<List<Link>> action)
    {
        action(LinksOf(link.From));
        if (link.To != link.From)
        {
            action(LinksOf(link.To));
        }
    }

    private List<Link> LinksOf(Guid node)
    {
        if (!_byNode.TryGetValue(node, out var links))
        {
            _byNode[node] = links = [];
        }

        return links;
    }

    // A separate link is keyed by its id too: it has no copy.
    private LinkKey KeyOf(Link link)
    {
        var separate = link.IsSeparate ? link.Id : Guid.Empty;
        return IsSymmetric(link.Type) && link.From.CompareTo(link.To) > 0
            ? new LinkKey(link.Type, link.To, link.From, separate)
            : new LinkKey(link.Type, link.From, link.To, separate);
    }

    private bool IsSymmetric(string type) => types().FindLinkType(type)?.IsSymmetric ?? false;

    private readonly record struct LinkKey(string Type, Guid First, Guid Second, Guid Separate);
}
