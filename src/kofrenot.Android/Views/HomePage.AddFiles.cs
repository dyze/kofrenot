using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Kofrenot.Core.Datasets;

namespace Kofrenot.Phone.Views;

/// <summary>＋: files picked with Android's picker, then added as datasets after their preview (<see cref="Shell.AddFilesAsync"/>).</summary>
public sealed partial class HomePage
{
    private void AddFiles_Click(object? sender, RoutedEventArgs e) => _shell.Run(AddFilesAsync);

    private async Task AddFilesAsync()
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
        {
            return;
        }

        var picked = await storage.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Add files", AllowMultiple = true });
        var files = new List<ImportFile>();
        foreach (var file in picked)
        {
            files.Add(await _shell.Services.Picked.ImportFileOf(file));
        }

        if (files.Count > 0)
        {
            await _shell.AddFilesAsync(files);
        }
    }
}
