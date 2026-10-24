using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Datasets;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Logging;

namespace Kofrenot.UI.Views;

/// <summary>
/// Details (a toggle on the bar, kept per view in <see cref="ViewStates"/>): each row shows its
/// node's type, size and last change (<see cref="NodeFacts"/>) in columns, under a header naming
/// them. A tree's rows are as wide as the tree less their indent, so the columns line up at every
/// depth; a list's rows stretch by themselves.
/// </summary>
public partial class ViewPanel
{
    private const double TypeWidth = 130;
    private const double SizeWidth = 90;
    private const double ModifiedWidth = 150;

    // The room a tree row leaves on its right: the scroll bar, and the row's own padding.
    private const double TreeRowRightRoom = 24;

    // The rows with details in the tree, fitted again when it is resized.
    private readonly List<Grid> _detailRows = [];

    private bool ShowsDetails => _view is not null && States?.ShowsDetails(_vault, _view.Id) == true;

    private void Details_Click(object sender, RoutedEventArgs e)
    {
        if (_view is null || States is null)
        {
            return;
        }

        var showsDetails = DetailsButton.IsChecked == true;
        States.SetShowsDetails(_vault, _view.Id, showsDetails);
        Log.Info("Action", showsDetails ? "View details shown" : "View details hidden");
        Refresh();
    }

    /// <summary>A row with its columns: the name (cut short when too long), then type, size and last change.</summary>
    private Grid WithDetails(Node node, StackPanel name)
    {
        var facts = NodeFacts.Of(_graph!, node);
        var row = new Grid();
        foreach (var width in new[] { new GridLength(1, GridUnitType.Star), new GridLength(TypeWidth), new GridLength(SizeWidth), new GridLength(ModifiedWidth) })
        {
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = width });
        }

        row.Children.Add(name);
        AddCell(row, 1, facts.Type);
        AddCell(row, 2, facts.Size is { } size ? ByteSize.Format(size, CultureInfo.CurrentCulture) : string.Empty, HorizontalAlignment.Right);
        AddCell(row, 3, facts.Modified.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture));
        row.Loaded += (_, _) => FitToTree(row);
        _detailRows.Add(row);
        return row;
    }

    private static void AddCell(Grid row, int column, string text, HorizontalAlignment alignment = HorizontalAlignment.Left)
    {
        var cell = new TextBlock
        {
            Text = text,
            Opacity = 0.75,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = alignment,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = alignment == HorizontalAlignment.Right ? new Thickness(0, 0, 16, 0) : new Thickness(0),
        };
        Grid.SetColumn(cell, column);
        row.Children.Add(cell);
    }

    /// <summary>A tree row as wide as the tree from where it starts (its indent), so its columns end where the others do.</summary>
    private void FitToTree(Grid row)
    {
        if (!Tree.IsAncestorOf(row))
        {
            return;
        }

        var start = row.TranslatePoint(new Point(0, 0), Tree).X;
        row.Width = Math.Max(TypeWidth + SizeWidth + ModifiedWidth, ViewportWidthOf(Tree) - start - TreeRowRightRoom);
    }

    private void Tree_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        foreach (var row in _detailRows)
        {
            FitToTree(row);
        }
    }

    /// <summary>The width a tree shows its rows in: its scroll viewer's, without the scroll bar.</summary>
    private static double ViewportWidthOf(FrameworkElement tree) =>
        FindScrollViewer(tree) is { ViewportWidth: > 0 } viewer ? viewer.ViewportWidth : tree.ActualWidth;

    private static ScrollViewer? FindScrollViewer(DependencyObject parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if ((child as ScrollViewer ?? FindScrollViewer(child)) is { } viewer)
            {
                return viewer;
            }
        }

        return null;
    }

    /// <summary>The picture and name of a row, with or without its details, or of a card.</summary>
    private static StackPanel? NamePanelOf(object? header) => header switch
    {
        StackPanel name => name,
        Grid { Children: [StackPanel name, ..] } => name,
        Border { Child: DockPanel { Children: [StackPanel name, ..] } } => name,
        _ => null,
    };
}
