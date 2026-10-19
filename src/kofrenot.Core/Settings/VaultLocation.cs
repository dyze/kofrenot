namespace Kofrenot.Core.Settings;

/// <summary>
/// Where a registered vault is: a folder, on this computer or a file share. Two locations are the
/// same folder whatever the case or a trailing separator, as on Windows.
/// </summary>
public sealed record VaultLocation(string Folder)
{
    private string Key => Folder.TrimEnd('\\', '/').ToUpperInvariant();

    public bool Equals(VaultLocation? other) => other is not null && Key == other.Key;

    public override int GetHashCode() => Key.GetHashCode(StringComparison.Ordinal);
}
