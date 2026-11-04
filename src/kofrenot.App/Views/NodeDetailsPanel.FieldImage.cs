using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Datasets;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Storage;
using Microsoft.Win32;

namespace Kofrenot.UI.Views;

/// <summary>
/// An image field: its thumbnail (a click shows the image, as a dataset's), Choose… to set another
/// (<see cref="FieldPlanner.SetImage"/>, its file written as a dataset version's), From a dataset… to
/// take an image dataset's, not copied (<see cref="FieldPlanner.SetImageFrom"/>), Paste, while the
/// clipboard holds an image, to set it once its preview is accepted, × to remove it, asked first.
/// </summary>
public partial class NodeDetailsPanel
{
    private const double ImageHeight = 64;

    /// <summary>Saves an image chosen for an image field: its files, then its value; false when it could not (the user is told).</summary>
    public Func<FieldSet, Func<Stream>, Task<bool>>? SaveImage { get; set; }

    /// <summary>The system's clipboard, whose image or file can be pasted into an image field or a dataset; null: no Paste button.</summary>
    public IClipboard? Clipboard
    {
        get => DatasetBox.Clipboard;
        set => DatasetBox.Clipboard = value;
    }

    private FrameworkElement ImageEditorOf(NodeField field, Thickness margin, bool canEdit)
    {
        var panel = new StackPanel { Margin = margin, Orientation = Orientation.Horizontal };
        if (field.Image?.Image is { } image)
        {
            panel.Children.Add(ThumbnailOf(image, field.Display));
        }
        else if (!canEdit)
        {
            panel.Children.Add(new TextBlock { Opacity = 0.7, Text = "No image" });
        }

        if (canEdit && SaveImage is not null)
        {
            var choose = new Button { Margin = new Thickness(field.HasValue ? 8 : 0, 0, 0, 0), Padding = new Thickness(10, 2, 10, 2), Content = "Choose…", VerticalAlignment = VerticalAlignment.Center };
            choose.Click += (_, _) => _ = ChooseImage(field);
            panel.Children.Add(choose);
            var fromDataset = new Button { Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(10, 2, 10, 2), Content = "From a dataset…", VerticalAlignment = VerticalAlignment.Center };
            fromDataset.Click += (_, _) => ChooseImageDataset(field);
            panel.Children.Add(fromDataset);
            if (Clipboard is { } clipboard)
            {
                var paste = new Button { Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(10, 2, 10, 2), Content = "Paste", ToolTip = "Set the image copied: a screenshot, an image file", VerticalAlignment = VerticalAlignment.Center };
                paste.Click += (_, _) => _ = PasteImage(field, clipboard);
                PasteButtons.Follow(paste, clipboard, PasteOffer.FitsImage);
                panel.Children.Add(paste);
            }

            if (field.HasValue)
            {
                panel.Children.Add(SmallButton("remove", "Remove this image", () => RemoveImage(field), focusable: true));
            }
        }

        return panel;
    }

    // The thumbnail, read off the UI thread (a server vault downloads it); the file's name until it is here.
    private FrameworkElement ThumbnailOf(DatasetEdited image, string name)
    {
        var frame = new Border { Height = ImageHeight, MinWidth = ImageHeight, Cursor = Cursors.Hand, ToolTip = $"{name}: click to show it", Child = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Text = name } };
        frame.MouseLeftButtonUp += (_, _) => ShowImage?.Invoke(image);
        _ = LoadThumbnail(frame, image.Id);
        return frame;
    }

    private static async Task LoadThumbnail(Border frame, Guid version)
    {
        if (await ThumbnailCache.Shared.Get(version) is { } bitmap)
        {
            frame.Child = new Image { Source = bitmap, Height = ImageHeight, Stretch = System.Windows.Media.Stretch.Uniform };
        }
    }

    private async Task ChooseImage(NodeField field)
    {
        var dialog = new OpenFileDialog
        {
            Title = $"Choose the image of {field.Label}",
            CheckFileExists = true,
            Filter = "Images|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.tif;*.tiff;*.ico;*.webp|All files|*.*",
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true)
        {
            var path = dialog.FileName;
            await SetImage(field, () => ImportFile.FromPath(path));
        }
    }

    // The image copied, or the image file copied in Explorer: set once its preview is accepted.
    private async Task PasteImage(NodeField field, IClipboard clipboard)
    {
        Log.Info("Action", "Paste of an image into a field");
        await SetImage(field, () => PasteOffer.Read(clipboard)?.OneFile, file =>
        {
            var accepted = PastePreviewDialog.Confirm(Window.GetWindow(this), $"Set this image as {field.Label}?", "Set", file);
            if (!accepted)
            {
                Log.Info("Action", "Paste of an image cancelled");
            }

            return accepted;
        });
    }

    /// <summary>Reads the image, described off the UI thread, then saves it as the field's value, once <paramref name="accept"/> says so.</summary>
    private async Task SetImage(NodeField field, Func<ImportFile?> read, Func<ImportFile, bool>? accept = null)
    {
        if (Browser is not { Graph: { } graph, Details: { } details } || SaveImage is null)
        {
            return;
        }

        try
        {
            if (read() is not { } file)
            {
                MessageDialog.Inform(Window.GetWindow(this), "The clipboard holds no image any more.");
                return;
            }

            if (accept?.Invoke(file) == false)
            {
                return;
            }

            var described = await Task.Run(() =>
            {
                using var content = file.Open();
                return DatasetFiles.Describe(file.Name, content);
            });
            var plan = new FieldPlanner(graph).SetImage(details.Node, field, described);
            ShowStatus(plan.Status);
            if (plan.Changes is [FieldSet value] && await SaveImage(value, file.Open))
            {
                Show();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The message names the file: shown, not logged.
            Log.Warn("Action", $"Cannot read the image chosen ({ex.GetType().Name})");
            MessageDialog.Inform(Window.GetWindow(this), $"Cannot read the image.\n\n{ex.Message}");
        }
    }

    // An image dataset of the vault: its latest file, not copied (FieldPlanner.SetImageFrom).
    private void ChooseImageDataset(NodeField field)
    {
        if (Browser?.Graph is { } graph && ImageDatasetDialog.Ask(Window.GetWindow(this), graph, field.Label) is { } choice)
        {
            WriteFields(planner => planner.SetImageFrom(Node, field, choice.Image), "Field image set from a dataset", field);
        }
    }

    private void RemoveImage(NodeField field)
    {
        if (MessageDialog.Confirm(Window.GetWindow(this), $"Remove the image of {field.Label}?", "Remove"))
        {
            WriteFields(planner => planner.Clear(Node, field), "Field image removed");
        }
    }
}
