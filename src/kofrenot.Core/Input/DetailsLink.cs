using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

/// <summary>
/// The Link box of a node's details: what is typed reads as in the input box, after the node's
/// name ("relates to Frank Herbert"), the node itself always on the left. <see cref="Request"/> and
/// <see cref="Choices"/> go to the creation screen when <see cref="CanAdd"/>; <see cref="Preview"/> says what Enter does.
/// </summary>
public sealed record DetailsLink(LinkRequest? Request, AddChoices Choices, string Preview, bool CanAdd)
{
    public const string Hint = "Type a link phrase and a node: “relates to Frank Herbert”.";

    public static DetailsLink Of(KnowledgeGraph graph, Node node, string typed)
    {
        var homonyms = graph.NodesNamed(node.Name).ToList();
        var choices = new AddChoices(Left: Math.Max(0, homonyms.FindIndex(other => other.Id == node.Id)));
        if (string.IsNullOrWhiteSpace(typed))
        {
            return new DetailsLink(null, choices, Hint, CanAdd: false);
        }

        if (InputParser.Parse($"{node.Name} {typed.Trim()}", graph.Types) is not LinkRequest request
            || !graph.NodesNamed(request.Left).Any(other => other.Id == node.Id))
        {
            return new DetailsLink(null, choices, "Start with a link phrase, such as “relates to”.", CanAdd: false);
        }

        var plan = (LinkPlan)new AddPlanner(graph).Plan(request, choices);
        var text = new PlanText(graph);
        return text.Problem(plan) is { } problem
            ? new DetailsLink(request, choices, problem, CanAdd: false)
            : new DetailsLink(request, choices, $"Enter to add {text.Additions(plan)}.", CanAdd: true);
    }
}
