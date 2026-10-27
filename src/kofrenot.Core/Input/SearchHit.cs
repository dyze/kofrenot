using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

/// <summary>
/// A node found while typing. <see cref="Rank"/> is its place among the nodes of the same name
/// (oldest first), <see cref="Homonyms"/> how many there are. <see cref="Field"/> is the field
/// whose value matched, as "Born: 1920-10-08", when its name did not.
/// </summary>
public sealed record SearchHit(Node Node, int Rank, int Homonyms, bool IsExact, string? Field = null)
{
    private const string SuperscriptDigits = "⁰¹²³⁴⁵⁶⁷⁸⁹";

    /// <summary>Shown after the name when several nodes have it: "¹", "²", ...; empty otherwise.</summary>
    public string Mark => Homonyms > 1 ? Superscript(Rank) : string.Empty;

    public static string Superscript(int number) =>
        string.Concat(number.ToString(System.Globalization.CultureInfo.InvariantCulture).Select(digit => SuperscriptDigits[digit - '0']));
}
