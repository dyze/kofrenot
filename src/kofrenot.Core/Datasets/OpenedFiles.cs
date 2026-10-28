using Kofrenot.Core.Logging;

namespace Kofrenot.Core.Datasets;

/// <summary>
/// Files handed to another app, outside the vault: a dataset's file decrypted to be opened with an
/// app, a photo the phone's camera app writes. Each is in a folder of its own under
/// <paramref name="folder"/>, so its name is kept, and is deleted when done with, or at the next
/// start, once the apps they were given to are done.
/// </summary>
public sealed class OpenedFiles(string folder, ILog? log = null)
{
    private readonly ILog _log = log ?? NullLog.Instance;

    /// <summary>Where to put a file named <paramref name="fileName"/>; its folder is created.</summary>
    public string NewPath(string fileName)
    {
        var own = Path.Combine(folder, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(own);
        return Path.Combine(own, Path.GetFileName(fileName));
    }

    /// <summary>Deletes a file given by <see cref="NewPath"/>, with its folder; one still in use stays until the next start.</summary>
    public void Delete(string path)
    {
        var parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        if (Path.GetDirectoryName(Path.GetFullPath(path)) is { } own && Path.GetDirectoryName(own) == parent && Directory.Exists(own))
        {
            TryDelete(own);
        }
    }

    /// <summary>Deletes the files given before; one still in use stays until the next start.</summary>
    public void Clear()
    {
        if (!Directory.Exists(folder))
        {
            return;
        }

        foreach (var own in Directory.EnumerateDirectories(folder))
        {
            TryDelete(own);
        }
    }

    private void TryDelete(string own)
    {
        try
        {
            Directory.Delete(own, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Warn("App", $"A file given to another app cannot be deleted yet ({ex.GetType().Name})");
        }
    }
}
