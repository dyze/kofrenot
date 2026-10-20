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
    /// <summary>The first item of the breadcrumb: where Back returns to (the search, or a view).</summary>
    public string HomeLabel { get; set; } = "⌂ Search";
    private const string PinIcon = "";
    private const string PinnedIcon = "";

    public NodeDetailsPanel()
    {
        InitializeComponent();
        DatasetBox.ViewChanged += FitDatasetRow;
    }

    /// <summary>Raised when the user goes back to the search (Esc at level 1, or ⌂ Search).</summary>
    public event Action? BackToSearch;

    public NodeBrowser Browser { get; } = new();

    public ILog Log { get; set; } = NullLog.Instance;

    /// <summary>Writes a change (a pin) to the vault, logging the action given; false when it could not.</summary>
    public Func<Change, string, bool>? Write { get; set; }

    /// <summary>Called with each node shown at a new level (opened from the search, or from a link).</summary>
    public Action<Node>? Visited { get; set; }

    /// <summary>Opens a node found by the search; for a link row, under the node it comes <paramref name="from"/>.</summary>
    public void Open(KnowledgeGraph graph, Node node, Node? from = null)
    {
        Browser.Open(graph, node, from);
        HideLinkRow();
        if (from is not null)
        {
            Visited?.Invoke(from);
        }

        Visited?.Invoke(node);
        Log.Info("Action", "Node details opened");
        Show();
    }

    /// <summary>Shows the current level again (after a change of the graph, or a move).</summary>
    public void Show()
    {
        if (Browser.Details is not { } details)
        {
            NoteBox.Close();
            DatasetBox.Close();
            return;
        }

        Breadcrumb.ItemsSource = Browser.Trail
            .Select((label, index) => new BreadcrumbItem(label, index + 1, IsCurrent: index + 1 == Browser.Depth))
            .Prepend(new BreadcrumbItem(HomeLabel, 0, IsCurrent: false))
            .ToList();
        NameText.Text = details.Name;
        TypeText.Text = details.Type;
        CreatedText.Text = DateOf(details.Node.Created);
        ModifiedText.Text = Browser.Graph is { } graph ? DateOf(NodeFacts.Of(graph, details.Node).Modified) : string.Empty;
        LinksHeader.Text = details.Links.Count == 0 ? "No links" : $"Links ({details.Links.Count})";
        LinksList.ItemsSource = details.Links;
        ConflictNames.ItemsSource = details.NameConflict;
        PinButton.Content = details.IsPinned ? PinnedIcon : PinIcon;
        PinButton.ToolTip = details.IsPinned ? "Unpin" : "Pin: listed while the input box is empty";
        ConflictPanel.Visibility = details.NameConflict.Count > 0 && !_editing ? Visibility.Visible : Visibility.Collapsed;
        ShowType(details);
        ShowNote(details);
        ShowDataset(details);
        ShowView(details);
    }

    /// <summary>
    /// Up/Down select a link, Enter opens it, Esc goes back, F2 renames. While the name is edited,
    /// Enter renames and Esc cancels. False for another key.
    /// </summary>
    public bool HandleKey(Key key)
    {
        if (DatasetBox.IsReading)
        {
            return HandleDatasetKey(key);
        }

        if (NoteBox.IsEditing)
        {
            return HandleNoteKey(key);
        }

        if (_editing)
        {
            return HandleEditKey(key);
        }

        if (LinkBox.IsKeyboardFocusWithin)
        {
            return HandleLinkKey(key);
        }

        if (HandleDatasetKey(key))
        {
            return true;
        }

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
            case Key.F2:
                StartEditing();
                return true;
            case Key.Escape:
                GoBackTo(Browser.Depth - 1);
                return true;
            default:
                return false;
        }
    }

    private static string DateOf(DateTimeOffset at) => at.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture);

    private void GoBackTo(int depth)
    {
        StopEditing();
        LeaveNote();
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
        StopEditing();
        Log.Info("Action", $"Node details: level {Browser.Depth} opened");
        if (Browser.Details is { } details)
        {
            Visited?.Invoke(details.Node);
        }

        Show();
    }

    private void BreadcrumbItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: BreadcrumbItem item })
        {
            GoBackTo(item.Depth);
        }
    }

    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        if (Browser is { Graph: { } graph, Details: { } details })
        {
            Write?.Invoke(new PinPlanner(graph).Toggle(details.Node), details.IsPinned ? "Node unpinned" : "Node pinned");
            Show();
        }
    }

    /// <summary>A click selects a link, as Up/Down would; a double-click opens its other node.</summary>
    private void Link_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: LinkRow link })
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            if (Browser.OpenLinked(link.OtherNode))
            {
                Opened();
            }
        }
        else if (Browser.Select(link))
        {
            Show();
        }

        e.Handled = true;
    }

    private void OpenLink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: LinkRow link } && Browser.OpenLinked(link.OtherNode))
        {
            Opened();
        }
    }
}
