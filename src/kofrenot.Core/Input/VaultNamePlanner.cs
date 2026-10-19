using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

/// <summary>Why the vault cannot be named with the text typed.</summary>
public enum VaultNameProblem
{
    None,

    /// <summary>No name typed.</summary>
    Empty,

    /// <summary>The name is the current one, and there is no conflict to resolve.</summary>
    Unchanged,
}

/// <summary>Names the vault <see cref="Name"/>; <see cref="Change"/> is null when it cannot (<see cref="Problem"/>).</summary>
public sealed record VaultNamePlan(string Name, VaultNameProblem Problem, VaultNamed? Change);

/// <summary>
/// Turns a name into the change that names the vault. The change replaces every current name, so
/// naming a vault in conflict (with one of its names, or another) resolves the conflict.
/// </summary>
public sealed class VaultNamePlanner(KnowledgeGraph graph, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public VaultNamePlan Plan(string text)
    {
        var name = text.Trim();
        var current = graph.VaultNames;
        var problem = name.Length == 0 ? VaultNameProblem.Empty
            : current is [{ } only] && only.Name == name ? VaultNameProblem.Unchanged
            : VaultNameProblem.None;
        var change = problem == VaultNameProblem.None
            ? new VaultNamed(Guid.NewGuid(), _clock.GetUtcNow(), name, current.Select(version => version.Id).ToList())
            : null;
        return new VaultNamePlan(name, problem, change);
    }
}
