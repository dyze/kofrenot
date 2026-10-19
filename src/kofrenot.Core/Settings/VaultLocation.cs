namespace Kofrenot.Core.Settings;

/// <summary>
/// Where a registered vault is: a folder, on this computer or a file share. Two locations are the
/// same folder whatever the case or a trailing separator, as on Windows.
/// </summary>
public sealed record VaultLocation(string Folder)
{
    /// <summary>The folder's own name ("Personal" for <c>D:\Sync\Personal</c>): how a vault is shown before its name is known.</summary>
    public string FolderName => Path.GetFileName(Trimmed) is { Length: > 0 } name ? name : Folder;

    private string Trimmed => Folder.TrimEnd('\\', '/');

    private string Key => Trimmed.ToUpperInvariant();

    public bool Equals(VaultLocation? other) => other is not null && Key == other.Key;

    public override int GetHashCode() => Key.GetHashCode(StringComparison.Ordinal);
}
