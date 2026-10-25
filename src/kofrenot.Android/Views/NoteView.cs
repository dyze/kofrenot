using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Kofrenot.Core.Notes;

namespace Kofrenot.Phone.Views;

/// <summary>A note's Markdown shown, not edited (<see cref="MarkdownBlocks"/>): one line of text, a heading, a list item or a task each.</summary>
public static class NoteView
{
    private const double IndentStep = 20;
    private static readonly double[] HeadingSizes = [24, 21, 19, 17, 16, 15];

    /// <summary>Adds the lines of <paramref name="text"/> to <paramref name="panel"/>.</summary>
    public static void Add(Panel panel, string text)
    {
        foreach (var block in MarkdownBlocks.Parse(text))
        {
            panel.Children.Add(ControlOf(block));
        }
    }

    private static Control ControlOf(MarkdownBlock block)
    {
        var line = TextOf(block.Runs);
        var indent = new Thickness(block.Level * IndentStep, 0, 0, 0);
        switch (block.Kind)
        {
            case BlockKind.Heading:
                line.FontSize = HeadingSizes[block.Level - 1];
                line.FontWeight = FontWeight.SemiBold;
                line.Margin = new Thickness(0, 6, 0, 0);
                return line;
            case BlockKind.Bullet:
                return Marked(new TextBlock { Text = "•", Margin = new Thickness(0, 0, 8, 0) }, line, indent);
            case BlockKind.Task:
                if (block.IsDone)
                {
                    line.TextDecorations = TextDecorations.Strikethrough;
                    line.Opacity = 0.6;
                }

                // Read only: checked in kofrenot on Windows.
                return Marked(new CheckBox { IsChecked = block.IsDone, IsHitTestVisible = false, Focusable = false, MinWidth = 0 }, line, indent);
            default:
                return line;
        }
    }

    private static Grid Marked(Control mark, Control line, Thickness indent)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Margin = indent };
        mark.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        line.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        Grid.SetColumn(line, 1);
        grid.Children.Add(mark);
        grid.Children.Add(line);
        return grid;
    }

    private static SelectableTextBlock TextOf(IReadOnlyList<TextRun> runs)
    {
        var text = new SelectableTextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 15 };
        text.Inlines ??= [];
        foreach (var run in runs)
        {
            var inline = new Run(run.Text)
            {
                FontWeight = run.IsBold ? FontWeight.Bold : FontWeight.Normal,
                FontStyle = run.IsItalic ? FontStyle.Italic : FontStyle.Normal,
            };
            if (run.Link is not null)
            {
                // Shown as a link, not followed: the phone opens nothing from a vault.
                inline.TextDecorations = TextDecorations.Underline;
                inline.Foreground = Application.Current?.FindResource("Accent") as IBrush;
            }

            text.Inlines.Add(inline);
        }

        return text;
    }
}
