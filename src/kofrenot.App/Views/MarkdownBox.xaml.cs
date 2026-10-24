using System.Windows;
using System.Windows.Controls;
using Kofrenot.Core.Notes;

namespace Kofrenot.UI.Views;

/// <summary>
/// Markdown text with a toolbar (<see cref="MarkdownFormat"/>): bold, italic, heading, list, link;
/// and checkboxes, shown or hidden (<see cref="TaskList"/>). A text whose lines are all items is shown as a task list.
/// </summary>
public partial class MarkdownBox : UserControl
{
    private bool _setting;

    public MarkdownBox()
    {
        InitializeComponent();
        Items.Changed += items => Box.Text = TaskList.Format(items, LineBreak());
    }

    /// <summary>Raised when the user changes the text (not when <see cref="Text"/> is set).</summary>
    public event Action? Edited;

    public string Text
    {
        get => Box.Text;
        set
        {
            _setting = true;
            Box.Text = value;
            _setting = false;
            ShowChecklist(TaskList.IsList(value));
        }
    }

    /// <summary>Read only: the text cannot be changed, and the toolbar is off.</summary>
    public bool IsReadOnly
    {
        get => Box.IsReadOnly;
        set
        {
            Box.IsReadOnly = value;
            Items.IsReadOnly = value;
            Toolbar.IsEnabled = !value;
        }
    }

    private void Box_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_setting)
        {
            Edited?.Invoke();
        }
    }

    private void Checklist_Click(object sender, RoutedEventArgs e)
    {
        var show = ChecklistButton.IsChecked == true;
        Box.Text = show ? TaskList.Show(Box.Text) : TaskList.Hide(Box.Text);
        ShowChecklist(show);
        if (show)
        {
            Items.FocusFirst();
        }
        else
        {
            Box.Focus();
        }
    }

    /// <summary>Shows the text as a task list, or as text with its formatting buttons.</summary>
    private void ShowChecklist(bool show)
    {
        ChecklistButton.IsChecked = show;
        ChecklistButton.ToolTip = show
            ? "Hide checkboxes: each item becomes a line of text"
            : "Show checkboxes: each line becomes an item of a task list";
        FormatButtons.IsEnabled = !show;
        Box.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
        Items.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (show)
        {
            Items.Show(TaskList.Parse(Box.Text));
        }
    }

    /// <summary>The text's line break; a text box's own (<c>\r\n</c>) while it has none.</summary>
    private string LineBreak() => Box.Text.Contains('\n') ? TaskList.LineBreakOf(Box.Text) : Environment.NewLine;

    private void Bold_Click(object sender, RoutedEventArgs e) => Format(edit => MarkdownFormat.Emphasis(edit, "**"));

    private void Italic_Click(object sender, RoutedEventArgs e) => Format(edit => MarkdownFormat.Emphasis(edit, "*"));

    private void Heading_Click(object sender, RoutedEventArgs e) => Format(edit => MarkdownFormat.LinePrefix(edit, "# "));

    private void List_Click(object sender, RoutedEventArgs e) => Format(edit => MarkdownFormat.LinePrefix(edit, "- "));

    private void Link_Click(object sender, RoutedEventArgs e) => Format(MarkdownFormat.Link);

    private void Format(Func<TextEdit, TextEdit> format)
    {
        var edited = format(new TextEdit(Box.Text, Box.SelectionStart, Box.SelectionLength));
        Box.Text = edited.Text;
        Box.Select(edited.Start, edited.Length);
        Box.Focus();
    }
}
