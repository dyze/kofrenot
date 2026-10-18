using System.Windows;
using System.Windows.Controls;
using Kofrenot.Core.Browse;

namespace Kofrenot.UI.Views;

/// <summary>Shows a <see cref="NodeVisual"/>: the glyph of its icon, then its thumbnail from <see cref="ThumbnailCache.Shared"/>.</summary>
public partial class NodeVisualView : UserControl
{
    public static readonly DependencyProperty VisualProperty = DependencyProperty.Register(
        nameof(Visual), typeof(NodeVisual), typeof(NodeVisualView), new PropertyMetadata(null, (view, _) => ((NodeVisualView)view).Show()));

    public NodeVisualView() => InitializeComponent();

    public NodeVisual? Visual
    {
        get => (NodeVisual?)GetValue(VisualProperty);
        set => SetValue(VisualProperty, value);
    }

    private static string GlyphOf(NodeIcon icon) => icon switch
    {
        NodeIcon.Note => "",
        NodeIcon.File => "",
        NodeIcon.Image => "",
        NodeIcon.Text => "",
        NodeIcon.Code => "",
        NodeIcon.Audio => "",
        NodeIcon.Video => "",
        NodeIcon.Archive => "",
        NodeIcon.Pdf => "",
        NodeIcon.View => "",
        _ => "",
    };

    private async void Show()
    {
        var visual = Visual;
        Glyph.Text = visual is null ? string.Empty : GlyphOf(visual.Icon);
        Glyph.Visibility = Visibility.Visible;
        ThumbnailFrame.Visibility = Visibility.Collapsed;
        Thumbnail.Source = null;
        if (visual?.Thumbnail is not { } version)
        {
            return;
        }

        var image = await ThumbnailCache.Shared.Get(version);

        // The row may show another node by now.
        if (image is not null && Visual?.Thumbnail == version)
        {
            Thumbnail.Source = image;
            ThumbnailFrame.Visibility = Visibility.Visible;
            Glyph.Visibility = Visibility.Collapsed;
        }
    }
}
