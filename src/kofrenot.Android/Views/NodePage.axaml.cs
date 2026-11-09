using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Datasets;
using Kofrenot.Core.Fields;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Notes;
using Kofrenot.Core.Storage;
using Kofrenot.Core.Views;
using Kofrenot.Phone.Platform;

namespace Kofrenot.Phone.Views;

/// <summary>A screen of the trail at the top, as a chip, the home with its icon; a tap goes back to it, but not on the current one.</summary>
public sealed record TrailChip(Control Page, string Label, bool IsFirst, bool IsCurrent)
{
    public bool IsHome => Page is HomePage;
}

/// <summary>A version of a dataset's file: its name and size, to open with another app or save; <paramref name="CanImport"/>: Import… in its row too (the only version, not read only).</summary>
public sealed record FileRow(DatasetEdited Version, string Name, string Size, bool CanImport = false);

/// <summary>A field shown on a node's page: its label, its value as shown, an image field's image and thumbnail, and the field it shows.</summary>
public sealed record FieldLine(string Label, string Value, DatasetEdited? Image = null, Bitmap? Thumbnail = null, NodeField? Field = null)
{
    /// <summary>What its value opens: a web address, a mail to write, a map, a call (<see cref="FieldValue.LinkOf"/>); null for the other kinds.</summary>
    public Uri? Link => Field is { Value: { } value } shown ? FieldValue.LinkOf(shown.Kind, value) : null;

    public bool HasLink => Link is not null;

    /// <summary>Its button's icon, as Windows' tooltip says what it does: a mail to write, a place on a map, a number to call, a page to open.</summary>
    public Geometry LinkIcon => PhoneTheme.Icon(Field?.Kind switch
    {
        FieldKind.Email => "mail",
        FieldKind.Address => "place",
        FieldKind.Phone => "phone",
        _ => "open",
    });

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
    private readonly Guid _nodeId;

    public NodePage(Shell shell, Node node)
    {
        _shell = shell;
        _nodeId = node.Id;
        InitializeComponent();
        TaskEditor.Changed += TaskEditor_Changed;
        AttachLinkBox();
        if (Vault is { } vault)
        {
            _browser.Open(vault.Graph, node);
            Opened(vault, node);
        }

        Refresh();
    }

    private Vault? Vault => _shell.Services.Session.Current;

    /// <summary>The node shown; null once removed.</summary>
    public Node? Shown => _browser.Details?.Node;

    /// <summary>The node's name, told from its homonyms, for the trail.</summary>
    public string Label => _browser.Trail.FirstOrDefault() ?? string.Empty;

    // Back leaves the screen: a note changed is saved (Leave).
    public bool GoBack() => false;

    public void SyncChanged()
    {
    }

    public void Refresh()
    {
        // The graph read again: a sync may have changed the node, or removed it, as a delete here does (then the screen before).
        if (Vault is not { } vault)
        {
            return;
        }

        if (vault.Graph.FindNode(_nodeId) is not { } open)
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

        var pages = _shell.PagesUpTo(this).ToList();
        _browser.Before = pages.OfType<NodePage>().Where(page => page != this).Select(page => page._nodeId).ToHashSet();
        var details = _browser.Details!;
        ShowTrail(pages);
        NodeName.Text = details.Name;
        NodeType.Text = details.Type;
        ColorDot.Fill = NodeLook.Brush(details.Color);
        ColorDot.IsVisible = details.Color != NodeColor.None;
        Pinned.IsVisible = details.IsPinned;
        ShowOffline(details.Node);
        ShowFields(details);
        ShowConflicts(details);
        ShowContent(vault, details.Node, details.Color);
        Links.ItemsSource = details.Links.Select(link => NodeRow.Of(vault.Graph, link.OtherNode, link.Type + link.QualifierSuffix, link: link.Link) with { CanOpen = link.CanOpen }).ToList();
        LinksHeader.IsVisible = details.Links.Count > 0 || !IsReadOnly;
        AddLinkButton.IsVisible = !IsReadOnly && !LinkPanel.IsVisible;
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
        NoteBox.IsVisible = ContentText.IsVisible = Files.IsVisible = ImportButton.IsVisible = OpenViewButton.IsVisible = ThumbnailFrame.IsVisible = false;
        var graph = vault.Graph;
        // No longer a note (its type changed): nothing to save when left.
        var isNote = graph.NoteVersionsOf(node.Id).Count > 0 || node.Type == TypeCatalog.Note.Id;
        _note = isNote ? _note : null;
        if (isNote)
        {
            ShowNote(vault, node, color);
        }
        else if (graph.DatasetVersionsOf(node.Id) is { Count: > 0 } datasets)
        {
            if (datasets.Count > 1)
            {
                ShowText($"{datasets.Count} versions, in conflict: Import… one, or keep one in kofrenot on Windows.");
            }

            // One version: Import… in its row, with its other actions; in conflict, under them all.
            var importInRow = datasets.Count == 1 && !IsReadOnly;
            Files.ItemsSource = datasets.Select(version => new FileRow(version, version.File.Name, ByteSize.Format(version.File.Size), importInRow)).ToList();
            Files.IsVisible = true;
            ImportButton.IsVisible = datasets.Count > 1 && !IsReadOnly;
            ShowThumbnail(datasets[^1]);
        }
        else if (node.Type == TypeCatalog.Dataset.Id)
        {
            ShowText(IsReadOnly ? "No file yet." : "No file yet: Import… adds it.");
            ImportButton.IsVisible = !IsReadOnly;
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

    // The current chip in sight: the trail scrolled to its end once laid out.
    private void ShowTrail(IReadOnlyList<Control> pages)
    {
        Trail.ItemsSource = pages
            .Select(page => (Page: page, Label: LabelOf(page)))
            .Where(shown => shown.Label is not null)
            .Select((shown, index) => new TrailChip(shown.Page, shown.Label!, index == 0, shown.Page == this))
            .ToList();
        Dispatcher.UIThread.Post(() => HorizontalScroll.ToRight(TrailScroll), DispatcherPriority.Background);
    }

    private void TrailChip_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.Tag is TrailChip { IsCurrent: false } chip)
        {
            _shell.Services.Log.Info("Action", "Trail: back to a screen before");
            _shell.BackTo(chip.Page);
        }
    }

    private static string? LabelOf(Control page) => page switch
    {
        HomePage => "Home",
        NodePage node => node.Label,
        _ => null,
    };

    // A screen of its own: this one stays as it is, a note being edited included, until Back comes to it.
    // A link with qualifiers, or whose type has some, opens its sheet (NodePage.Qualifiers); another opens its node.
    private void Link_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.Tag is NodeRow { Link: { } link } row && Vault is { } vault && NodeFields.OfLink(vault.Graph, link).Count > 0)
        {
            ShowLinkSheet(row, link);
        }
        else if ((sender as Control)?.Tag is NodeRow { CanOpen: true } open)
        {
            _shell.Push(new NodePage(_shell, open.Node));
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
