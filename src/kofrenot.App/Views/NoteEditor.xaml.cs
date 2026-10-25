using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Notes;
using Kofrenot.Core.Storage;

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
    private const double CardPadding = 8;

    private KnowledgeGraph? _graph;
    private Node? _node;
    private NoteState? _state;
    private Func<Guid, string?> _read = _ => null;

    /// <summary>Where a version's content stands, while it is not here: not synced, being downloaded, or not downloaded.</summary>
    public Func<Guid, ContentState> StateOf { get; set; } = _ => ContentState.NotHere;

    public NoteEditor()
    {
        InitializeComponent();
        Box.Edited += ShowStatus;
    }

    /// <summary>Writes a version with its content; false when it could not.</summary>
    public Func<NoteEdited, string, bool>? Save { get; set; }

    /// <summary>The note shown, or null.</summary>
    public Node? Node => _node;

    /// <summary>Typing in the note: the details leave its keys alone.</summary>
    public bool IsEditing => Box.IsKeyboardFocusWithin;

    /// <summary>The note was changed since it was read or saved.</summary>
    public bool IsDirty => _state is { IsMissing: false } state && Box.Text != state.Text;

    /// <summary>Shows a note, reading its content. The note shown before is saved first if it was changed.</summary>
    /// <param name="read">Reads a version's content; null when its file is not there.</param>
    public void Open(KnowledgeGraph graph, Node node, Func<Guid, string?> read)
    {
        SaveIfChanged();
        _graph = graph;
        _node = node;
        _read = read;
        ShowColor();
        Load();
    }

    /// <summary>
    /// The graph changed (a save, or changes from elsewhere): reads the note again, unless it is
    /// being changed here: its edit is kept, and saving it keeps any version saved meanwhile as a conflict.
    /// </summary>
    public void Refresh()
    {
        if (_node is null)
        {
            return;
        }

        ShowColor();
        if (!IsDirty)
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

    /// <summary>The note's colour as the background of its text, with room around it; nothing without one.</summary>
    private void ShowColor()
    {
        Card.Background = NoteColors.Tint(_graph!.ColorOf(_node!.Id));
        Card.Padding = new Thickness(Card.Background is null ? 0 : CardPadding);
    }

    private void Load()
    {
        _state = new NotePlanner(_graph!).Load(_node!, _read);
        Box.Text = _state.Text ?? string.Empty;
        Box.IsReadOnly = _state.IsMissing;
        SaveButton.IsEnabled = !_state.IsMissing;
        ConflictVersions.ItemsSource = _state.Conflict.Select(version => new ConflictChoice(version)).ToList();
        ConflictPanel.Visibility = _state.Conflict.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ShowStatus();
    }

    /// <summary>Whether the note is saved, after how many of its items are checked when it has some.</summary>
    private void ShowStatus()
    {
        var status = _state switch
        {
            { IsMissing: true } missing => StateOf(missing.Versions[^1].Id).Describe("this note"),
            _ when IsDirty => "Not saved: Ctrl+S saves, and so does leaving the note.",
            { Versions.Count: 0 } => "Empty note. Markdown: **bold**, *italic*, # heading, - list, [link](url).",
            _ => "Saved.",
        };
        var progress = TaskList.ProgressOf(Box.Text);
        StatusText.Text = progress.Total > 0 ? $"{progress.Done}/{progress.Total} done. {status}" : status;
    }

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
}
