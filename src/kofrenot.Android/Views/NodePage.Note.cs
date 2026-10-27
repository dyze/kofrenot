using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Notes;
using Kofrenot.Core.Storage;
using Kofrenot.Phone.Platform;

namespace Kofrenot.Phone.Views;

/// <summary>
/// A note on the phone: shown as Markdown, a tap on a checkbox checks its item, Edit turns it into
/// its text to change. Each change is a new version, uploaded at the next sync. A note edited on
/// two devices before they synced shows both versions, to keep one. A reader changes nothing.
/// </summary>
public sealed partial class NodePage
{
    // The note being edited, and its versions as loaded when editing began; null when not editing.
    private (Node Node, NoteState Loaded)? _editing;

    private bool CanEdit => !_shell.Services.Session.IsReadOnly;

    /// <summary>
    /// Before leaving the note: when its text was changed and not saved, asks whether to discard
    /// it, then runs <paramref name="leave"/> on yes. True while it asks: the caller does not leave.
    /// </summary>
    private bool AsksBeforeLeaving(Action leave)
    {
        if (_editing is not { } editing)
        {
            return false;
        }

        if ((NoteEditor.Text ?? string.Empty) == (editing.Loaded.Text ?? string.Empty))
        {
            _editing = null;
            return false;
        }

        _shell.Run(async () =>
        {
            if (await _shell.Confirm("Discard your changes to this note?", "Discard"))
            {
                _shell.Services.Log.Info("Action", "Note changes discarded");
                _editing = null;
                leave();
            }
        });
        return true;
    }

    // The latest version, then those in conflict with it; one not here says why.
    private void ShowNote(Vault vault, Node node, NodeColor color)
    {
        NoteBox.Background = NodeLook.Brush(color) is ISolidColorBrush shade
            ? new SolidColorBrush(shade.Color, 0.25)
            : null;
        if (_editing?.Node.Id == node.Id)
        {
            // A refresh (a sync) keeps what is being typed.
            NoteBox.IsVisible = true;
            return;
        }

        _editing = null;
        var state = new NotePlanner(vault.Graph).Load(node, vault.ReadNote);
        if (state.IsMissing)
        {
            ShowText(vault.StateOf(state.Versions[^1].Id).Describe("the note"));
            return;
        }

        Note.Children.Clear();
        if (state.Conflict.Count > 1)
        {
            foreach (var version in state.Conflict)
            {
                Note.Children.Add(new TextBlock { Classes = { "caption" }, Text = $"Version of {version.At.ToLocalTime():g}" });
                NoteView.Add(Note, version.Text ?? vault.StateOf(version.Id).Describe("this version"));
                if (CanEdit && version.Text is { } text)
                {
                    var keep = new Button { Content = "Keep this version" };
                    keep.Click += (_, _) => Keep(node, text);
                    Note.Children.Add(keep);
                }
            }
        }
        else
        {
            NoteView.Add(Note, state.Text!, CanEdit ? task => Toggle(node, task) : null);
        }

        ShowEditor(false);
        EditNoteButton.IsVisible = CanEdit && state.Conflict.Count == 0;
        NoteBox.IsVisible = true;
    }

    private void ShowEditor(bool editing)
    {
        Note.IsVisible = !editing;
        NoteEditor.IsVisible = SaveNoteButton.IsVisible = CancelNoteButton.IsVisible = editing;
        EditNoteButton.IsVisible = !editing;
    }

    private void EditNote_Click(object? sender, RoutedEventArgs e)
    {
        if (Vault is not { } vault || _browser.Details?.Node is not { } node)
        {
            return;
        }

        var loaded = new NotePlanner(vault.Graph).Load(node, vault.ReadNote);
        if (loaded.IsMissing || loaded.Conflict.Count > 1)
        {
            return;
        }

        _editing = (node, loaded);
        NoteEditor.Text = loaded.Text;
        ShowEditor(true);
        NoteEditor.Focus();
    }

    private void SaveNote_Click(object? sender, RoutedEventArgs e)
    {
        if (_editing is not { } editing)
        {
            return;
        }

        _editing = null;
        Save(editing.Node, NoteEditor.Text ?? string.Empty, editing.Loaded);
        Refresh();
    }

    private void CancelNote_Click(object? sender, RoutedEventArgs e)
    {
        _editing = null;
        Refresh();
    }

    // A checkbox tapped: its item checked or unchecked, saved at once.
    private void Toggle(Node node, int task)
    {
        if (Vault is { } vault && new NotePlanner(vault.Graph).Load(node, vault.ReadNote) is { IsMissing: false, Text: { } text } loaded)
        {
            Save(node, TaskList.Toggle(text, task), loaded);
        }
    }

    // One version of a conflict kept: saved again over all of them.
    private void Keep(Node node, string text)
    {
        if (Vault is { } vault)
        {
            Save(node, text, new NotePlanner(vault.Graph).Load(node, vault.ReadNote));
        }
    }

    private void Save(Node node, string text, NoteState loaded)
    {
        if (Vault is not { } vault || new NotePlanner(vault.Graph).Save(node, text, loaded) is not { } version)
        {
            return;
        }

        var answers = new PreparedAnswers();
        _shell.Services.Session.SaveNote(answers, version, text);
        _shell.Tell(answers.Told);
    }
}
