using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;

namespace Kofrenot.Phone.Views;

/// <summary>
/// A node's picture in a row, as on Windows (NodeVisualView): its thumbnail once read (an image
/// dataset's, or its image field's), else the glyph of its icon with a dot of its colour.
/// </summary>
public sealed class NodePicture : Grid
{
    public static readonly StyledProperty<NodeRow?> ItemProperty = AvaloniaProperty.Register<NodePicture, NodeRow?>(nameof(Item));

    private readonly TextBlock _glyph = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly Ellipse _dot = new() { Width = 8, Height = 8, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom };
    private readonly Image _image = new() { Stretch = Stretch.UniformToFill };
    private readonly Border _frame;

    public NodePicture()
    {
        _glyph.FontSize = PhoneTheme.Size("BodySize");
        _frame = new Border { CornerRadius = new CornerRadius(4), ClipToBounds = true, Child = _image, IsVisible = false };
        Children.Add(_glyph);
        Children.Add(_dot);
        Children.Add(_frame);
    }

    public NodeRow? Item
    {
        get => GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemProperty)
        {
            Show(Item);
        }
    }

    private async void Show(NodeRow? row)
    {
        _glyph.Text = row?.Glyph;
        _dot.Fill = row?.Color;
        _dot.IsVisible = row?.HasColor == true;
        _frame.IsVisible = false;
        _image.Source = null;
        if (row?.Thumbnail is not { } version)
        {
            return;
        }

        // A row reused for another node while it was read shows that one's.
        var image = await ThumbnailCache.Shared.Get(version);
        if (image is not null && Item?.Thumbnail == version)
        {
            _image.Source = image;
            _frame.IsVisible = true;
        }
    }
}
