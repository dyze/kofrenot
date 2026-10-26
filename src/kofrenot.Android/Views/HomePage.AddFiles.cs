using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Kofrenot.Core.Datasets;
using Kofrenot.Core.Logging;
using Kofrenot.Phone.Platform;

namespace Kofrenot.Phone.Views;

/// <summary>
/// Files added from the phone: picked with Android's picker, previewed (their number and size,
/// the names taken), then each added as a dataset named after its file, uploaded at the next sync.
/// One file added opens its dataset; several are listed among the recent nodes.
/// </summary>
public sealed partial class HomePage
{
    private void AddFiles_Click(object? sender, RoutedEventArgs e) => _shell.Run(AddFilesAsync);

    private async Task AddFilesAsync()
    {
        if (Vault is not { } vault || TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
        {
            return;
        }

        var picked = await storage.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Add files", AllowMultiple = true });
        if (picked.Count == 0)
        {
            return;
        }

        var files = new List<ImportFile>();
        foreach (var file in picked)
        {
            files.Add(await _shell.Services.Picked.ImportFileOf(file));
        }

        var preview = new FileImportPlanner(vault.Graph).Preview(files);
        if (!await _shell.Confirm(PreviewText(preview), "Add"))
        {
            _shell.Services.Log.Info("Action", "Import of files cancelled");
            return;
        }

        var answers = new PreparedAnswers();
        var added = await _shell.Services.Session.ImportFiles(answers, preview);
        _shell.Tell(answers.Told);
        foreach (var node in added)
        {
            _shell.Services.Recent.Opened(vault.Id, node.Id);
        }

        if (added is [var one])
        {
            _shell.Push(new NodePage(_shell, one));
        }
        else
        {
            Refresh();
        }
    }

    private static string PreviewText(ImportPreview preview) =>
        preview.Heading + "\n\n" + string.Join("\n", preview.Rows.Select(row =>
            $"+ {row.NodeName} ({row.Type.Name}, {ByteSize.Format(row.File.Size)}){(row.IsHomonym ? ", a homonym" : string.Empty)}"));
}
