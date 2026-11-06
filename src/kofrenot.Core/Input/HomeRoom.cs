namespace Kofrenot.Core.Input;

/// <summary>
/// The room the home list (the empty input box's) has on screen, and the height of its node rows and
/// of its headers: as many columns as the width allows (each at least <see cref="MinColumnWidth"/>,
/// at most <see cref="MaxColumns"/>), as tall as the height.
/// </summary>
public readonly record struct HomeRoom(double Width, double Height, double RowHeight, double HeaderHeight)
{
    public const double MinColumnWidth = 320;
    public const int MaxColumns = 3;

    public int Columns => Math.Clamp((int)(Width / MinColumnWidth), 1, MaxColumns);

    /// <summary>The most node rows it can hold: the recent nodes worth asking for.</summary>
    public int MaxRows => Columns * Math.Max(1, (int)(Height / Math.Max(RowHeight, 1)));
}
