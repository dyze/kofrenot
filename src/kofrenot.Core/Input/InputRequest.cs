using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

/// <summary>What the text of the input box asks for.</summary>
public abstract record InputRequest;

/// <summary>A node: <c>Dune</c>.</summary>
public sealed record NodeRequest(string Name) : InputRequest;

/// <summary>
/// A link: <c>Dune relates to Frank Herbert</c>. A reverse <see cref="Verb"/> (<c>Arrakis contained by
/// Dune</c>) links from <see cref="Right"/> to <see cref="Left"/>. <see cref="Right"/> is empty while it is being typed.
/// </summary>
public sealed record LinkRequest(string Left, LinkVerb Verb, string Right) : InputRequest;
