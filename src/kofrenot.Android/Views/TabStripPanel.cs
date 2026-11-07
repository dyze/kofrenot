using Avalonia;
using Avalonia.Controls;
using Kofrenot.Core.Views;

namespace Kofrenot.Phone.Views;

/// <summary>
/// The tabs, left to right, as many as fit whole (<see cref="TabFit"/>), the <see cref="Selected"/> one
/// always in sight: the others are left out (<see cref="Hidden"/>), listed by ▾, as on Windows.
/// </summary>
public sealed class TabStripPanel : Panel
{
    private HashSet<Control> _shown = [];
    private Control? _selected;

    /// <summary>The tab selected, kept in sight.</summary>
    public Control? Selected
    {
        get => _selected;
        set
        {
            _selected = value;
            InvalidateMeasure();
        }
    }

    /// <summary>The tabs left out, in their order, once the layout has run.</summary>
    public IReadOnlyList<Control> Hidden => Children.Where(tab => !_shown.Contains(tab)).ToList();

    protected override Size MeasureOverride(Size availableSize)
    {
        var tabs = Children.ToList();
        foreach (var tab in tabs)
        {
            tab.Measure(new Size(double.PositiveInfinity, availableSize.Height));
        }

        var selected = _selected is null ? -1 : tabs.IndexOf(_selected);
        _shown = [.. TabFit.Fitting(tabs.Select(tab => tab.DesiredSize.Width).ToList(), selected, availableSize.Width).Select(index => tabs[index])];
        return new Size(_shown.Sum(tab => tab.DesiredSize.Width), tabs.Select(tab => tab.DesiredSize.Height).DefaultIfEmpty(0).Max());
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = 0.0;
        foreach (var tab in Children)
        {
            // A tab left out takes no room; transparent, as its content could draw past a zero size.
            var isShown = _shown.Contains(tab);
            tab.Arrange(isShown ? new Rect(x, 0, tab.DesiredSize.Width, finalSize.Height) : default);
            tab.Opacity = isShown ? 1 : 0;
            tab.IsHitTestVisible = isShown;
            x += isShown ? tab.DesiredSize.Width : 0;
        }

        return finalSize;
    }
}
