using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

/// <summary>How plans and nodes are named to the user, shared by the input box and the creation screen.</summary>
internal sealed class PlanText(KnowledgeGraph graph)
{
    /// <summary>A node's name, marked "¹", "²", ... when several nodes have it (oldest first).</summary>
    public string NodeLabel(Node node)
    {
        var homonyms = graph.NodesNamed(node.Name);
        return homonyms.Count > 1 ? node.Name + SearchHit.Superscript(homonyms.ToList().IndexOf(node) + 1) : node.Name;
    }

    /// <summary>The chosen node's label, or the name typed for a new one.</summary>
    public string Label(PlannedSide side) => side.Chosen is { } node ? NodeLabel(node) : side.Name;

    /// <summary>What the plan adds: "the node “Dune”, the link “Dune relates to Frank Herbert”".</summary>
    public string Additions(AddPlan plan) => plan is LinkPlan link
        ? string.Join(", ", new[] { link.Left, link.Right! }
            .Where(side => side.IsNew)
            .Select(side => $"the node “{side.Name}”")
            .Append($"the link “{Label(link.Left)} {link.Verb.Name} {Label(link.Right!)}”"))
        : $"the node “{((NodePlan)plan).Name}”";

    /// <summary>Why the link cannot be added, or null when it can.</summary>
    public string? Problem(LinkPlan link) => link.Problem switch
    {
        LinkProblem.SecondNodeMissing => $"Type the node to link “{Label(link.Left)}” to.",
        LinkProblem.SameNode => "A node cannot be linked to itself.",
        LinkProblem.AlreadyLinked => $"“{Label(link.Left)}” {link.Verb.Name} “{Label(link.Right!)}” already.",
        _ => null,
    };

    /// <summary>"“Dune” exists already (2 times)." for a node plan repeating a name; null otherwise.</summary>
    public static string? Duplicate(AddPlan plan) => plan is NodePlan { Existing.Count: > 0 } node
        ? $"“{node.Name}” exists already{(node.Existing.Count > 1 ? $" ({node.Existing.Count} times)" : string.Empty)}."
        : null;
}
