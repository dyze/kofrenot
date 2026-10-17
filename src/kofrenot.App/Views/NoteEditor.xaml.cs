using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Notes;

namespace Kofrenot.UI.Views;

/// <summary>A version offered in a conflict: the button shows when it was saved.</summary>
public sealed record ConflictChoice(ConflictVersion Version)
{
    public string Label => Version.At.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture) + (CanKeep ? string.Empty : " (not synced yet)");

    public bool CanKeep => Version.Text is not null;
}

/// <summary>
/// The content of the note shown in the details (a view of <see cref="NotePlanner"/>): read when
/// the note is opened, saved by Ctrl+S, the save button, or when the note is left.
/// </summary>
public partial class NoteEditor : UserControl
{
    private KnowledgeGraph? _graph;
    private Node? _node;
    private NoteState? _state;
    private Func<Guid, string?> _read = _ => null;
    private bool _loading;

    public NoteEditor() => InitializeComponent();

    /// <summary>Writes a version with its content; false when it could not.</summary>
    public Func<NoteEdited, string, bool>? Save { get; set; }

    /// <summary>The note shown, or null.</summary>
    public Node? Node => _node;

    /// <summary>Typing in the note: the details leave its keys alone.</summary>
    public bool IsEditing => Box.IsKeyboardFocusWithin;

    private bool IsDirty => _state is { IsMissing: false } state && Box.Text != state.Text;

    /// <summary>Shows a note, reading its content. The note shown before is saved first if it was changed.</summary>
    /// <param name="read">Reads a version's content; null when its file is not there.</param>
    public void Open(KnowledgeGraph graph, Node node, Func<Guid, string?> read)
    {
        SaveIfChanged();
        _graph = graph;
        _node = node;
        _read = read;
        Load();
    }

    /// <summary>
    /// The graph changed (a save, or changes from elsewhere): reads the note again, unless it is
    /// being changed here: its edit is kept, and saving it keeps any version saved meanwhile as a conflict.
    /// </summary>
    public void Refresh()
    {
        if (_node is not null && !IsDirty)
        {
            Load();
        }
    }

    /// <summary>Saves the note if it was changed (when it is left). False when that save failed.</summary>
    public bool SaveIfChanged() => !IsDirty || Write(Box.Text);

    /// <summary>Forgets the note (the vault closed); nothing is saved.</summary>
    public void Close()
    {
        _graph = null;
        _node = null;
        _state = null;
    }

    private void Load()
    {
        _state = new NotePlanner(_graph!).Load(_node!, _read);
        _loading = true;
        Box.Text = _state.Text ?? string.Empty;
        _loading = false;
        Toolbar.IsEnabled = !_state.IsMissing;
        Box.IsReadOnly = _state.IsMissing;
        ConflictVersions.ItemsSource = _state.Conflict.Select(version => new ConflictChoice(version)).ToList();
        ConflictPanel.Visibility = _state.Conflict.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ShowStatus();
    }

    private void ShowStatus() => StatusText.Text = _state switch
    {
        { IsMissing: true } => "The content of this note is not synced to this computer yet.",
        _ when IsDirty => "Not saved: Ctrl+S saves, and so does leaving the note.",
        { Versions.Count: 0 } => "Empty note. Markdown: **bold**, *italic*, # heading, - list, [link](url).",
        _ => "Saved.",
    };

    private bool Write(string text)
    {
        if (_graph is null || _node is null || _state is null || new NotePlanner(_graph).Save(_node, text, _state) is not { } version)
        {
            return true;
        }

        if (Save?.Invoke(version, text) != true)
        {
            return false;
        }

        Load();
        return true;
    }

    private void Box_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loading)
        {
            ShowStatus();
        }
    }

    private void Box_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
        {
            Write(Box.Text);
            e.Handled = true;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e) => Write(Box.Text);

    private void KeepVersion_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ConflictChoice { Version.Text: { } text } })
        {
            Write(text);
        }
    }

    private void Bold_Click(object sender, RoutedEventArgs e) => Format(edit => MarkdownFormat.Emphasis(edit, "**"));

    private void Italic_Click(object sender, RoutedEventArgs e) => Format(edit => MarkdownFormat.Emphasis(edit, "*"));

    private void Heading_Click(object sender, RoutedEventArgs e) => Format(edit => MarkdownFormat.LinePrefix(edit, "# "));

    private void List_Click(object sender, RoutedEventArgs e) => Format(edit => MarkdownFormat.LinePrefix(edit, "- "));

    private void Link_Click(object sender, RoutedEventArgs e) => Format(MarkdownFormat.Link);

    private void Format(Func<TextEdit, TextEdit> format)
    {
        if (Box.IsReadOnly)
        {
            return;
        }

        var edited = format(new TextEdit(Box.Text, Box.SelectionStart, Box.SelectionLength));
        Box.Text = edited.Text;
        Box.Select(edited.Start, edited.Length);
        Box.Focus();
    }
}
