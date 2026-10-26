using Kofrenot.Core.Logging;

namespace Kofrenot.Core.Datasets;

/// <summary>
/// Datasets' files opened with another app: decrypted into <paramref name="folder"/>, each in a
/// folder of its own so its name is kept, and deleted at the next start, once the apps they were
/// given to are done.
/// </summary>
public sealed class OpenedFiles(string folder, ILog? log = null)
{
    private readonly ILog _log = log ?? NullLog.Instance;

    /// <summary>Where to decrypt a file named <paramref name="fileName"/>; its folder is created.</summary>
    public string NewPath(string fileName)
    {
        var own = Path.Combine(folder, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(own);
        return Path.Combine(own, Path.GetFileName(fileName));
    }

    /// <summary>Deletes the files opened before; one still in use stays until the next start.</summary>
    public void Clear()
    {
        if (!Directory.Exists(folder))
        {
            return;
        }

        foreach (var opened in Directory.EnumerateDirectories(folder))
        {
            try
            {
                Directory.Delete(opened, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _log.Warn("App", $"A file opened before cannot be deleted yet ({ex.GetType().Name})");
            }
        }
    }
}
