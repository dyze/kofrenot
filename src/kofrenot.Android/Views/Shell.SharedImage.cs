using Kofrenot.Core.Browse;
using Kofrenot.Core.Datasets;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.Core.Logging;
using Kofrenot.Phone.Platform;

namespace Kofrenot.Phone.Views;

/// <summary>
/// One image shared by another app (a browser's Share image): the image of a node's image field, or a
/// dataset as before. The node shown when it comes, when it has an image field, is offered first, in
/// the sheet and in the list of nodes; the back stack is kept, the node's page shown with its picture.
/// </summary>
public sealed partial class Shell
{
    private void OfferSharedImage(ImportFile image)
    {
        var shown = (PageHost.Content as NodePage)?.Shown is { } node && Services.Session.Current is { } vault
            && ImagePicker.FieldsOf(vault.Graph, node.Id).Count > 0 ? node : null;
        ShowSheet(image.Name,
        [
            .. shown is null ? Array.Empty<SheetChoice>() : [new SheetChoice($"Set as the image of {shown.Name}", () => Run(() => SetSharedImageAsync(shown, image)), Icon: "CameraIcon")],
            new(shown is null ? "Set as the image of a node…" : "Set as the image of another node…", () => PickImageTarget(image, shown), Icon: "ListIcon"),
            new("Add as a dataset", () => Run(() => AddFilesAsync([image])), Icon: "FileIcon"),
        ]);
    }

    private void PickImageTarget(ImportFile image, Node? shown) =>
        Push(new PickNodePage(
            this,
            "Set the image of",
            (graph, text) => ImagePicker.Targets(graph, text, shown).Select(node => NodeRow.Of(graph, node, detail: node == shown ? "Shown now" : null)).ToList(),
            "Type a name: the nodes with an image field are listed.",
            node => SetSharedImageAsync(node, image)));

    // Its one image field set; with several, the one chosen from a sheet.
    private Task SetSharedImageAsync(Node node, ImportFile image)
    {
        if (Services.Session.Current is not { } vault)
        {
            return Task.CompletedTask;
        }

        var fields = ImagePicker.FieldsOf(vault.Graph, node.Id);
        if (fields is [var only])
        {
            return SetSharedImageAsync(node, only, image);
        }

        ShowSheet($"The image of {node.Name}", fields.Select(field => new SheetChoice(field.Label, () => Run(() => SetSharedImageAsync(node, field, image)))));
        return Task.CompletedTask;
    }

    // Read and sealed off the screen's thread; then the node's page, the one shown refreshed, else opened over it.
    private async Task SetSharedImageAsync(Node node, NodeField field, ImportFile image)
    {
        Services.Log.Info("Action", "Shared image set as a field's image");
        var answers = new PreparedAnswers();
        var set = await Services.Session.SetImage(answers, node, field, image);
        Tell(answers.Told);
        if (!set || Services.Session.Current is not { } vault)
        {
            return;
        }

        Services.Recent.Opened(vault.Id, node.Id);
        ShowSetImage(node);
    }

    private void ShowSetImage(Node node)
    {
        if ((PageHost.Content as NodePage)?.Shown?.Id == node.Id)
        {
            RefreshPage();
        }
        else
        {
            Push(new NodePage(this, node));
        }
    }
}
