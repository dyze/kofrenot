using System.Windows;
using System.Windows.Controls;
using Kofrenot.Core.Notes;

namespace Kofrenot.UI.Views;

/// <summary>Markdown text with a toolbar (<see cref="MarkdownFormat"/>): bold, italic, heading, list, link.</summary>
public partial class MarkdownBox : UserControl
{
    private bool _setting;

    public MarkdownBox() => InitializeComponent();

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
        }
    }

    /// <summary>Read only: the text cannot be changed, and the toolbar is off.</summary>
    public bool IsReadOnly
    {
        get => Box.IsReadOnly;
        set
        {
            Box.IsReadOnly = value;
            Toolbar.IsEnabled = !value;
        }
    }

    /// <summary>Puts the caret in the text.</summary>
    public void FocusText() => Box.Focus();

    private void Box_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_setting)
        {
            Edited?.Invoke();
        }
    }

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
