namespace Kofrenot.Core.Server;

/// <summary>
/// The paths of a vault's files in a store (<c>changes/3f/3f9c….kfc</c>), and where they are in
/// its mirror. Only paths a vault writes are accepted, so a store cannot write outside the mirror.
/// </summary>
internal static class StorePaths
{
    private const int MaxSegments = 8;

    public static bool IsValid(string path)
    {
        var segments = path.Split('/');
        return segments.Length <= MaxSegments && segments.All(IsValidSegment);
    }

    public static string LocalOf(string mirror, string path) => Path.Combine([mirror, .. path.Split('/')]);

    public static string PathOf(string mirror, string file) => Path.GetRelativePath(mirror, file).Replace(Path.DirectorySeparatorChar, '/');

    private static bool IsValidSegment(string segment) =>
        segment is { Length: > 0 and <= 128 } and not "." and not ".."
        && segment.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-');
}
