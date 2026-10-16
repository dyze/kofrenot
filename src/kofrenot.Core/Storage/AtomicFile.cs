namespace Kofrenot.Core.Storage;

/// <summary>
/// Writes a file under a temporary name, then renames it: a reader (another app, the sync tool)
/// sees the whole file or nothing.
/// </summary>
internal static class AtomicFile
{
    public const string TemporaryExtension = ".tmp";

    public static void Write(string path, byte[] content, bool overwrite)
    {
        var temporary = path + TemporaryExtension;
        File.WriteAllBytes(temporary, content);
        try
        {
            File.Move(temporary, path, overwrite);
        }
        catch
        {
            File.Delete(temporary);
            throw;
        }
    }
}
