using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Kofrenot.UI.Views;

/// <summary>
/// The tabs, left to right, as many as fit: the others are left out, listed by ▾ (<see cref="Hidden"/>).
/// The selected tab is always in sight, in place of the last ones that fit.
/// </summary>
public sealed class TabStripPanel : Panel
{
    public static readonly RoutedEvent HiddenChangedEvent = EventManager.RegisterRoutedEvent(
        "HiddenChanged", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TabStripPanel));

    private HashSet<UIElement> _shown = [];

    /// <summary>The tabs left out, in their order, once the layout has run.</summary>
    public IReadOnlyList<TabRow> Hidden { get; private set; } = [];

    /// <summary>HiddenChanged, attached to the items control: raised, after the layout, when the tabs left out change.</summary>
    public static void AddHiddenChangedHandler(DependencyObject element, RoutedEventHandler handler) =>
        ((UIElement)element).AddHandler(HiddenChangedEvent, handler);

    public static void RemoveHiddenChangedHandler(DependencyObject element, RoutedEventHandler handler) =>
        ((UIElement)element).RemoveHandler(HiddenChangedEvent, handler);

    protected override Size MeasureOverride(Size availableSize)
    {
        var tabs = InternalChildren.Cast<UIElement>().ToList();
        foreach (var tab in tabs)
        {
            tab.Measure(new Size(double.PositiveInfinity, availableSize.Height));
        }

        _shown = [.. Fitting(tabs, availableSize.Width)];
        UpdateHidden(tabs);
        var shown = tabs.Where(_shown.Contains).ToList();
        return new Size(shown.Sum(tab => tab.DesiredSize.Width), tabs.Select(tab => tab.DesiredSize.Height).DefaultIfEmpty(0).Max());
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = 0.0;
        foreach (UIElement tab in InternalChildren)
        {
            if (_shown.Contains(tab))
            {
                tab.Arrange(new Rect(x, 0, tab.DesiredSize.Width, finalSize.Height));
                x += tab.DesiredSize.Width;
            }
            else
            {
                tab.Arrange(new Rect(0, 0, 0, 0));
            }
        }

        return finalSize;
    }

    /// <summary>The first tabs that fit in <paramref name="width"/>; the selected one replaces the last of them if it is not one.</summary>
    private static List<UIElement> Fitting(List<UIElement> tabs, double width)
    {
        var fitting = new List<UIElement>();
        var used = 0.0;
        foreach (var tab in tabs.TakeWhile(tab => used + tab.DesiredSize.Width <= width))
        {
            fitting.Add(tab);
            used += tab.DesiredSize.Width;
        }

        if (tabs.FirstOrDefault(IsSelected) is { } selected && !fitting.Contains(selected))
        {
            while (fitting.Count > 0 && used + selected.DesiredSize.Width > width)
            {
                used -= fitting[^1].DesiredSize.Width;
                fitting.RemoveAt(fitting.Count - 1);
            }

            fitting.Add(selected);
        }

        return fitting;
    }

    private static bool IsSelected(UIElement tab) => tab is FrameworkElement { DataContext: TabRow { IsSelected: true } };

    /// <summary>Said after the layout: whoever shows ▾ changes the room left to the tabs.</summary>
    private void UpdateHidden(List<UIElement> tabs)
    {
        var hidden = tabs.Where(tab => !_shown.Contains(tab)).Select(tab => ((FrameworkElement)tab).DataContext).OfType<TabRow>().ToList();
        if (hidden.SequenceEqual(Hidden))
        {
            return;
        }

        Hidden = hidden;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => RaiseEvent(new RoutedEventArgs(HiddenChangedEvent, this)));
    }
}
