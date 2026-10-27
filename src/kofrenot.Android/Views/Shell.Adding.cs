using Kofrenot.Core.Datasets;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Notes;
using Kofrenot.Phone.Platform;

namespace Kofrenot.Phone.Views;

/// <summary>
/// What the phone adds to the open vault, after a preview naming it: files (picked with ＋, or
/// shared by another app) as datasets, text shared by another app as a note. One node added opens;
/// several are listed among the recent nodes. A share waits until a vault is open.
/// </summary>
public sealed partial class Shell
{
    private string VaultName => Services.Session.Selected is { } vault ? Services.Session.NameOf(vault) : "the vault";

    /// <summary>Adds what another app shared, once a vault is open; else it waits for the vault to be opened.</summary>
    public void AddShared()
    {
        if (Services.Session.Current is null || App.Current?.TakeShare() is not { } shared)
        {
            return;
        }

        Run(() => shared.Text is { } text ? AddNoteAsync(text) : AddFilesAsync(shared.Files));
    }

    public async Task AddFilesAsync(IReadOnlyList<ImportFile> files)
    {
        if (Services.Session.Current is not { } vault || RefusesToAdd())
        {
            return;
        }

        var preview = new FileImportPlanner(vault.Graph).Preview(files);
        var rows = preview.Rows.Select(row =>
            $"+ {row.NodeName} ({row.Type.Name}, {ByteSize.Format(row.File.Size)}){(row.IsHomonym ? ", a homonym" : string.Empty)}");
        if (!await Confirm($"{preview.Heading} to {VaultName}\n\n{string.Join("\n", rows)}", "Add"))
        {
            Services.Log.Info("Action", "Import of files cancelled");
            return;
        }

        var answers = new PreparedAnswers();
        var added = await Services.Session.ImportFiles(answers, preview);
        Tell(answers.Told);
        Opened(vault.Id, added);
    }

    public async Task AddNoteAsync(string text)
    {
        if (Services.Session.Current is not { } vault || RefusesToAdd())
        {
            return;
        }

        var note = new SharedNotePlanner(vault.Graph).Preview(text);
        var shown = text.Length > 300 ? text[..300] + "…" : text;
        if (!await Confirm($"Add a note to {VaultName}: {note.Name}{(note.IsHomonym ? " (a homonym)" : string.Empty)}\n\n{shown}", "Add"))
        {
            Services.Log.Info("Action", "Shared note cancelled");
            return;
        }

        var answers = new PreparedAnswers();
        var added = Services.Session.AddNote(answers, note);
        Tell(answers.Told);
        Opened(vault.Id, added is null ? [] : [added]);
    }

    // A reader adds nothing: told so.
    private bool RefusesToAdd()
    {
        if (!Services.Session.IsReadOnly)
        {
            return false;
        }

        Tell("This vault is shared with you to read: nothing can be added to it.");
        return true;
    }

    private void Opened(Guid vault, IReadOnlyList<Core.Graph.Node> added)
    {
        foreach (var node in added)
        {
            Services.Recent.Opened(vault, node.Id);
        }

        if (added is [var one])
        {
            Push(new NodePage(this, one));
        }
        else
        {
            RefreshPage();
        }
    }
}
