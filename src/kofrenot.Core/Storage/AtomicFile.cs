namespace Kofrenot.Core.Storage;

/// <summary>
/// Writes a file under a temporary name, then renames it: a reader (another app, the sync tool)
/// sees the whole file or nothing.
/// </summary>
internal static class AtomicFile
{
    public const string TemporaryExtension = ".tmp";

    public static void Write(string path, byte[] content, bool overwrite) =>
        Write(path, output => output.Write(content), overwrite);

    /// <summary>
    /// Writes the file through <paramref name="write"/>. When it throws, the temporary file is
    /// deleted and nothing is renamed.
    /// </summary>
    public static void Write(string path, Action<Stream> write, bool overwrite)
    {
        var temporary = path + TemporaryExtension;
        try
        {
            using (var output = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                write(output);
            }

            File.Move(temporary, path, overwrite);
        }
        catch
        {
            File.Delete(temporary);
            throw;
        }
    }
}
