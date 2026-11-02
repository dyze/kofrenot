using System.IO;
using System.Windows;

namespace Kofrenot.UI.Platform;

/// <summary>
/// Drags files out of kofrenot: Windows only takes files on disk, so they are exported first into a
/// folder of its own under <c>%TEMP%\kofrenot</c>, dragged with a standard <see cref="DataObject"/>,
/// then deleted. Folders left by an earlier drag (kofrenot stopped meanwhile) are deleted first.
/// </summary>
/// <remarks>
/// Not lazily, when another program asks: a hand-made <see cref="IDataObject"/>, wrapped by WPF,
/// crashes Explorer on the drop. The standard one does not.
/// </remarks>
internal static class DragExport
{
    private static string Root => Path.Combine(Path.GetTempPath(), "kofrenot");

    /// <param name="export">Writes the file to the path given.</param>
    /// <returns>True when the file was dropped somewhere.</returns>
    /// <exception cref="IOException">The file cannot be exported.</exception>
    public static bool Drag(DependencyObject source, string fileName, Action<string> export) =>
        Drag(source, folder =>
        {
            var path = Path.Combine(folder, fileName);
            export(path);
            return [path];
        }, DragDropEffects.Copy) != DragDropEffects.None;

    /// <summary>
    /// Drags files and folders, also carrying <paramref name="ownData"/> in <paramref name="ownFormat"/>
    /// when given, for a drop inside kofrenot: a text, so no .NET object is handed to other programs.
    /// </summary>
    /// <param name="export">Writes the files and folders into the folder given; their paths.</param>
    /// <returns>The effect of the drop.</returns>
    /// <exception cref="IOException">A file cannot be exported.</exception>
    public static DragDropEffects Drag(DependencyObject source, Func<string, string[]> export, DragDropEffects allowed, string? ownFormat = null, string? ownData = null)
    {
        DeleteLeftovers();
        var folder = Path.Combine(Root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var data = new DataObject(DataFormats.FileDrop, export(folder));
            if (ownFormat is not null && ownData is not null)
            {
                data.SetData(ownFormat, ownData);
            }

            return DragDrop.DoDragDrop(source, data, allowed);
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
