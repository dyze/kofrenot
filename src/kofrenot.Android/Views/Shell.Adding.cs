using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Datasets;
using Kofrenot.Core.Input;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Notes;
using Kofrenot.Core.Storage;
using Kofrenot.Phone.Platform;

namespace Kofrenot.Phone.Views;

/// <summary>
/// What the phone adds to the open vault, after a preview naming it and where it goes: files (picked with ＋, or
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

        if (shared.Text is { } text)
        {
            Run(() => AddNoteAsync(text));
        }
        else if (shared.Files is [var image] && ImagePicker.CanBeImage(image) && !Services.Session.IsReadOnly)
        {
            OfferSharedImage(image);
        }
        else
        {
            Run(() => AddFilesAsync(shared.Files));
        }
    }

    /// <summary>Offers where to add the files (<see cref="OfferDestinations"/>), then adds them as datasets.</summary>
    public Task AddFilesAsync(IReadOnlyList<ImportFile> files)
    {
        if (Services.Session.Current is { } vault && !RefusesToAdd())
        {
            var planner = new FileImportPlanner(vault.Graph);
            OfferDestinations(vault, "Import of files cancelled", under =>
            {
                var preview = planner.Preview(files, under);
                var rows = preview.Rows.Select(row => $"{(row.IsUpdate ? "↻" : "+")} {row.NodeName} ({row.TypeName}, {ByteSize.Format(row.Size)}){Remark(row)}");
                return new Addition(preview.Heading, string.Join("\n", rows), async () =>
                {
                    var answers = new PreparedAnswers();
                    var added = await Services.Session.ImportFiles(answers, preview);
                    Tell(answers.Told);
                    return added;
                });
            });
        }

        return Task.CompletedTask;
    }

    /// <summary>Offers where to add the note (<see cref="OfferDestinations"/>), then adds it.</summary>
    public Task AddNoteAsync(string text)
    {
        if (Services.Session.Current is { } vault && !RefusesToAdd())
        {
            var planner = new SharedNotePlanner(vault.Graph);
            var shown = text.Length > 300 ? text[..300] + "…" : text;
            OfferDestinations(vault, "Shared note cancelled", under =>
            {
                var note = planner.Preview(text, under);
                return new Addition($"Add a note: {note.Name}{(note.IsHomonym ? " (a homonym)" : string.Empty)}", shown, () =>
                {
                    var answers = new PreparedAnswers();
                    var added = Services.Session.AddNote(answers, note);
                    Tell(answers.Told);
                    return Task.FromResult<IReadOnlyList<Core.Graph.Node>>(added is null ? [] : [added]);
                });
            });
        }

        return Task.CompletedTask;
    }

    private static string Remark(ImportRow row) => row switch
    {
        { IsUpdate: true } => ", a new version",
        { IsHomonym: true } => ", a homonym",
        _ => string.Empty,
    };

    /// <summary>What a share or ＋ adds, for a destination: its heading, its lines, and how to add it.</summary>
    private sealed record Addition(string Heading, string Lines, Func<Task<IReadOnlyList<Core.Graph.Node>>> Add);

    /// <summary>
    /// A sheet with what is added to the vault's last destination (<see cref="ImportDestinations"/>):
    /// add it there, choose another node (<see cref="PickDestinationPage"/>, then a preview for it), or
    /// add it with no parent. The destination chosen is kept for the next time.
    /// </summary>
    private void OfferDestinations(Vault vault, string cancelled, Func<ParentLink?, Addition> additionTo)
    {
        var last = Services.ImportDestinations.Of(vault.Id);
        var resolved = ImportDestinations.Resolve(vault.Graph, last);
        var addition = additionTo(resolved?.Under);
        var lines = new TextBlock { Text = addition.Lines, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(16, 0, 16, 8) };
        ShowSheet($"{addition.Heading} to {VaultName}", lines,
        [
            .. resolved is null ? Array.Empty<SheetChoice>() : [new SheetChoice($"Add into {resolved.Label}", () => Run(() => AddAsync(vault, last, addition)), Icon: "import")],
            new(resolved is null ? "Add into…" : "Add elsewhere…", () => Push(new PickDestinationPage(this, "Add into", "Add into", view: null,
                destination => ConfirmAndAddAsync(vault, destination, additionTo(ImportDestinations.Resolve(vault.Graph, destination)?.Under), cancelled))), Icon: "view"),
            new("Add with no parent", () => Run(() => resolved is null
                ? AddAsync(vault, null, addition)
                : ConfirmAndAddAsync(vault, null, additionTo(null), cancelled)), Icon: "unlink"),
        ]);
    }

    private async Task ConfirmAndAddAsync(Vault vault, ImportDestination? destination, Addition addition, string cancelled)
    {
        var into = ImportDestinations.Resolve(vault.Graph, destination) is { } resolved ? $"into {resolved.Label}" : "with no parent";
        if (await Confirm($"{addition.Heading} to {VaultName}, {into}\n\n{addition.Lines}", "Add"))
        {
            await AddAsync(vault, destination, addition);
        }
        else
        {
            Services.Log.Info("Action", cancelled);
        }
    }

    private async Task AddAsync(Vault vault, ImportDestination? destination, Addition addition)
    {
        Services.ImportDestinations.Set(vault.Id, destination);
        Opened(vault.Id, await addition.Add());
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
            ShowOverHome(new NodePage(this, one));
        }
        else
        {
            RefreshPage();
        }
    }
}
