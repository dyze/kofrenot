using System.Windows;
using System.Windows.Input;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Logging;

namespace Kofrenot.UI.Views;

/// <summary>The content of a note, under its properties: read when the note is shown, saved when it is left.</summary>
public partial class NodeDetailsPanel
{
    /// <summary>Reads a note version's content; null when its file is not there.</summary>
    public Func<Guid, string?>? ReadNote { get; set; }

    /// <summary>Writes a note version with its content; false when it could not.</summary>
    public Func<NoteEdited, string, bool>? SaveNote
    {
        set => NoteBox.Save = value;
    }

    /// <summary>Saves the note shown if it was changed: when it is left, or the window hidden.</summary>
    public void LeaveNote() => NoteBox.SaveIfChanged();

    /// <summary>The name of the note shown, when it has unsaved changes; null otherwise.</summary>
    public string? UnsavedNote => NoteBox.IsDirty ? NoteBox.Node?.Name : null;

    private void ShowNote(NodeDetails details)
    {
        var isNote = details.Node.Type == TypeCatalog.Note.Id;
        NoteBox.Visibility = isNote ? Visibility.Visible : Visibility.Collapsed;
        NoteRow.Height = isNote ? new GridLength(2, GridUnitType.Star) : GridLength.Auto;
        if (!isNote)
        {
            CloseNote();
        }
        else if (NoteBox.Node?.Id == details.Node.Id)
        {
            NoteBox.Refresh();
        }
        else
        {
            NoteBox.Open(Browser.Graph!, details.Node, ReadNote ?? (_ => null));
            Log.Info("Action", "Note opened");
        }
    }

    private void CloseNote()
    {
        NoteBox.SaveIfChanged();
        NoteBox.Close();
    }

    /// <summary>While typing in the note, Esc leaves it (saving it); the other keys are the text box's.</summary>
    private bool HandleNoteKey(Key key)
    {
        if (key != Key.Escape)
        {
            return false;
        }

        LeaveNote();
        Focus();
        return true;
    }
}
