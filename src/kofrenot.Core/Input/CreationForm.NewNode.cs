using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

/// <summary>
/// A creation started without typed text (a view's New, the tabs' New view…): one new node, its
/// name typed on the creation screen, its type fixed when given.
/// </summary>
public sealed partial class CreationForm
{
    /// <summary>One new node, its name empty until typed; <paramref name="type"/> fixed, or chosen when null.</summary>
    public static CreationForm NewNode(KnowledgeGraph graph, NodeType? type = null, TimeProvider? clock = null) =>
        new(graph, new NodeRequest(string.Empty), new AddChoices(LeftType: type), clock)
        {
            IsNameEditable = true,
            IsTypeFixed = type is not null,
        };

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

    private bool HasName => Plan is not NodePlan { Name.Length: 0 };
}
