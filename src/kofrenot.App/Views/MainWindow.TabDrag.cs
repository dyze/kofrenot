using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kofrenot.Core.Logging;

namespace Kofrenot.UI.Views;

/// <summary>
/// A view's tab dragged among the others changes their order, kept on this device (<see cref="Kofrenot.Core.Browse.ViewTabs"/>);
/// a bar shows where it goes. Home stays first: dropped on, it puts the tab first.
/// </summary>
public partial class MainWindow
{
    private const string TabDragFormat = "kofrenot.ViewTab";

    private (Point At, Guid View)? _tabPressed;

    /// <summary>Pressed on a view's tab: dragged far enough, it starts moving (the press has selected it).</summary>
    private void PressTab(TabRow row, MouseButtonEventArgs e) =>
        _tabPressed = row.Tab.View is { } view ? (e.GetPosition(TabsList), view.Id) : null;

    private void TabsList_MouseMove(object sender, MouseEventArgs e)
    {
        if (_tabPressed is not { } pressed || e.LeftButton != MouseButtonState.Pressed)
        {
            _tabPressed = null;
            return;
        }

        var moved = e.GetPosition(TabsList) - pressed.At;
        if (Math.Abs(moved.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(moved.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _tabPressed = null;
        DragDrop.DoDragDrop(TabsList, new DataObject(TabDragFormat, pressed.View), DragDropEffects.Move);
    }

    private void Tab_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.None;
        if (TabDropOf(sender, e) is { } drop)
        {
            ShowTabDrop(drop.Tab, drop.Before);
            e.Effects = DragDropEffects.Move;
        }

        e.Handled = true;
    }

    private void Tab_DragLeave(object sender, DragEventArgs e)
    {
        if (sender is Border tab)
        {
            ShowTabDrop(tab, before: null);
        }
    }

    private void Tab_Drop(object sender, DragEventArgs e)
    {
        if (TabDropOf(sender, e) is not { } drop || _session.Current is not { } vault)
        {
            return;
        }

        ShowTabDrop(drop.Tab, before: null);
        _tabs.Move(vault.Id, drop.View, BeforeView(drop.Row, drop.Before));
        _log.Info("Action", "View tab moved");
        ShowVault();
        e.Handled = true;
    }

    /// <summary>A view's tab dragged over a tab, and whether it goes before it (Home: always after); null otherwise.</summary>
    private static (Border Tab, TabRow Row, Guid View, bool Before)? TabDropOf(object sender, DragEventArgs e) =>
        sender is Border { DataContext: TabRow row } tab && e.Data.GetData(TabDragFormat) is Guid view
            ? (tab, row, view, !row.Tab.IsSearch && e.GetPosition(tab).X < tab.ActualWidth / 2)
            : null;

    /// <summary>The view the dragged one goes before: the tab dropped on, or the one after it (null: last).</summary>
    private Guid? BeforeView(TabRow target, bool before)
    {
        var views = ((IEnumerable<TabRow>)TabsList.ItemsSource).Select(row => row.Tab.View?.Id).ToList();
        var index = views.IndexOf(target.Tab.View?.Id) + (before ? 0 : 1);
        return index < views.Count ? views[index] : null;
    }

    /// <summary>The bar before or after a tab; none for null.</summary>
    private static void ShowTabDrop(Border tab, bool? before)
    {
        if (tab.TemplatedParent is not ContentPresenter presenter)
        {
            return;
        }

        Bar("DropBefore").Visibility = before == true ? Visibility.Visible : Visibility.Collapsed;
        Bar("DropAfter").Visibility = before == false ? Visibility.Visible : Visibility.Collapsed;

        FrameworkElement Bar(string name) => (FrameworkElement)presenter.ContentTemplate.FindName(name, presenter);
    }
}
