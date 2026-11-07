using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Notes;
using Kofrenot.Core.Storage;
using Kofrenot.Phone.Platform;

namespace Kofrenot.Phone.Views;

/// <summary>
/// A note on the phone, as on Windows: always editable, its Markdown in a text box, or its items
/// (<see cref="TaskListEditor"/>) when its lines are all tasks; Checkboxes turns one into the other.
/// Saved by the save icon, and when it is left (another screen, Back, the app in the background).
/// Each save is a new version, uploaded at the next sync. A note edited on two devices before they
/// synced offers each version to keep. A reader changes nothing.
/// </summary>
public sealed partial class NodePage
{
    // The note shown and its versions as last loaded or saved; null when the node shows none.
    private (Node Node, NoteState Loaded)? _note;

    // Its text as typed: the text box's, or the items' as task lines.
    private string _noteText = string.Empty;

    // Shown as a list of items, not as text.
    private bool _asList;

    // Why the note's content is not here; null when it is.
    private string? _noteMissing;

    private bool CanEdit => !_shell.Services.Session.IsReadOnly;

    private bool IsNoteDirty => _note is { Loaded: { IsMissing: false } loaded } && _noteText != loaded.Text;

    /// <summary>The screen is left (another one shown, or the app in the background): the note is saved if changed.</summary>
    public void Leave()
    {
        if (IsNoteDirty)
        {
            SaveNote();
        }
    }

    // A refresh (a sync, a save) keeps what is being typed, and the text box when it shows the note as saved.
    private void ShowNote(Vault vault, Node node, NodeColor color)
    {
        NoteBox.Background = NodeLook.Brush(color) is ISolidColorBrush shade
            ? new SolidColorBrush(shade.Color, 0.25)
            : null;
        NoteBox.IsVisible = true;
        if (_note?.Node.Id == node.Id && IsNoteDirty)
        {
            return;
        }

        var state = new NotePlanner(vault.Graph).Load(node, vault.ReadNote);
        var isShown = _note is { } shown && shown.Node.Id == node.Id && shown.Loaded.Conflict.Count == 0 && state.Conflict.Count == 0 && state.Text == _noteText;
        _note = (node, state);
        _noteMissing = state.IsMissing ? vault.StateOf(state.Versions[^1].Id).Describe("this note") : null;
        if (!isShown)
        {
            ShowNoteText(state);
        }

        ShowNoteStatus();
    }

    private void ShowNoteText(NoteState state)
    {
        var canEdit = CanEdit && !state.IsMissing;
        var text = state.Text ?? string.Empty;
        _asList = TaskList.IsList(text);
        _noteText = text;
        NoteEditor.Text = text;
        NoteEditor.IsReadOnly = TaskEditor.IsReadOnly = !canEdit;
        TaskEditor.Show(TaskList.Parse(text));
        CheckboxesButton.IsVisible = SaveNoteButton.IsVisible = canEdit;
        ShowConflict(state);
        ShowNoteMode();
    }

    // Each version, dated, to keep; one not synced yet cannot be.
    private void ShowConflict(NoteState state)
    {
        ConflictVersions.Children.Clear();
        foreach (var version in state.Conflict)
        {
            var keep = new Button
            {
                Content = $"{version.At.ToLocalTime():g}{(version.Text is null ? " (not synced yet)" : string.Empty)}",
                IsEnabled = version.Text is not null,
            };
            keep.Click += (_, _) => Keep(version.Text!);
            ConflictVersions.Children.Add(keep);
        }

        NoteConflict.IsVisible = CanEdit && state.Conflict.Count > 0;
    }

    private void ShowNoteMode()
    {
        NoteEditor.IsVisible = !_asList;
        TaskEditor.IsVisible = _asList;
        CheckboxesButton.Content = _asList ? "Text" : "Checkboxes";
    }

    // Whether the note is saved, after how many of its items are checked when it has some, as on Windows.
    private void ShowNoteStatus()
    {
        var status = _note switch
        {
            null => string.Empty,
            _ when _noteMissing is not null => _noteMissing,
            _ when IsNoteDirty => "Not saved: leaving the note saves it.",
            { Loaded.Versions.Count: 0 } => "Empty note.",
            _ => "Saved.",
        };
        var progress = TaskList.ProgressOf(_noteText);
        NoteStatus.Text = progress.Total > 0 ? $"{progress.Done}/{progress.Total} done. {status}" : status;
    }

    private void NoteEditor_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (!_asList)
        {
            NoteText(NoteEditor.Text ?? string.Empty);
        }
    }

    private void TaskEditor_Changed(IReadOnlyList<TaskItem> items) =>
        NoteText(TaskList.Format(items, TaskList.LineBreakOf(_note?.Loaded.Text ?? string.Empty)));

    private void NoteText(string text)
    {
        _noteText = text;
        ShowNoteStatus();
    }

    // Checkboxes: each line becomes an item; Text: each item becomes its line (TaskList.Show, Hide), as on Windows.
    private void Checkboxes_Click(object? sender, RoutedEventArgs e)
    {
        _asList = !_asList;
        var text = _asList ? TaskList.Show(_noteText) : TaskList.Hide(_noteText);
        if (_asList)
        {
            TaskEditor.Show(TaskList.Parse(text));
        }
        else
        {
            NoteEditor.Text = text;
        }

        NoteText(text);
        ShowNoteMode();
    }

    // Saves even when nothing was typed: a conflict is resolved by the text shown.
    private void SaveNote_Click(object? sender, RoutedEventArgs e) => SaveNote();

    // One version of a conflict kept: saved again over all of them, then shown.
    private void Keep(string text)
    {
        _noteText = text;
        SaveNote();
        if (_note is { } note)
        {
            ShowNoteText(note.Loaded);
            ShowNoteStatus();
        }
    }

    // The note saved, then loaded again: the next save replaces this version.
    private void SaveNote()
    {
        if (Vault is not { } vault || _note is not { Loaded.IsMissing: false } note || vault.Graph.FindNode(note.Node.Id) is null)
        {
            return;
        }

        if (new NotePlanner(vault.Graph).Save(note.Node, _noteText, note.Loaded) is { } version)
        {
            var answers = new PreparedAnswers();
            _shell.Services.Session.SaveNote(answers, version, _noteText);
            _shell.Tell(answers.Told);
        }

        var saved = new NotePlanner(vault.Graph).Load(note.Node, vault.ReadNote);
        _note = (note.Node, saved);
        ShowConflict(saved);
        ShowNoteStatus();
    }
}
