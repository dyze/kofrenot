namespace Kofrenot.Core.Settings;

/// <summary>
/// Where a registered vault is: a folder, on this computer or a file share, or a store on a
/// server (<paramref name="Store"/>) whose <paramref name="Folder"/> is its local mirror. Two
/// locations are the same folder whatever the case or a trailing separator, as on Windows.
/// </summary>
public sealed record VaultLocation(string Folder, StoreAddress? Store = null)
{
    /// <summary>
    /// How a vault is shown before its name is known: the folder's own name ("Personal" for
    /// <c>D:\Sync\Personal</c>), or the server's host name.
    /// </summary>
    public string FolderName => Store is { } store ? store.Server.Host
        : Path.GetFileName(Trimmed) is { Length: > 0 } name ? name
        : Folder;

    public bool IsOnServer => Store is not null;

    private string Trimmed => Folder.TrimEnd('\\', '/');

    private string Key => Trimmed.ToUpperInvariant();

    public bool Equals(VaultLocation? other) => other is not null && Key == other.Key;

    public override int GetHashCode() => Key.GetHashCode(StringComparison.Ordinal);
}

/// <summary>A store on a Kofre server.</summary>
public sealed record StoreAddress(Uri Server, Guid Id);

/// <summary>The server account of this kofrenot: one for all its server vaults.</summary>
public sealed record ServerAccount(Uri Server, string Email);
