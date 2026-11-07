using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

/// <summary>
/// The node a new node is linked to, and the type of the link: from <see cref="Parent"/> (under it, in
/// a tree), or to it when <see cref="IsReverse"/> (a graph's New node, its "child of").
/// </summary>
public sealed record ParentLink(Node Parent, string LinkType, bool IsReverse = false);

/// <summary>
/// A creation started without typed text (a view's New, the tabs' New view…): one new node, its
/// name typed on the creation screen, its type fixed when given, linked under a parent when given.
/// </summary>
public sealed partial class CreationForm
{
    /// <summary>One new node, its name typed (<paramref name="name"/> to begin with, or empty); <paramref name="type"/> fixed, or chosen when null.</summary>
    /// <param name="under">The node it is linked to: in a tree, the node it is added under.</param>
    public static CreationForm NewNode(KnowledgeGraph graph, NodeType? type = null, ParentLink? under = null, string? name = null, TimeProvider? clock = null) =>
        new(graph, new NodeRequest(name?.Trim() ?? string.Empty), new AddChoices(LeftType: type), clock)
        {
            StartsOnName = true,
            IsTypeFixed = type is not null,
            Under = under,
        };

    /// <summary>The node the new node is linked to, or null.</summary>
    public ParentLink? Under { get; private init; }

    /// <summary>The name can be typed, or retyped, on the creation screen (<see cref="Name"/>): a node's, not a link's.</summary>
    public bool IsNameEditable => _request is NodeRequest;

    /// <summary>The name is typed first: nothing was typed before the creation screen (a view's New, a dropped file).</summary>
    public bool StartsOnName { get; private init; }

    /// <summary>The new node's type is given: the creation screen does not offer to change it.</summary>
    public bool IsTypeFixed { get; private init; }

    /// <summary>The new node's name, while it is typed (trimmed).</summary>
    /// <exception cref="InvalidOperationException">Set on a link request.</exception>
    public string Name
    {
        get => NameOf(Side.Left);
        set
        {
            if (!IsNameEditable)
            {
                throw new InvalidOperationException("A link's names come from the request.");
            }

            Replan(new NodeRequest(value.Trim()), _choices);
        }
    }

    /// <summary>The link between <see cref="Under"/> and the new node, for its node in <see cref="AddPlan.Changes"/>. New ids at each call: call it once, when adding.</summary>
    public IReadOnlyList<LinkAdded> LinksToSave()
    {
        if (Under is not { } under || Plan.NewNodes is not [var node])
        {
            return [];
        }

        var now = _clock.GetUtcNow();
        var (from, to) = under.IsReverse ? (node.Id, under.Parent.Id) : (under.Parent.Id, node.Id);
        return [new LinkAdded(Guid.NewGuid(), now, new Link(Guid.NewGuid(), under.LinkType, from, to, now))];
    }

    // "Add a node: “Anne” parent of …", as the link reads from the node it is added to.
    private string TitleUnder(ParentLink under) =>
        Types.FindLinkType(under.LinkType) is { } type && (!under.IsReverse || type.Reverse is not null)
            ? $"Add a node: “{under.Parent.Name}” {(under.IsReverse ? type.Reverse! : type.Forward).Name} …"
            : $"Add a node under “{under.Parent.Name}”";

    private bool HasName => Plan is not NodePlan { Name.Length: 0 };
}
