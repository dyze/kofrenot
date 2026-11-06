using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

/// <summary>
/// What the input box asks to list rather than to add: the nodes of a <see cref="Type"/> (<c>person:</c>, whose
/// names match <see cref="Name"/>: <c>person: Frank</c>), linked to a node (<see cref="Link"/>: <c>contained by Kitchen</c>),
/// or by when (<see cref="Time"/>: <c>changed: this week</c>); a type in front narrows the others (<c>book: written by Frank Herbert</c>).
/// </summary>
public sealed record NodeQuery(NodeType? Type, string Name, LinkQuery? Link, TimeQuery? Time)
{
    /// <summary><c>?</c> alone: the ways to start a query are listed (<see cref="QueryCompletion.Starts"/>).</summary>
    public bool IsEmpty => Type is null && Name.Length == 0 && Link is null && Time is null;
}

/// <summary>
/// The nodes that read <see cref="Verb"/> to a node named <see cref="Node"/>: <c>written by Frank Herbert</c>. While no node has
/// that name, those whose names match it; with none typed, any node.
/// </summary>
public sealed record LinkQuery(LinkVerb Verb, string Node);
