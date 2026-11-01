using System.Windows;
using System.Windows.Controls;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;

namespace Kofrenot.UI.Views;

/// <summary>Shows a <see cref="NodeVisual"/>: its icon, then its thumbnail from <see cref="ThumbnailCache.Shared"/>, and its colour's dot.</summary>
public partial class NodeVisualView : UserControl
{
    private const double MinDotSize = 7;
    private const double DotShare = 0.35;

    public static readonly DependencyProperty VisualProperty = DependencyProperty.Register(
        nameof(Visual), typeof(NodeVisual), typeof(NodeVisualView), new PropertyMetadata(null, (view, _) => ((NodeVisualView)view).Show()));

    public NodeVisualView() => InitializeComponent();

    public NodeVisual? Visual
    {
        get => (NodeVisual?)GetValue(VisualProperty);
        set => SetValue(VisualProperty, value);
    }

    /// <summary>The colour's dot, sized with the picture.</summary>
    private void ShowColor(NodeColor color)
    {
        ColorDot.Fill = NoteColors.Fill(color);
        ColorDot.Width = ColorDot.Height = Math.Max(MinDotSize, Width * DotShare);
        ColorDot.Visibility = ColorDot.Fill is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void Show()
    {
        var visual = Visual;
        Picture.Kind = visual is null ? null : IconPaths.NameOf(visual);
        ShowColor(visual?.Color ?? NodeColor.None);
        Picture.Visibility = Visibility.Visible;
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
            Picture.Visibility = Visibility.Collapsed;
        }
    }
}
