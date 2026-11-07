namespace Kofrenot.Core.Views;

/// <summary>
/// Which tabs a strip shows, left to right, as many as fit: the others are left out (listed by ▾).
/// The selected tab is always in sight, in place of the last ones that fit.
/// </summary>
public static class TabFit
{
    /// <summary>The indexes of the tabs shown, in order.</summary>
    /// <param name="widths">The width of each tab, in order.</param>
    /// <param name="selected">The index of the selected tab; null or out of range when none is.</param>
    /// <param name="room">The width the strip has.</param>
    public static IReadOnlyList<int> Fitting(IReadOnlyList<double> widths, int? selected, double room)
    {
        var fitting = new List<int>();
        var used = 0.0;
        for (var index = 0; index < widths.Count && used + widths[index] <= room; index++)
        {
            fitting.Add(index);
            used += widths[index];
        }

        if (selected is { } shown && shown >= 0 && shown < widths.Count && !fitting.Contains(shown))
        {
            while (fitting.Count > 0 && used + widths[shown] > room)
            {
                used -= widths[fitting[^1]];
                fitting.RemoveAt(fitting.Count - 1);
            }

            fitting.Add(shown);
        }

        return fitting;
    }
}
