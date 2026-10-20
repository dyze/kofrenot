using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

/// <summary>The node a new node is added under, in a tree, and the type of the link from it.</summary>
public sealed record ParentLink(Node Parent, string LinkType);

/// <summary>
/// A creation started without typed text (a view's New, the tabs' New view…): one new node, its
/// name typed on the creation screen, its type fixed when given, linked under a parent when given.
/// </summary>
public sealed partial class CreationForm
{
    /// <summary>One new node, its name typed (<paramref name="name"/> to begin with, or empty); <paramref name="type"/> fixed, or chosen when null.</summary>
    /// <param name="under">In a tree: the node it is added under, linked from it.</param>
    public static CreationForm NewNode(KnowledgeGraph graph, NodeType? type = null, ParentLink? under = null, string? name = null, TimeProvider? clock = null) =>
        new(graph, new NodeRequest(name?.Trim() ?? string.Empty), new AddChoices(LeftType: type), clock)
        {
            IsNameEditable = true,
            IsTypeFixed = type is not null,
            Under = under,
        };

    /// <summary>The node the new node is added under, or null.</summary>
    public ParentLink? Under { get; private init; }

    /// <summary>The name is typed on the creation screen (<see cref="Name"/>).</summary>
    public bool IsNameEditable { get; private init; }

    /// <summary>The new node's type is given: the creation screen does not offer to change it.</summary>
    public bool IsTypeFixed { get; private init; }

    /// <summary>The new node's name, while it is typed (trimmed).</summary>
    /// <exception cref="InvalidOperationException">Set when the name comes from the request.</exception>
    public string Name
    {
        get => NameOf(Side.Left);
        set
        {
            if (!IsNameEditable)
            {
                throw new InvalidOperationException("The name comes from the request.");
            }

            Replan(new NodeRequest(value.Trim()), _choices);
        }
    }

    /// <summary>The link from <see cref="Under"/> to the new node, for its node in <see cref="AddPlan.Changes"/>. New ids at each call: call it once, when adding.</summary>
    public IReadOnlyList<LinkAdded> LinksToSave()
    {
        if (Under is not { } under || Plan.NewNodes is not [var node])
        {
            return [];
        }

        var now = _clock.GetUtcNow();
        return [new LinkAdded(Guid.NewGuid(), now, new Link(Guid.NewGuid(), under.LinkType, under.Parent.Id, node.Id, now))];
    }

    private bool HasName => Plan is not NodePlan { Name.Length: 0 };
}
