namespace Kofrenot.Core.Storage;

/// <summary>What a folder chosen as vault holds.</summary>
public enum FolderKind
{
    /// <summary>Missing or empty: a new vault can be created in it.</summary>
    Empty,

    /// <summary>Holds a vault header.</summary>
    Vault,

    /// <summary>Holds other files: not used, so nothing of the user's is mixed with the vault.</summary>
    Other,
}

/// <summary>
/// The layout of a vault folder, and the creation of a new one. The only place the names of
/// its files are written.
/// </summary>
public static class VaultFolder
{
    public const string HeaderFile = "kofrenot-vault.json";
    public const string ChangesFolder = "changes";
    public const string ChangeExtension = ".kfc";
    public const string ContentFolder = "content";
    public const string ContentExtension = ".kfn";
    public const string DatasetExtension = ".kfd";

    public static FolderKind Inspect(string folder)
    {
        if (File.Exists(HeaderOf(folder)))
        {
            return FolderKind.Vault;
        }

        return Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any() ? FolderKind.Other : FolderKind.Empty;
    }

    /// <summary>Creates a vault in an empty folder: its header, with a new master key.</summary>
    /// <exception cref="IOException">The folder is not empty.</exception>
    public static (VaultHeader Header, byte[] MasterKey) Create(string folder, string passPhrase, int iterations = KeyWrap.DefaultIterations)
    {
        if (Inspect(folder) != FolderKind.Empty)
        {
            throw new IOException("A vault can only be created in an empty folder");
        }

        var id = Guid.NewGuid();
        var (wrapped, masterKey) = KeyWrap.Create(passPhrase, id.ToByteArray(), iterations);
        var header = new VaultHeader(VaultFormat.Current, id, wrapped);
        Directory.CreateDirectory(ChangesOf(folder));
        header.Write(HeaderOf(folder));
        return (header, masterKey);
    }

    public static string HeaderOf(string folder) => Path.Combine(folder, HeaderFile);

    internal static string ChangesOf(string folder) => Path.Combine(folder, ChangesFolder);

    /// <summary><c>changes/3f/3f9c…e1.kfc</c>: sub-folders by the first two characters, so none holds too many files.</summary>
    internal static string ChangeFileOf(string folder, Guid change) => SpreadFileOf(ChangesOf(folder), change, ChangeExtension);

    /// <summary><c>content/3f/3f9c…e1.kfn</c>: the content of a note version, named by its change.</summary>
    internal static string ContentFileOf(string folder, Guid version) => SpreadFileOf(Path.Combine(folder, ContentFolder), version, ContentExtension);

    /// <summary><c>content/b2/b2d4…07.kfd</c>: the file of a dataset version, named by its change.</summary>
    internal static string DatasetFileOf(string folder, Guid version) => SpreadFileOf(Path.Combine(folder, ContentFolder), version, DatasetExtension);

    private static string SpreadFileOf(string parent, Guid id, string extension)
    {
        var name = id.ToString("N");
        return Path.Combine(parent, name[..2], name + extension);
    }
}
