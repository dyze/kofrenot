using Kofrenot.Core.Browse;
using Kofrenot.Core.Datasets;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.Phone.Platform;

namespace Kofrenot.Phone.Views;

/// <summary>
/// An image field set on the phone, as Choose… and × do on Windows: a tap on it gives a sheet to
/// choose an image (the picker, images only), take a photo (with a camera app), take an image dataset
/// of the vault (From a dataset…, nothing copied), or remove it (asked
/// first). The image is read, described and saved off the screen's thread (<c>VaultSession.SetImage</c>).
/// </summary>
public sealed partial class NodePage
{
    private void ShowImageChoices(FieldPlanner planner, FieldHolder node, NodeField field) =>
        _shell.ShowSheet(field.Label,
        [
            new("Choose an image…", () => _shell.Run(() => ChooseImageAsync(node, field)), Icon: "file"),
            .. _shell.Services.Camera.IsAvailable
                ? [new SheetChoice("Take a photo", () => _shell.Run(() => _shell.CaptureAsync(CaptureKind.Photo, file => SetImageAsync(node, field, file))), Icon: "camera")]
                : Array.Empty<SheetChoice>(),
            new("From a dataset…", () => PickImageDataset(planner, node, field), Icon: "image"),
            .. field.HasValue
                ? [new SheetChoice("Remove image…", () => _shell.Run(() => RemoveImageAsync(planner, node, field)), Icon: "remove")]
                : Array.Empty<SheetChoice>(),
        ]);

    private async Task ChooseImageAsync(FieldHolder node, NodeField field)
    {
        if (await _shell.PickAsync($"The image of {field.Label}", images: true) is [var file])
        {
            await SetImageAsync(node, field, file);
        }
    }

    // The busy bar while it is read and sealed; what went wrong told.
    private async Task SetImageAsync(FieldHolder node, NodeField field, ImportFile file)
    {
        var answers = new PreparedAnswers();
        Busy.IsVisible = true;
        Fields.IsEnabled = false;
        try
        {
            await _shell.Services.Session.SetImage(answers, node, field, file);
        }
        finally
        {
            Busy.IsVisible = false;
            Fields.IsEnabled = true;
            _shell.Tell(answers.Told);
            Refresh();
        }
    }

    // An image dataset of the vault, found by its name: its latest file, not copied.
    private void PickImageDataset(FieldPlanner planner, FieldHolder node, NodeField field) =>
        _shell.Push(new PickNodePage(
            _shell,
            $"The image of {field.Label}",
            (graph, text) => ImagePicker.Images(graph, text).Select(choice => NodeRow.Of(graph, choice.Node, detail: choice.Image.File.Name)).ToList(),
            "No image dataset of that name.",
            dataset =>
            {
                if (Vault?.Graph.DatasetVersionsOf(dataset.Id) is [.., var image])
                {
                    WriteField(planner.SetImageFrom(node, field, image), "Field image set from a dataset");
                }

                return Task.CompletedTask;
            }));

    private async Task RemoveImageAsync(FieldPlanner planner, FieldHolder node, NodeField field)
    {
        if (await _shell.Confirm($"Remove the image of {field.Label}?", "Remove"))
        {
            WriteField(planner.Clear(node, field), "Field image removed");
        }
    }
}
