using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

/// <summary>Why a phrase cannot become an alias.</summary>
public enum AliasProblem
{
    None,

    /// <summary>No phrase typed.</summary>
    Empty,

    /// <summary>The phrase names a link type already (<see cref="AliasPlan.TakenBy"/>).</summary>
    Taken,
}

/// <summary>
/// Adds <see cref="Phrase"/> as an alias of <see cref="Verb"/>. <see cref="Hidden"/> are the nodes
/// whose name contains it: the input box would read them as links, so they could no longer be typed.
/// </summary>
public sealed record AliasPlan(LinkVerb Verb, string Phrase, AliasProblem Problem, LinkVerb? TakenBy, IReadOnlyList<Node> Hidden, AliasAdded? Change)
{
    public bool CanAdd => Change is not null;

    /// <summary>What Add does, or why it cannot.</summary>
    public string Status => Problem switch
    {
        AliasProblem.Empty => "Type a phrase.",
        AliasProblem.Taken => $"“{Phrase}” is a phrase of “{TakenBy!.Name}” already.",
        _ when Hidden.Count > 0 => $"Adds “{Phrase}” to “{Verb.Name}”. {Hidden.Count} node name{(Hidden.Count > 1 ? "s contain" : " contains")} it: the input box could no longer find {(Hidden.Count > 1 ? "them" : "it")}.",
        _ => $"Adds “{Phrase}” to “{Verb.Name}”.",
    };
}

/// <summary>Turns a phrase into the change adding it as an alias, and an alias into the change removing it.</summary>
public sealed class AliasPlanner(KnowledgeGraph graph, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public AliasPlan Plan(LinkVerb verb, string text)
    {
        var phrase = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (phrase.Length == 0)
        {
            return new AliasPlan(verb, phrase, AliasProblem.Empty, TakenBy: null, Hidden: [], Change: null);
        }

        var key = NameKey.Of(phrase);
        if (graph.Types.Verbs.FirstOrDefault(other => other.Phrases.Any(taken => NameKey.Of(taken) == key)) is { } takenBy)
        {
            return new AliasPlan(verb, phrase, AliasProblem.Taken, takenBy, Hidden: [], Change: null);
        }

        var alone = new TypeCatalog([], [new LinkType(verb.Type.Id, [phrase])]);
        var hidden = graph.Nodes.Where(node => InputParser.Parse(node.Name, alone) is LinkRequest).ToList();
        var change = new AliasAdded(Guid.NewGuid(), _clock.GetUtcNow(), verb.Type.Id, verb.IsReverse, phrase);
        return new AliasPlan(verb, phrase, AliasProblem.None, TakenBy: null, hidden, change);
    }

    /// <summary>Removes an alias, with any copy of it (the same phrase added on two devices).</summary>
    public ChangesRemoved Remove(LinkAlias alias) => new(
        Guid.NewGuid(),
        _clock.GetUtcNow(),
        graph.Aliases.All.Where(other => other.Verb == alias.Verb && NameKey.Of(other.Phrase) == NameKey.Of(alias.Phrase)).Select(other => other.Id).ToList());
}
