using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;

namespace Kofrenot.UI.Views;

/// <summary>A node a tree can start from, in the view step's list: its name (marked among homonyms) and type.</summary>
public sealed record RootChoice(Node Node, string Label, NodeVisual Visual);

/// <summary>A new view's step: a tree from a node found by name, through a link type, or a list of a node type.</summary>
public partial class CreationDialog
{
    private void ShowView(Side side)
    {
        var isView = _form.IsView(side);
        ViewPanel.Visibility = isView ? Visibility.Visible : Visibility.Collapsed;
        if (!isView)
        {
            return;
        }

        var view = _form.ViewOf(side);
        var tree = view.Display == ViewDisplay.Tree;
        DisplayBox.SelectedIndex = tree ? 0 : 1;
        foreach (var element in new UIElement[] { RootLabel, RootPanel, ThroughLabel, ViewLinkBox })
        {
            element.Visibility = tree ? Visibility.Visible : Visibility.Collapsed;
        }

        NodeTypeLabel.Visibility = ViewTypeBox.Visibility = tree ? Visibility.Collapsed : Visibility.Visible;
        ViewLinkBox.SelectedItem = view.LinkType;
        ViewTypeBox.SelectedItem = view.NodeType;
        RootList.SelectedItem = RootChoices.FirstOrDefault(choice => choice.Node.Id == view.Root?.Id);
    }

    private IEnumerable<RootChoice> RootChoices => RootList.ItemsSource as IEnumerable<RootChoice> ?? [];

    private void ChangeView(Func<ViewDraft, ViewDraft> change)
    {
        if (_showing || StepSide is not { } side)
        {
            return;
        }

        _form.SetView(side, change(_form.ViewOf(side)));
        ShowForm();
    }

    /// <summary>The nodes found for the typed name; the first one is chosen when none is.</summary>
    private void RootBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RootList.ItemsSource = _form.FindRoots(RootBox.Text)
            .Select(hit => new RootChoice(hit.Node, $"{hit.Node.Name}{hit.Mark} ({_form.Types.NodeTypeName(hit.Node.Type)})", NodeVisual.OfType(hit.Node.Type)))
            .ToList();
        ChangeView(view => view with { Root = RootChoices.FirstOrDefault(choice => choice.Node.Id == view.Root?.Id)?.Node ?? RootChoices.FirstOrDefault()?.Node });
    }

    /// <summary>Up/Down in the name choose among the nodes found.</summary>
    private void RootBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var step = e.Key switch { Key.Up => -1, Key.Down => 1, _ => 0 };
        if (step != 0 && RootList.Items.Count > 0)
        {
            RootList.SelectedIndex = Math.Clamp(RootList.SelectedIndex + step, 0, RootList.Items.Count - 1);
            RootList.ScrollIntoView(RootList.SelectedItem);
            e.Handled = true;
        }
    }

    private void RootList_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ChangeView(view => view with { Root = (RootList.SelectedItem as RootChoice)?.Node });

    private void DisplayBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ChangeView(view => view with { Display = DisplayBox.SelectedIndex == 1 ? ViewDisplay.Flat : ViewDisplay.Tree });

    private void ViewLinkBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewLinkBox.SelectedItem is LinkType type)
        {
            ChangeView(view => view with { LinkType = type });
        }
    }

    private void ViewTypeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewTypeBox.SelectedItem is NodeType type)
        {
            ChangeView(view => view with { NodeType = type });
        }
    }
}
