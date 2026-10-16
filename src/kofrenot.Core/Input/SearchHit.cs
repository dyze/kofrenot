using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

/// <summary>
/// A node found while typing. <see cref="Rank"/> is its place among the nodes of the same name
/// (oldest first), <see cref="Homonyms"/> how many there are.
/// </summary>
public sealed record SearchHit(Node Node, int Rank, int Homonyms, bool IsExact)
{
    private const string SuperscriptDigits = "⁰¹²³⁴⁵⁶⁷⁸⁹";

    /// <summary>Shown after the name when several nodes have it: "¹", "²", ...; empty otherwise.</summary>
    public string Mark => Homonyms > 1 ? Superscript(Rank) : string.Empty;

    public static string Superscript(int number) =>
        string.Concat(number.ToString(System.Globalization.CultureInfo.InvariantCulture).Select(digit => SuperscriptDigits[digit - '0']));
}
