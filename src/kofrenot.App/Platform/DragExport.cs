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

    /// <summary>
    /// Drags data of kofrenot's own (<paramref name="format"/>, <paramref name="data"/>) that also
    /// offers a file, exported only if another program asks for it (<see cref="LazyFileDrop"/>).
    /// </summary>
    /// <param name="export">Writes the file to the path given.</param>
    /// <returns>The effect of the drop, and whether the file was exported (dragged out).</returns>
    /// <exception cref="IOException">The file cannot be exported.</exception>
    public static (DragDropEffects Effect, bool DraggedOut) Drag(DependencyObject source, string format, object data, string fileName, Action<string> export, DragDropEffects allowed)
    {
        DeleteLeftovers();
        var folder = Path.Combine(Root, Guid.NewGuid().ToString("N"));
        var lazy = new LazyFileDrop(format, data, () =>
        {
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, fileName);
            export(path);
            return path;
        });
        try
        {
            var effect = DragDrop.DoDragDrop(source, new DataObject(lazy), allowed);
            return (effect, lazy.WasMade);
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                TryDelete(folder);
            }
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
