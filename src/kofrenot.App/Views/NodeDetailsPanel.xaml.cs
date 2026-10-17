using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Logging;

namespace Kofrenot.UI.Views;

/// <summary>An item of the breadcrumb: depth 0 is the search, then each node opened.</summary>
public sealed record BreadcrumbItem(string Label, int Depth, bool IsCurrent);

/// <summary>
/// The details screen of the main window: a view of a <see cref="NodeBrowser"/>, with its
/// breadcrumb. <see cref="BackToSearch"/> is raised when it closes.
/// </summary>
public partial class NodeDetailsPanel : UserControl
{
    private const string SearchLabel = "⌂ Search";

    public NodeDetailsPanel() => InitializeComponent();

    /// <summary>Raised when the user goes back to the search (Esc at level 1, or ⌂ Search).</summary>
    public event Action? BackToSearch;

    public NodeBrowser Browser { get; } = new();

    public ILog Log { get; set; } = NullLog.Instance;

    /// <summary>Opens a node found by the search; for a link row, under the node it comes <paramref name="from"/>.</summary>
    public void Open(KnowledgeGraph graph, Node node, Node? from = null)
    {
        Browser.Open(graph, node, from);
        Log.Info("Action", "Node details opened");
        Show();
    }

    /// <summary>Shows the current level again (after a change of the graph, or a move).</summary>
    public void Show()
    {
        if (Browser.Details is not { } details)
        {
            return;
        }

        Breadcrumb.ItemsSource = Browser.Trail
            .Select((label, index) => new BreadcrumbItem(label, index + 1, IsCurrent: index + 1 == Browser.Depth))
            .Prepend(new BreadcrumbItem(SearchLabel, 0, IsCurrent: false))
            .ToList();
        NameText.Text = details.Name;
        TypeText.Text = details.Type;
        CreatedText.Text = details.Node.Created.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture);
        LinksHeader.Text = details.Links.Count == 0 ? "No links" : $"Links ({details.Links.Count})";
        LinksList.ItemsSource = details.Links;
    }

    /// <summary>Up/Down select a link, Enter opens it, Esc goes back. False for another key.</summary>
    public bool HandleKey(Key key)
    {
        switch (key)
        {
            case Key.Up or Key.Down:
                if (Browser.MoveSelection(key == Key.Up ? -1 : 1))
                {
                    Show();
                }

                return true;
            case Key.Enter:
                if (Browser.OpenSelected())
                {
                    Opened();
                }

                return true;
            case Key.Escape:
                GoBackTo(Browser.Depth - 1);
                return true;
            default:
                return false;
        }
    }

    private void GoBackTo(int depth)
    {
        Browser.BackTo(depth);
        Log.Info("Action", $"Node details: back to level {Browser.Depth}");
        if (Browser.IsOpen)
        {
            Show();
        }
        else
        {
            BackToSearch?.Invoke();
        }
    }

    private void Opened()
    {
        Log.Info("Action", $"Node details: level {Browser.Depth} opened");
        Show();
    }

    private void BreadcrumbItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: BreadcrumbItem item })
        {
            GoBackTo(item.Depth);
        }
    }

    private void OpenLink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: LinkRow link } && Browser.OpenLinked(link.OtherNode))
        {
            Opened();
        }
    }
}
