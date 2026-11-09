using System.Globalization;
using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Views;

/// <summary>
/// How a query view's rows are sorted: by <see cref="Column"/> (<see cref="NameColumn"/>, then its columns from 1), A→Z, or Z→A when
/// <see cref="Descending"/>. Values are compared as what they are (<see cref="CompareCells"/>); empty cells last; ties by name.
/// </summary>
public sealed record QuerySort(int Column, bool Descending)
{
    public const int NameColumn = 0;

    public static QuerySort ByName { get; } = new(NameColumn, false);

    /// <summary>Sorted by <paramref name="column"/>: A→Z, or Z→A when it already sorts by it A→Z.</summary>
    public QuerySort ClickedOn(int column) => new(column, column == Column && !Descending);

    /// <summary>
    /// Two cells compared: as numbers when both are (<c>200</c> before <c>1965</c>), else as names are (case and accents ignored),
    /// which puts dates (<c>2025-07-01</c>) in order.
    /// </summary>
    public static int CompareCells(string first, string second) =>
        IsNumber(first, out var firstNumber) && IsNumber(second, out var secondNumber)
            ? firstNumber.CompareTo(secondNumber)
            : string.CompareOrdinal(NameKey.Of(first), NameKey.Of(second));

    private static bool IsNumber(string text, out decimal number) =>
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out number);
}
