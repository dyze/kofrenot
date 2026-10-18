using System.IO;
using System.Windows;

namespace Kofrenot.UI.Platform;

/// <summary>
/// Drags a dataset's file out of kofrenot: Windows only takes a file on disk, so it is exported
/// into a folder of its own under <c>%TEMP%\kofrenot</c>, dropped, then deleted. Folders left by an
/// earlier drag (kofrenot stopped meanwhile) are deleted first.
/// </summary>
internal static class DragExport
{
    private static string Root => Path.Combine(Path.GetTempPath(), "kofrenot");

    /// <param name="export">Writes the file to the path given.</param>
    /// <returns>True when the file was dropped somewhere.</returns>
    /// <exception cref="IOException">The file cannot be exported.</exception>
    public static bool Drag(DependencyObject source, string fileName, Action<string> export)
    {
        DeleteLeftovers();
        var folder = Path.Combine(Root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, fileName);
            export(path);
            var data = new DataObject(DataFormats.FileDrop, new[] { path });
            return DragDrop.DoDragDrop(source, data, DragDropEffects.Copy) != DragDropEffects.None;
        }
        finally
        {
            TryDelete(folder);
        }
    }

    private static void DeleteLeftovers()
    {
        if (Directory.Exists(Root))
        {
            foreach (var folder in Directory.EnumerateDirectories(Root))
            {
                TryDelete(folder);
            }
        }
    }

    // A file still open elsewhere is left for the next drag.
    private static void TryDelete(string folder)
    {
        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
