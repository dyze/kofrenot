using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;

namespace Kofrenot.UI.Views;

/// <summary>
/// A node's colour, as in Google Keep: a dot of it on the palette button next to the pin; the button
/// shows a swatch per colour of the palette (<see cref="NoteColors"/>) in a popup, the current one
/// ringed and checked, and in a conflict each colour given. A click gives the node that colour.
/// </summary>
public partial class NodeDetailsPanel
{
    private const double SwatchSize = 24;

    private void ShowColor(NodeDetails details)
    {
        ColorDot.Fill = NoteColors.Fill(details.Color);
        ColorDot.Visibility = ColorDot.Fill is null ? Visibility.Collapsed : Visibility.Visible;
        ColorButton.ToolTip = details.ColorConflict.Count > 0
            ? "Colour: chosen differently on two devices"
            : $"Colour: {NoteColors.NameOf(details.Color).ToLowerInvariant()}";

        var ringed = details.ColorConflict.Count > 0 ? details.ColorConflict : [details.Color];
        ColorSwatches.Children.Clear();
        foreach (var color in NoteColors.Palette)
        {
            ColorSwatches.Children.Add(SwatchOf(color, ringed.Contains(color)));
        }

        ColorConflictText.Visibility = details.ColorConflict.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ColorButton_Click(object sender, RoutedEventArgs e) => ColorPopup.IsOpen = !ColorPopup.IsOpen;

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
        var face = new Grid();
        face.Children.Add(dot);
        if (isRinged)
        {
            face.Children.Add(new TextBlock
            {
                Text = "",
                FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        var ring = new Border
        {
            Padding = new Thickness(2),
            CornerRadius = new CornerRadius(SwatchSize),
            BorderThickness = new Thickness(2),
            BorderBrush = isRinged ? (Brush)FindResource("AccentFillColorDefaultBrush") : Brushes.Transparent,
            Child = face,
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
        ColorPopup.IsOpen = false;
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
