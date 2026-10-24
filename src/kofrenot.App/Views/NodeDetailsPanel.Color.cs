using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;

namespace Kofrenot.UI.Views;

/// <summary>
/// A node's colour: a swatch per colour of the palette (<see cref="NoteColors"/>), the current one
/// ringed, and in a conflict each colour given; a click gives the node that colour.
/// </summary>
public partial class NodeDetailsPanel
{
    private const double SwatchSize = 18;

    private void ShowColor(NodeDetails details)
    {
        var ringed = details.ColorConflict.Count > 0 ? details.ColorConflict : [details.Color];
        ColorSwatches.Children.Clear();
        foreach (var color in NoteColors.Palette)
        {
            ColorSwatches.Children.Add(SwatchOf(color, ringed.Contains(color)));
        }

        ColorConflictText.Visibility = details.ColorConflict.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private Button SwatchOf(NodeColor color, bool isRinged)
    {
        var dot = new Ellipse
        {
            Width = SwatchSize,
            Height = SwatchSize,
            Fill = NoteColors.Fill(color) ?? Brushes.Transparent,
            Stroke = (Brush)FindResource("ControlStrongStrokeColorDefaultBrush"),
            StrokeThickness = color == NodeColor.None ? 1 : 0,
        };
        var ring = new Border
        {
            Padding = new Thickness(2),
            CornerRadius = new CornerRadius(SwatchSize),
            BorderThickness = new Thickness(2),
            BorderBrush = isRinged ? (Brush)FindResource("AccentFillColorDefaultBrush") : Brushes.Transparent,
            Child = dot,
        };
        var swatch = new Button
        {
            Style = (Style)FindResource("FlatButton"),
            Padding = new Thickness(1),
            Content = ring,
            ToolTip = NoteColors.NameOf(color),
        };
        swatch.Click += (_, _) => ChangeColor(color);
        return swatch;
    }

    private void ChangeColor(NodeColor color)
    {
        if (Browser is not { Graph: { } graph, Details: { } details }
            || new ColorPlanner(graph).Plan(details.Node, color) is not { } change)
        {
            return;
        }

        var resolved = change.Replaces.Count > 1 ? $", a conflict of {change.Replaces.Count} colours resolved" : string.Empty;
        if (WriteChanges?.Invoke([change], $"Node colour changed to {NoteColors.NameOf(color).ToLowerInvariant()}{resolved}") == true)
        {
            Show();
        }
    }
}
