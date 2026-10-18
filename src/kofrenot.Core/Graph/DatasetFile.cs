namespace Kofrenot.Core.Graph;

/// <summary>
/// The file of a dataset version: its name as imported (without folder), its size in bytes, and
/// its SHA-256 in lower-case hex.
/// </summary>
public sealed record DatasetFile(string Name, long Size, string Sha256)
{
    private const string Fallback = "dataset";

    // Windows' own invalid characters, the same on every OS, so a name imported anywhere exports on Windows.
    private static readonly char[] Invalid = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    /// <summary>
    /// The name to export the file under on Windows: invalid characters replaced by '_', and no
    /// trailing dot or space.
    /// </summary>
    public string SafeName
    {
        get
        {
            var safe = new string(Name.Select(c => c < ' ' || Invalid.Contains(c) ? '_' : c).ToArray()).TrimEnd('.', ' ');
            return safe.Length == 0 ? Fallback : safe;
        }
    }
}
