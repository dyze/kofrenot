using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Datasets;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Notes;
using Kofrenot.Core.Storage;
using Kofrenot.Core.Views;
using Kofrenot.Phone.Platform;

namespace Kofrenot.Phone.Views;

/// <summary>A version of a dataset's file: its name and size, to open with another app or save.</summary>
public sealed record FileRow(DatasetEdited Version, string Name, string Size);

/// <summary>A field shown on a node's page: its label, its value as shown, an image field's image and thumbnail, and the field it shows.</summary>
public sealed record FieldLine(string Label, string Value, DatasetEdited? Image = null, Bitmap? Thumbnail = null, NodeField? Field = null)
{
    public bool HasImage => Image is not null;

    public bool HasThumbnail => Thumbnail is not null;
}

/// <summary>
/// A node's details, as in the main window: name, type, colour, links read from its side; a tap
/// on a link opens that node as a screen of its own, so Back goes through them one by one (the
/// trail at the top: the screens before). A note's content is shown, fetched when not here; a
/// dataset's file is opened with another app or saved, downloaded first when not here; a view
/// opens as a tab.
/// </summary>
public sealed partial class NodePage : UserControl, IPage
{
    private readonly Shell _shell;
    private readonly NodeBrowser _browser = new();

    public NodePage(Shell shell, Node node)
    {
        _shell = shell;
        InitializeComponent();
        if (Vault is { } vault)
        {
            _browser.Open(vault.Graph, node);
            Opened(vault, node);
        }

        Refresh();
    }

    private Vault? Vault => _shell.Services.Session.Current;

    /// <summary>The node's name, told from its homonyms, for the trail.</summary>
    public string Label => _browser.Trail.FirstOrDefault() ?? string.Empty;

    // A note changed and not saved: asked before leaving.
    public bool GoBack() => AsksBeforeLeaving(() => _shell.Back());

    public void SyncChanged()
    {
    }

    public void Refresh()
    {
        // The graph read again: a sync may have changed the node, or removed it (then the screen before).
        if (Vault is not { } vault || _browser.Details?.Node is not { } open)
        {
            return;
        }

        if (vault.Graph.FindNode(open.Id) is null)
        {
            if (_shell.IsShown(this))
            {
                _shell.Back();
            }

            return;
        }

        if (_browser.Graph != vault.Graph)
        {
            _browser.Open(vault.Graph, open);
        }

        var details = _browser.Details!;
        Trail.Text = string.Join(" › ", _shell.PagesUpTo(this).Select(LabelOf).OfType<string>());
        NodeName.Text = details.Name;
        NodeType.Text = details.Type;
        ColorDot.Fill = NodeLook.Brush(details.Color);
        ColorDot.IsVisible = details.Color != NodeColor.None;
        Pinned.IsVisible = details.IsPinned;
        ShowOffline(vault, details.Node);
        ShowFields(details);
        ShowConflicts(details);
        ShowContent(vault, details.Node, details.Color);
        Links.ItemsSource = details.Links.Select(link => NodeRow.Of(vault.Graph, link.OtherNode, link.Type, link: link.Link)).ToList();
        LinksHeader.IsVisible = details.Links.Count > 0;
    }

    // The fields of its types and its own, empty ones of its types included, as on Windows.
    private void ShowFields(NodeDetails details)
    {
        Fields.ItemsSource = details.Fields.Select(field => LineOf(field)).ToList();
        Fields.IsVisible = details.Fields.Count > 0;
    }

    // An image's thumbnail is read from the vault (a server vault downloads it, then the page is shown again).
    private FieldLine LineOf(NodeField field)
    {
        if (field.Image?.Image is not { } image)
        {
            return new FieldLine(field.Label, field.HasValue ? field.Display : "—", Field: field);
        }

        var png = Vault?.ReadThumbnail(image.Id);
        using var thumbnail = png is null ? null : new MemoryStream(png);
        return new FieldLine(field.Label, field.Display, image, thumbnail is null ? null : new Bitmap(thumbnail), field);
    }

    private void ShowContent(Vault vault, Node node, NodeColor color)
    {
        NoteBox.IsVisible = ContentText.IsVisible = Files.IsVisible = OpenViewButton.IsVisible = ThumbnailFrame.IsVisible = false;
        var graph = vault.Graph;
        if (graph.NoteVersionsOf(node.Id).Count > 0 || node.Type == TypeCatalog.Note.Id)
        {
            ShowNote(vault, node, color);
        }
        else if (graph.DatasetVersionsOf(node.Id) is { Count: > 0 } datasets)
        {
            if (datasets.Count > 1)
            {
                ShowText($"{datasets.Count} versions, in conflict: resolved in kofrenot on Windows.");
            }

            Files.ItemsSource = datasets.Select(version => new FileRow(version, version.File.Name, ByteSize.Format(version.File.Size))).ToList();
            Files.IsVisible = true;
            ShowThumbnail(datasets[^1]);
        }
        else if (node.Type == TypeCatalog.View.Id)
        {
            OpenViewButton.IsVisible = true;
        }
    }

    private void ShowText(string text)
    {
        ContentText.Text = text;
        ContentText.IsVisible = true;
    }

    private void Opened(Vault vault, Node node) => _shell.Services.Recent.Opened(vault.Id, node.Id);

    private static string? LabelOf(Control page) => page switch
    {
        HomePage => "Home",
        NodePage node => node.Label,
        _ => null,
    };

    // A screen of its own: this one stays as it is, a note being edited included, until Back comes to it.
    private void Link_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.Tag is NodeRow row)
        {
            _shell.Push(new NodePage(_shell, row.Node));
        }
    }

    private void OpenView_Click(object? sender, RoutedEventArgs e)
    {
        if (Vault is { } vault && _browser.Details?.Node is { } view)
        {
            _shell.Services.ViewTabs.Open(vault.Id, view.Id);
            _shell.Show(new HomePage(_shell, new Tab(view)));
        }
    }
}
