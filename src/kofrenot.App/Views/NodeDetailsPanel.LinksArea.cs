using System.Windows;
using System.Windows.Controls.Primitives;
using Kofrenot.Core.Logging;

namespace Kofrenot.UI.Views;

/// <summary>
/// The links' room. Under a note, the note takes the space: the links are as tall as their rows, up to a height the user
/// drags (a third of the panel until then), and scroll beyond. Elsewhere they take the space left.
/// </summary>
public partial class NodeDetailsPanel
{
    private const double MinNoteLinksHeight = 40;
    private const double MinNoteHeight = 120;

    /// <summary>The height of a note's links, as last dragged; null for the default.</summary>
    public double? NoteLinksHeight { get; set; }

    /// <summary>Remembers a height dragged for a note's links.</summary>
    public Action<double> SaveNoteLinksHeight { get; set; } = _ => { };

    private bool IsNoteWithLinks => NoteBox.Visibility == Visibility.Visible && LinksList.Items.Count > 0;

    private void FitLinksRow()
    {
        var hasLinks = LinksList.Items.Count > 0;
        LinksRow.Height = hasLinks && !IsNoteWithLinks ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
        LinksSplitter.Visibility = IsNoteWithLinks ? Visibility.Visible : Visibility.Collapsed;
        FitNoteLinksHeight();
    }

    /// <summary>On the scroll viewer, not its row: an automatic row would cut the links instead of scrolling them.</summary>
    private void FitNoteLinksHeight()
    {
        LinksScroll.MaxHeight = IsNoteWithLinks
            ? Math.Clamp(NoteLinksHeight ?? (ActualHeight / 3), MinNoteLinksHeight, MaxNoteLinksHeight())
            : double.PositiveInfinity;
    }

    // What the note leaves, keeping it a minimum height.
    private double MaxNoteLinksHeight() => Math.Max(
        MinNoteLinksHeight,
        ActualHeight - Breadcrumb.ActualHeight - PropertiesScroll.ActualHeight - LinksBar.ActualHeight - LinksBar.Margin.Top - LinksBar.Margin.Bottom - MinNoteHeight);

    private void LinksSplitter_DragDelta(object sender, DragDeltaEventArgs e)
    {
        NoteLinksHeight = Math.Clamp(LinksScroll.ActualHeight - e.VerticalChange, MinNoteLinksHeight, MaxNoteLinksHeight());
        FitNoteLinksHeight();
    }

    private void LinksSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (NoteLinksHeight is { } height)
        {
            SaveNoteLinksHeight(height);
            Log.Info("Action", "Note links resized");
        }
    }
}
