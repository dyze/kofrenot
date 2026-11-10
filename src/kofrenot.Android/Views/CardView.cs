using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Datasets;

namespace Kofrenot.Phone.Views;

/// <summary>
/// A node as a card of a list view (<see cref="CardView"/>): its row's picture and name, whether it is
/// pinned, its colour as a tint; its preview read by <see cref="Read"/>, and what a note not on the phone
/// says of its content (<see cref="Missing"/>).
/// </summary>
public sealed record CardRow(NodeRow Row, bool IsPinned, Func<NodeCard> Read, Func<Guid, string> Missing)
{
    public IBrush? Tint => Row.Color is ISolidColorBrush shade ? new SolidColorBrush(shade.Color, 0.25) : null;
}

/// <summary>
/// A card, as on Windows: its node's picture and name, a pin when it is pinned, over its preview
/// (<see cref="NodeCard"/>: a note's lines or unchecked items and how many are checked, a file's kind
/// and size, a thumbnail), in its colour, as tall as its content. Its preview is read once the card
/// comes into view, so a long list reads only the notes looked at.
/// </summary>
public sealed class CardView : Border
{
    public static readonly StyledProperty<CardRow?> CardProperty = AvaloniaProperty.Register<CardView, CardRow?>(nameof(Card));

    private const double ThumbnailHeight = 120;
    private const double CheckboxSize = 12;

    private readonly Decorator _body = new();
    private readonly DockPanel _header = new() { Margin = new Thickness(0, 0, 0, 4) };
    private bool _isRead;

    public CardView()
    {
        Padding = new Thickness(10);
        CornerRadius = new CornerRadius(8);
        BorderThickness = new Thickness(1);
        BorderBrush = PhoneTheme.Brush("ChipStroke");
        ClipToBounds = true;
        DockPanel.SetDock(_header, Dock.Top);
        Child = new DockPanel { Children = { _header, _body } };
        EffectiveViewportChanged += (_, e) => ReadWhenIn(e.EffectiveViewport);
    }

    public CardRow? Card
    {
        get => GetValue(CardProperty);
        set => SetValue(CardProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CardProperty)
        {
            Show(Card);
        }
    }

    private void Show(CardRow? card)
    {
        Background = card?.Tint ?? PhoneTheme.Brush("ChipFill");
        _header.Children.Clear();
        _body.Child = null;
        _isRead = false;
        if (card is null)
        {
            return;
        }

        var size = PhoneTheme.Size("BodySize");
        var picture = new NodePicture { Item = card.Row, Width = size + 4, Height = size + 4, Margin = new Thickness(0, 0, 6, 0) };
        DockPanel.SetDock(picture, Dock.Left);
        _header.Children.Add(picture);
        if (card.IsPinned)
        {
            var pin = new PathIcon { Data = PhoneTheme.Icon("pin"), Width = 12, Height = 12, Opacity = 0.7, Margin = new Thickness(4, 0, 0, 0) };
            DockPanel.SetDock(pin, Dock.Right);
            _header.Children.Add(pin);
        }

        _header.Children.Add(new TextBlock { Text = card.Row.Name, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
        InvalidateMeasure();
    }

    private void ReadWhenIn(Rect viewport)
    {
        if (_isRead || Card is not { } card || Bounds.Height <= 0 || !viewport.Intersects(new Rect(Bounds.Size)))
        {
            return;
        }

        _isRead = true;
        _body.Child = BodyOf(card, card.Read());
    }

    private Control? BodyOf(CardRow row, NodeCard card) => card.Body switch
    {
        NoteCardBody note => NoteBodyOf(note.Preview),
        MissingCardBody missing => Caption(row.Missing(missing.Version)),
        FileCardBody file when card.Visual.Thumbnail is { } version => ThumbnailOf(row, version, file),
        FileCardBody file => Caption(FileText(file)),
        TextCardBody text => Caption(text.Text),
        _ => null,
    };

    private static StackPanel NoteBodyOf(NotePreview preview)
    {
        var lines = new StackPanel();
        foreach (var line in preview.Lines)
        {
            lines.Children.Add(Line(line));
        }

        foreach (var item in preview.Items)
        {
            var checkbox = new PathIcon { Data = PhoneTheme.Icon("checkbox"), Width = CheckboxSize, Height = CheckboxSize, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(checkbox, Dock.Left);
            lines.Children.Add(new DockPanel { Children = { checkbox, Line(item) } });
        }

        if (preview.HasMore)
        {
            lines.Children.Add(Caption("…"));
        }

        if (preview.CheckedCount > 0)
        {
            lines.Children.Add(Caption(preview.CheckedCount == 1 ? "+ 1 checked" : $"+ {preview.CheckedCount} checked"));
        }

        return lines;
    }

    private DockPanel ThumbnailOf(CardRow row, Guid version, FileCardBody file)
    {
        var caption = Caption(FileText(file));
        DockPanel.SetDock(caption, Dock.Bottom);
        var image = new Image { Height = ThumbnailHeight, Stretch = Stretch.UniformToFill };
        ShowThumbnail(row, image, version);
        return new DockPanel { Children = { caption, new Border { CornerRadius = new CornerRadius(4), ClipToBounds = true, Child = image } } };
    }

    // A card reused for another node while it was read keeps that one's.
    private async void ShowThumbnail(CardRow row, Image image, Guid version)
    {
        var thumbnail = await ThumbnailCache.Shared.Get(version);
        if (Card == row)
        {
            image.Source = thumbnail;
        }
    }

    private static string FileText(FileCardBody file) => $"{file.Kind}, {ByteSize.Format(file.Size, CultureInfo.CurrentCulture)}";

    private static TextBlock Line(string text) => new() { Text = text, FontSize = PhoneTheme.Size("CaptionSize") + 1, TextTrimming = TextTrimming.CharacterEllipsis };

    private static TextBlock Caption(string text) => new() { Text = text, Classes = { "caption" }, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) };
}
