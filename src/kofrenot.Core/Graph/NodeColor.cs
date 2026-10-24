namespace Kofrenot.Core.Graph;

/// <summary>The colours a node can be given, from a fixed palette; each app draws them in its own shades.</summary>
public enum NodeColor
{
    None,
    Yellow,
    Green,
    Blue,
    Pink,
    Purple,
    Teal,
    Grey,
}

/// <summary>A colour's version: the colour change (<see cref="Id"/>), its colour, and when it was given.</summary>
public sealed record ColorVersion(Guid Id, NodeColor Color, DateTimeOffset At);

/// <summary>How a colour is written in a <see cref="NodeColored"/> change: its name in lower case, none as null.</summary>
public static class NodeColorNames
{
    /// <summary>The name written for <paramref name="color"/> ("yellow"); null for none.</summary>
    public static string? Of(NodeColor color) => color == NodeColor.None ? null : color.ToString().ToLowerInvariant();

    /// <summary>The colour of a name; none for null, or for a name this version does not know (a later palette's).</summary>
    public static NodeColor Parse(string? name) =>
        Enum.TryParse<NodeColor>(name, ignoreCase: true, out var color) && Enum.IsDefined(color) && name!.All(char.IsAsciiLetter) ? color : NodeColor.None;
}
