using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Views;

/// <summary>
/// What a tree operation will do, said before it is done: a summary, then its details one per
/// line. Nothing is done when <see cref="CanProceed"/> is false.
/// </summary>
public sealed record OperationPreview(string Summary, IReadOnlyList<string> Lines, bool CanProceed)
{
    public static OperationPreview Delete(DeletePlan plan, TypeCatalog types)
    {
        var nodes = Count(plan.Nodes.Count, "node");
        var links = Count(plan.Links.Count, "link");
        var others = plan.OtherParents.Select(other => $"“{other.Node.Name}” is also under “{other.Parent.Name}”: deleted there too");
        return new OperationPreview(
            $"Deletes {nodes} and {links}:",
            [.. plan.Nodes.Select(node => Label(node, types)), .. others],
            CanProceed: true);
    }

    public static OperationPreview Copy(CopyPlan plan, Node target, TypeCatalog types) => plan.CanCopy
        ? new OperationPreview(
            $"Copies {Count(plan.Nodes.Count, "node")} under “{target.Name}”, as new nodes:",
            plan.Nodes.Select(node => Label(node, types)).ToList(),
            CanProceed: true)
        : new OperationPreview(
            "Cannot copy: the content of these nodes has not arrived on this computer yet.",
            plan.Missing.Select(node => Label(node, types)).ToList(),
            CanProceed: false);

    /// <summary>Why a node cannot move; null when it can.</summary>
    public static string? Refusal(MoveProblem problem) => problem switch
    {
        MoveProblem.Root => "The node the tree starts from cannot move.",
        MoveProblem.SameParent => "The node is already there.",
        MoveProblem.IntoItsSubtree => "A node cannot move into itself or under one of its children.",
        _ => null,
    };

    private static string Label(Node node, TypeCatalog types) => $"{node.Name} ({types.NodeTypeName(node.Type)})";

    private static string Count(int count, string noun) => $"{count} {noun}{(count == 1 ? string.Empty : "s")}";
}
