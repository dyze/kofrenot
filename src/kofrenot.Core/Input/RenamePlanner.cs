using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

/// <summary>Why a node cannot be renamed to the text typed.</summary>
public enum RenameProblem
{
    None,

    /// <summary>No name typed.</summary>
    Empty,

    /// <summary>The name is the current one, and there is no conflict to resolve.</summary>
    Unchanged,

    /// <summary>The name contains a link phrase ("relates to"): the input box could not find it.</summary>
    LinkPhrase,
}

/// <summary>
/// Renames <see cref="Node"/> to <see cref="Name"/>. <see cref="Homonyms"/> are the other nodes
/// that already have that name: allowed, but worth a warning.
/// </summary>
public sealed record RenamePlan(Node Node, string Name, RenameProblem Problem, IReadOnlyList<Node> Homonyms, NodeRenamed? Change)
{
    public bool CanRename => Change is not null;

    /// <summary>What Enter does, shown under the name while it is edited.</summary>
    public string Status => Problem switch
    {
        RenameProblem.Empty => "Type a name.",
        RenameProblem.LinkPhrase => "A name cannot contain a link phrase, such as “relates to”.",
        RenameProblem.Unchanged => "Same name. Esc to cancel.",
        _ when Homonyms.Count > 0 => $"“{Name}” exists already{(Homonyms.Count > 1 ? $" ({Homonyms.Count} times)" : string.Empty)}. Enter renames anyway, Esc cancels.",
        _ => "Enter renames, Esc cancels.",
    };
}

/// <summary>
/// Turns a new name into the change that renames a node. The change replaces every current name,
/// so renaming a node in conflict (to one of its names, or another) resolves the conflict.
/// </summary>
public sealed class RenamePlanner(KnowledgeGraph graph, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public RenamePlan Plan(Node node, string text)
    {
        var current = graph.NamesOf(node.Id);
        var (name, problem) = InputParser.Parse(text, graph.Types) switch
        {
            null => (string.Empty, RenameProblem.Empty),
            LinkRequest => (text.Trim(), RenameProblem.LinkPhrase),
            NodeRequest request when current is [{ } only] && only.Name == request.Name => (request.Name, RenameProblem.Unchanged),
            NodeRequest request => (request.Name, RenameProblem.None),
            _ => throw new ArgumentOutOfRangeException(nameof(text), "Unknown request"),
        };

        var homonyms = name.Length == 0 ? [] : graph.NodesNamed(name).Where(other => other.Id != node.Id).ToList();
        var change = problem == RenameProblem.None
            ? new NodeRenamed(Guid.NewGuid(), _clock.GetUtcNow(), node.Id, name, current.Select(version => version.Id).ToList())
            : null;
        return new RenamePlan(node, name, problem, homonyms, change);
    }
}
