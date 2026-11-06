namespace Kofrenot.Core.Input;

/// <summary>
/// Rows laid out top to bottom, then in the next column: the first row of each column
/// (<see cref="Starts"/>), and how many rows are shown (<see cref="Shown"/>), those that fit.
/// </summary>
public sealed record HomeColumns(IReadOnlyList<int> Starts, int Shown)
{
    /// <summary>
    /// Fills a column, then the next, while the room has some: a header goes with the row under it,
    /// never alone at a column's bottom. The first <paramref name="kept"/> rows are all shown, in the
    /// last column if need be; the rest stop at the first that does not fit.
    /// </summary>
    public static HomeColumns Of(IReadOnlyList<bool> headers, HomeRoom room, int kept)
    {
        double HeightOf(int index) => headers[index] ? room.HeaderHeight : room.RowHeight;

        var starts = new List<int> { 0 };
        var used = 0.0;
        for (var index = 0; index < headers.Count; index++)
        {
            var needed = HeightOf(index) + (headers[index] && index + 1 < headers.Count ? HeightOf(index + 1) : 0);
            if (used > 0 && used + needed > room.Height)
            {
                if (starts.Count < room.Columns)
                {
                    starts.Add(index);
                    used = 0;
                }
                else if (index >= kept)
                {
                    return new HomeColumns(starts, index);
                }
            }

            used += HeightOf(index);
        }

        return new HomeColumns(starts, headers.Count);
    }

    /// <summary>
    /// Left / Right: in the column <paramref name="step"/> beside the row at <paramref name="index"/>, the
    /// row it can select level with it, or the nearest (the upper one when two are). Null: no such column, or no row in it.
    /// </summary>
    public static int? Beside(IReadOnlyList<int> starts, IReadOnlyList<bool> selectable, int index, int step)
    {
        var column = starts.Count(start => start <= index) - 1;
        var target = column + step;
        if (target < 0 || target >= starts.Count)
        {
            return null;
        }

        var first = starts[target];
        var end = target + 1 < starts.Count ? starts[target + 1] : selectable.Count;
        var level = first + index - starts[column];
        return Enumerable.Range(first, Math.Max(0, end - first))
            .Where(row => selectable[row])
            .OrderBy(row => Math.Abs(row - level))
            .Select(row => (int?)row)
            .FirstOrDefault();
    }
}
