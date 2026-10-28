using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Storage;
using Microsoft.Win32;

namespace Kofrenot.UI.Views;

/// <summary>
/// An image field: its thumbnail (a click shows the image, as a dataset's), Choose… to set another
/// (<see cref="FieldPlanner.SetImage"/>, its file written as a dataset version's), × to remove it, asked first.
/// </summary>
public partial class NodeDetailsPanel
{
    private const double ImageHeight = 64;

    /// <summary>Saves an image chosen for an image field: its files, then its value; false when it could not (the user is told).</summary>
    public Func<FieldSet, Func<Stream>, Task<bool>>? SaveImage { get; set; }

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
            var choose = new Button { Margin = new Thickness(field.HasValue ? 8 : 0, 0, 0, 0), Padding = new Thickness(10, 2, 10, 2), Content = "Choose…", Focusable = false, VerticalAlignment = VerticalAlignment.Center };
            choose.Click += (_, _) => _ = ChooseImage(field);
            panel.Children.Add(choose);
            if (field.HasValue)
            {
                panel.Children.Add(SmallButton("", "Remove this image", () => RemoveImage(field)));
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
        if (dialog.ShowDialog(Window.GetWindow(this)) != true || Browser is not { Graph: { } graph, Details: { } details } || SaveImage is null)
        {
            return;
        }

        var path = dialog.FileName;
        try
        {
            var file = await Task.Run(() => DatasetFiles.Describe(path));
            var plan = new FieldPlanner(graph).SetImage(details.Node, field, file);
            FieldStatus.Text = plan.Status;
            FieldStatus.Visibility = plan.Status.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            if (plan.Changes is [FieldSet value] && await SaveImage(value, () => File.OpenRead(path)))
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

    private void RemoveImage(NodeField field)
    {
        if (MessageDialog.Confirm(Window.GetWindow(this), $"Remove the image of {field.Label}?", "Remove"))
        {
            WriteFields(planner => planner.Clear(Node, field), "Field image removed");
        }
    }
}
